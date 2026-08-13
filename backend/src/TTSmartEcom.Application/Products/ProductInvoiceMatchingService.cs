using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Domain.Catalog;
using TTSmartEcom.Domain.Products;

namespace TTSmartEcom.Application.Products;

public sealed partial class ProductInvoiceMatchingService(
    IProductCatalogRepository products,
    ICatalogRepository catalog)
{
    private const int MaxItems = 500;

    public async Task<ProductInvoiceMatchResult?> MatchAsync(
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() > MaxItems) return null;

        ProductPage page = await products.ListAsync(new ProductListQuery(
            1, 10_000, null, null, null, null, null, null, "createdAt", "desc", true, true), cancellationToken);
        IReadOnlyList<BrandRecord> brands = await catalog.ListBrandsAsync(cancellationToken);
        JsonElement[] sourceItems = payload.EnumerateArray().ToArray();
        if (sourceItems.Any(static item => item.ValueKind != JsonValueKind.Object)) return null;

        string[] rawCodes = NormalizeRepeatedInvoiceCodePrefix(sourceItems);
        List<IReadOnlyDictionary<string, object?>> items = new(sourceItems.Length);
        for (int index = 0; index < sourceItems.Length; index++)
        {
            JsonElement item = sourceItems[index];
            Dictionary<string, object?> output = CopyAllowed(item);
            string rawName = Bound(Text(item, "rawScannedName"), 1_000) ?? string.Empty;
            string rawCode = rawCodes[index];
            string canonicalCode = BuildCanonicalCode(rawCode, rawName);
            string normalizedCodeKey = NormalizeCodeKey(canonicalCode);
            string coreModelKey = ExtractCoreModelKey(canonicalCode);
            BrandResolution brand = ResolveBrand(Text(item, "brand"), brands);
            string sourceConfidence = NormalizeConfidence(Text(item, "confidence"));
            bool isLowConfidence = sourceConfidence == "low";
            HashSet<string> scanType = TokenizeTypeWords(rawName);

            string matchStatus = "NEW_PRODUCT";
            string? matchedProductId = "NEW_PRODUCT";
            string[] candidateProductIds = [];
            bool autoSelected = false;
            bool requiresReview = false;
            string matchReason = "Không tìm thấy sản phẩm có cùng model trong DB.";
            string confidence = canonicalCode.Length == 0 ? "low" : sourceConfidence;

            ProductRecord[] exactMatches = normalizedCodeKey.Length == 0
                ? []
                : page.Products.Where(product => NormalizeCodeKey(product.Code) == normalizedCodeKey).ToArray();

            if (exactMatches.Length == 1)
            {
                matchedProductId = exactMatches[0].Id;
                matchStatus = "MATCHED";
                matchReason = "Khớp chính xác mã sản phẩm đầy đủ.";
                confidence = "high";
            }
            else if (exactMatches.Length > 1)
            {
                matchedProductId = null;
                matchStatus = "POSSIBLE_MATCH";
                candidateProductIds = exactMatches.Select(static product => product.Id).ToArray();
                matchReason = "Có nhiều sản phẩm có mã chuẩn tương đương; cần người dùng xác nhận.";
                confidence = "low";
            }
            else if (coreModelKey.Length > 0)
            {
                ProductRecord[] coreCandidates = page.Products.Where(product =>
                {
                    string productCore = ExtractCoreModelKey(product.Code);
                    if (productCore.Length == 0) productCore = ExtractCoreModelKey(product.Name);
                    return productCore == coreModelKey;
                }).ToArray();

                if (coreCandidates.Length > 0)
                {
                    matchStatus = "POSSIBLE_MATCH";
                    matchedProductId = null;
                    candidateProductIds = coreCandidates.Select(static product => product.Id).ToArray();
                    confidence = "low";

                    ProductRecord[] safeAutoCandidates = coreCandidates.Where(product =>
                    {
                        if (isLowConfidence || !BrandsCompatible(brand.Name, product.Brand) ||
                            !HasTypeOverlap(scanType, product.Name)) return false;

                        string derivedProductCode = BuildCanonicalCode(product.Code, product.Name);
                        string derivedProductKey = NormalizeCodeKey(derivedProductCode);
                        string productNameKey = NormalizeCodeKey(product.Name);
                        return derivedProductKey == normalizedCodeKey ||
                            (normalizedCodeKey.Length > 0 && productNameKey.Contains(normalizedCodeKey, StringComparison.Ordinal));
                    }).ToArray();

                    if (coreCandidates.Length == 1 && safeAutoCandidates.Length == 1)
                    {
                        ProductRecord candidate = safeAutoCandidates[0];
                        matchedProductId = candidate.Id;
                        autoSelected = true;
                        requiresReview = true;
                        confidence = "medium";
                        matchReason = $"Khớp duy nhất model {Display(canonicalCode, rawCode)}; tên DB chứa đủ mã chuẩn nhưng DB đang dùng mã ngắn.";
                    }
                    else if (coreCandidates.Length == 1)
                    {
                        ProductRecord candidate = coreCandidates[0];
                        string candidateCode = BuildCanonicalCode(candidate.Code, candidate.Name);
                        HashSet<string> scanSpecs = ExtractTechnicalSpecKeys(canonicalCode);
                        HashSet<string> candidateSpecs = ExtractTechnicalSpecKeys(candidateCode);
                        bool conflictingSpecs = scanSpecs.Count > 0 && candidateSpecs.Count > 0 &&
                            scanSpecs.Any(spec => !candidateSpecs.Contains(spec));
                        matchReason = conflictingSpecs
                            ? $"Khớp model {coreModelKey} nhưng thông số DB khác; cần chọn đúng phiên bản."
                            : $"Khớp model {coreModelKey} nhưng DB đang dùng mã ngắn hoặc thiếu thông số; cần người dùng xác nhận.";
                    }
                    else
                    {
                        matchReason = $"Có {coreCandidates.Length} sản phẩm cùng model {coreModelKey}; cần chọn đúng phiên bản.";
                    }
                }
            }

            if (coreModelKey.Length == 0 && exactMatches.Length == 0)
            {
                string scanCodeKind = CodeKind(canonicalCode);
                HashSet<string> scanSpecs = TokenizeSpec($"{rawName} {canonicalCode}");
                ProductRecord[] fallbackCandidates = page.Products.Where(product =>
                    PassFallbackGates(product, scanCodeKind, scanSpecs, scanType, canonicalCode)).ToArray();
                if (fallbackCandidates.Length > 0)
                {
                    matchStatus = "POSSIBLE_MATCH";
                    matchedProductId = null;
                    candidateProductIds = fallbackCandidates.Select(static product => product.Id).ToArray();
                    matchReason = "Không đọc chắc chắn model; đã tìm thấy sản phẩm gần giống để người dùng chọn.";
                    confidence = "low";
                }
            }

            if (matchedProductId is not null and not "NEW_PRODUCT" &&
                string.IsNullOrWhiteSpace(Text(item, "vat")))
            {
                ProductRecord? selected = page.Products.FirstOrDefault(product => product.Id == matchedProductId);
                if (!string.IsNullOrWhiteSpace(selected?.Vat)) output["vat"] = selected.Vat;
            }

            output["code"] = canonicalCode.Length == 0 ? rawCode : canonicalCode;
            output["rawScannedCode"] = rawCode;
            output["canonicalCode"] = canonicalCode.Length == 0 ? rawCode : canonicalCode;
            output["normalizedCodeKey"] = normalizedCodeKey;
            output["coreModelKey"] = coreModelKey;
            output["brand"] = brand.Name;
            output["brandIsNew"] = brand.IsNew;
            output["matchStatus"] = matchStatus;
            output["matchedProductId"] = matchedProductId;
            output["candidateProductIds"] = candidateProductIds;
            output["autoSelected"] = autoSelected;
            output["requiresReview"] = requiresReview;
            output["matchReason"] = matchReason;
            output["confidence"] = confidence;
            items.Add(output);
        }

        return new ProductInvoiceMatchResult(items);
    }

    private static string[] NormalizeRepeatedInvoiceCodePrefix(IReadOnlyList<JsonElement> items)
    {
        string[] codes = items.Select(item => NormalizeCodeDisplay(Text(item, "code"))).ToArray();
        string[] valid = codes.Where(static code => code.Length > 0).ToArray();
        if (valid.Length <= 1) return codes;

        string[] firstWords = valid.Select(static code => code.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0]).ToArray();
        string firstWord = firstWords[0];
        if (firstWord.Length < 6 || firstWords.Any(word => word != firstWord)) return codes;

        for (int index = 0; index < codes.Length; index++)
        {
            if (codes[index].Length == 0) continue;
            string clean = codes[index][Math.Min(firstWord.Length, codes[index].Length)..].Trim();
            codes[index] = LeadingSeparators().Replace(clean, string.Empty);
        }
        return codes;
    }

    private static bool PassFallbackGates(
        ProductRecord product,
        string scanCodeKind,
        HashSet<string> scanSpecs,
        HashSet<string> scanType,
        string canonicalCode)
    {
        if (scanCodeKind == "model" && CodeKind(product.Code) == "model" &&
            CleanCode(canonicalCode) != CleanCode(product.Code)) return false;

        if (scanSpecs.Count > 0)
        {
            HashSet<string> productSpecs = TokenizeSpec($"{product.Name} {product.Code}");
            if (scanSpecs.Any(spec => !productSpecs.Contains(spec))) return false;
        }

        HashSet<string> productType = TokenizeTypeWords(product.Name);
        return scanType.Any(productType.Contains);
    }

    private static HashSet<string> TokenizeSpec(string? value)
    {
        HashSet<string> tokens = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return tokens;
        foreach (Match match in ModelOrSpecToken().Matches(value)) tokens.Add(match.Value.ToLowerInvariant());
        foreach (Match match in PureNumberToken().Matches(value)) tokens.Add(match.Value.ToLowerInvariant());
        return tokens;
    }

    private static HashSet<string> TokenizeTypeWords(string? value)
    {
        HashSet<string> words = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value)) return words;
        foreach (string word in TypeWordSeparator().Split(RemoveVietnameseTones(value).ToLowerInvariant()))
        {
            if (word.Length > 1 && word.Any(static character => character is >= 'a' and <= 'z') && !word.Any(char.IsDigit))
                words.Add(word);
        }

        if (words.Contains("cong") && words.Contains("to") ||
            words.Contains("cong") && words.Contains("tac") && (words.Contains("to") || words.Contains("tor")) ||
            words.Contains("khoi") && words.Contains("dong") && words.Contains("tu"))
            words.Add("contactor");
        if (words.Contains("contactor"))
        {
            words.UnionWith(["cong", "tac", "to", "contactor"]);
        }

        if (words.Contains("ro") && words.Contains("le")) words.UnionWith(["role", "relay"]);
        if (words.Contains("role") || words.Contains("relay")) words.UnionWith(["ro", "le", "role", "relay"]);

        if (words.Contains("aptomat") || words.Contains("cau") && words.Contains("dao"))
            words.UnionWith(["cb", "mcb", "mccb"]);
        if (words.Contains("cb") || words.Contains("mcb") || words.Contains("mccb"))
            words.UnionWith(["cau", "dao", "aptomat"]);
        return words;
    }

    private static string BuildCanonicalCode(string? rawCode, string? rawName)
    {
        string codeSegment = ExtractCodeSegment(rawCode);
        string nameSegment = ExtractCodeSegment(rawName);
        if (codeSegment.Length == 0) return nameSegment;
        if (nameSegment.Length == 0) return codeSegment;

        string codeCore = ExtractCoreModelKey(codeSegment);
        string nameCore = ExtractCoreModelKey(nameSegment);
        string codeKey = NormalizeCodeKey(codeSegment);
        string nameKey = NormalizeCodeKey(nameSegment);
        return codeCore.Length > 0 && codeCore == nameCore && nameKey.StartsWith(codeKey, StringComparison.Ordinal) &&
            nameKey.Length > codeKey.Length ? nameSegment : codeSegment;
    }

    private static string ExtractCodeSegment(string? value)
    {
        string displayValue = NormalizeCodeDisplay(value);
        if (displayValue.Length == 0) return string.Empty;
        if (displayValue.All(char.IsDigit)) return displayValue;

        string[] tokens = displayValue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeCodeToken).Where(static token => token.Length > 0).ToArray();
        int modelIndex = Array.FindIndex(tokens, IsModelLikeToken);
        if (modelIndex < 0) return string.Empty;

        List<string> codeTokens = [tokens[modelIndex]];
        for (int index = modelIndex + 1; index < tokens.Length && IsTechnicalSpecToken(tokens[index]); index++)
            codeTokens.Add(tokens[index]);
        return NormalizeCodeDisplay(string.Join(' ', codeTokens));
    }

    private static string ExtractCoreModelKey(string? value)
    {
        string segment = ExtractCodeSegment(value);
        if (segment.Length == 0) return string.Empty;
        if (segment.All(char.IsDigit)) return NormalizeCodeKey(segment);
        string? model = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(IsModelLikeToken);
        return NormalizeCodeKey(model);
    }

    private static HashSet<string> ExtractTechnicalSpecKeys(string? value)
    {
        string segment = ExtractCodeSegment(value);
        if (segment.Length == 0) return new HashSet<string>(StringComparer.Ordinal);
        return segment.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Where(IsTechnicalSpecToken).Select(NormalizeCodeKey).Where(static key => key.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool HasTypeOverlap(HashSet<string> scanType, string? productName)
    {
        HashSet<string> productType = TokenizeTypeWords(productName);
        return scanType.Count == 0 || productType.Count == 0 || scanType.Any(productType.Contains);
    }

    private static bool BrandsCompatible(string? scannedBrand, string? productBrand)
    {
        string scanKey = NormalizeBrandKey(scannedBrand);
        string productKey = NormalizeBrandKey(productBrand);
        return scanKey.Length == 0 || productKey.Length == 0 || productKey is "n/a" or "chuaro" || scanKey == productKey;
    }

    private static string CodeKind(string? value)
    {
        string code = value?.Trim() ?? string.Empty;
        if (code.Length == 0) return "none";
        return code.All(char.IsDigit) ? "supplier" : "model";
    }

    private static string CleanCode(string? value) => new((value ?? string.Empty)
        .Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static bool IsTechnicalSpecToken(string value) => TechnicalSpecToken().IsMatch(SanitizeCodeToken(value).ToUpperInvariant());

    private static bool IsModelLikeToken(string value)
    {
        string token = SanitizeCodeToken(value);
        string compact = NormalizeCodeKey(token);
        return compact.Length >= 2 && token.Any(char.IsAsciiLetter) && token.Any(char.IsDigit) && !IsTechnicalSpecToken(token);
    }

    private static string SanitizeCodeToken(string value) => value.Trim().Trim(',', ';', ':', '(', ')', '[', ']', '{', '}');
    private static string NormalizeCodeDisplay(string? value) => string.Join(' ', (value ?? string.Empty)
        .Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeCodeKey(string? value) => new((value ?? string.Empty)
        .Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string NormalizeBrandKey(string? value) => new string(RemoveVietnameseTones(value ?? string.Empty)
        .ToLowerInvariant().Where(static character => !char.IsWhiteSpace(character)).ToArray()).Trim();

    private static string RemoveVietnameseTones(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder result = new(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            result.Append(character is 'đ' or 'Đ' ? 'd' : character);
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Dictionary<string, object?> CopyAllowed(JsonElement item)
    {
        string[] names = ["stt", "rawScannedName", "quantity", "price", "unit", "vat", "taxAmount", "note"];
        Dictionary<string, object?> result = new(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (item.TryGetProperty(name, out JsonElement value)) result[name] = Convert(value);
        }
        result.TryAdd("stt", null);
        result.TryAdd("rawScannedName", string.Empty);
        result.TryAdd("quantity", 0d);
        result.TryAdd("price", null);
        result.TryAdd("unit", null);
        result.TryAdd("vat", null);
        result.TryAdd("taxAmount", 0d);
        result.TryAdd("note", null);
        return result;
    }

    private static object? Convert(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetInt64(out long integer) => integer,
        JsonValueKind.Number when value.TryGetDouble(out double number) && double.IsFinite(number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null,
    };

    private static BrandResolution ResolveBrand(string? value, IReadOnlyList<BrandRecord> brands)
    {
        string raw = Bound(value, 120) ?? string.Empty;
        if (raw.Length == 0) return new BrandResolution(string.Empty, false);
        BrandRecord? existing = brands.FirstOrDefault(brand => NormalizeBrandKey(brand.Brand) == NormalizeBrandKey(raw));
        return existing is null ? new BrandResolution(raw, true) : new BrandResolution(existing.Brand ?? string.Empty, false);
    }

    private static string NormalizeConfidence(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "high" => "high",
        "low" => "low",
        _ => "medium",
    };

    private static string? Bound(string? value, int maximum) => string.IsNullOrWhiteSpace(value)
        ? null
        : value.Trim()[..Math.Min(value.Trim().Length, maximum)];
    private static string? Text(JsonElement item, string name) => item.TryGetProperty(name, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string Display(string primary, string secondary) => primary.Length > 0 ? primary : secondary;

    [GeneratedRegex(@"^[\s_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingSeparators();

    [GeneratedRegex(@"(?=\d+[a-zA-Z]|[a-zA-Z]+\d)[a-zA-Z0-9\-/]+", RegexOptions.CultureInvariant)]
    private static partial Regex ModelOrSpecToken();

    [GeneratedRegex(@"\b\d{3,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex PureNumberToken();

    [GeneratedRegex(@"[\s,.\-/()]+", RegexOptions.CultureInvariant)]
    private static partial Regex TypeWordSeparator();

    [GeneratedRegex(@"^(?:(?:AC|DC)?\d+(?:[.,]\d+)?(?:V|A|W|KW|MW|HP|HZ|KA|MA|VAC|VDC)|\d+/\d+(?:A|V)?|\d+(?:P|POLE)|\d+(?:X\d+)+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalSpecToken();

    private sealed record BrandResolution(string Name, bool IsNew);
}

public sealed record ProductInvoiceMatchResult(IReadOnlyList<IReadOnlyDictionary<string, object?>> Items);
