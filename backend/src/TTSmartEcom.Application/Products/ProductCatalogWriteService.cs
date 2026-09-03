using System.Globalization;
using System.Text;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Products;

public sealed class ProductCatalogWriteService(
    IProductCatalogWriteRepository repository,
    IProductCatalogRepository reads,
    ActivityLogWriteService activityLogs,
    ProductAccessScopeService accessScope,
    ProductBranchDistributionService distribution)
{
    private static readonly HashSet<string> ValidSortIrrelevant = new(StringComparer.Ordinal);
    private const int MaxBulkItems = 200;

    public Task<ProductMutationResult> CreateAsync(ProductMutation mutation, CancellationToken cancellationToken) =>
        CreateAsync(mutation, null, cancellationToken);

    public async Task<ProductMutationResult> CreateAsync(
        ProductMutation mutation, string? actorName, CancellationToken cancellationToken)
        => await CreateAsync(mutation, actorName, null, cancellationToken);

    public async Task<ProductMutationResult> CreateAsync(
        ProductMutation mutation,
        string? actorName,
        ICurrentUserContext? currentContext,
        CancellationToken cancellationToken)
    {
        ProductMutation? normalized = NormalizeProduct(mutation, requireNameAndDefaultEarn: true);
        if (normalized is null) return Invalid("Dữ liệu sản phẩm không hợp lệ");
        ProductCreationAssignment? assignment = currentContext is null
            ? null
            : await distribution.ResolveCreationAssignmentAsync(currentContext, cancellationToken);
        try
        {
            ProductMutationResult duplicate = await RejectDuplicateCodeAsync(
                normalized.Code,
                null,
                assignment?.CompanyId,
                cancellationToken);
            if (duplicate.Status == ProductMutationStatus.Conflict) return duplicate;

            ProductMutationResult result = await repository.CreateAsync(normalized, assignment, cancellationToken);
            if (CanAudit(actorName) && result is { Status: ProductMutationStatus.Success, Product: not null })
                await activityLogs.TryAppendAsync(ActivityLogEntries.CreateProduct(actorName!, result.Product), cancellationToken);
            return result;
        }
        catch (UnauthorizedAccessException)
        {
            return new ProductMutationResult(
                ProductMutationStatus.Forbidden,
                Message: "Company database không khớp phạm vi đã xác thực.");
        }
    }

    public Task<ProductMutationResult> UpdateAsync(string id, ProductMutation mutation, CancellationToken cancellationToken) =>
        UpdateAsync(id, mutation, null, cancellationToken);

    public async Task<ProductMutationResult> UpdateAsync(
        string id, ProductMutation mutation, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id)) return Invalid("Mã sản phẩm không hợp lệ");
        ProductMutation? normalized = NormalizeProduct(mutation, requireNameAndDefaultEarn: false);
        if (normalized is null) return Invalid("Dữ liệu sản phẩm không hợp lệ");
        ProductMutationResult duplicate = await RejectDuplicateCodeAsync(normalized.Code, id, cancellationToken);
        if (duplicate.Status == ProductMutationStatus.Conflict) return duplicate;
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.UpdateAsync(id, normalized, cancellationToken);
        if (before is not null && result is { Status: ProductMutationStatus.Success, Product: not null } &&
            ActivityLogEntries.UpdateProduct(actorName!, before, result.Product) is { } entry)
            await activityLogs.TryAppendAsync(entry, cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> DeleteAsync(string id, CancellationToken cancellationToken) =>
        DeleteAsync(id, null, cancellationToken);

    public async Task<ProductMutationResult> DeleteAsync(
        string id, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id)) return Invalid("Mã sản phẩm không hợp lệ");
        ProductMutationResult result = await repository.DeleteAsync(id, cancellationToken);
        if (CanAudit(actorName) && result is { Status: ProductMutationStatus.Success, Product: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteProduct(actorName!, result.Product), cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> ToggleDisplayAsync(string id, CancellationToken cancellationToken) =>
        ToggleDisplayAsync(id, null, cancellationToken);

    public async Task<ProductMutationResult> ToggleDisplayAsync(
        string id, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id)) return Invalid("Mã sản phẩm không hợp lệ");
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.ToggleDisplayAsync(id, cancellationToken);
        if (before is not null && result is { Status: ProductMutationStatus.Success, Product: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.ToggleProductDisplay(actorName!, before, result.Product), cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> BulkDeleteAsync(IReadOnlyCollection<string>? ids, CancellationToken cancellationToken) =>
        BulkDeleteAsync(ids, null, cancellationToken);

    public async Task<ProductMutationResult> BulkDeleteAsync(
        IReadOnlyCollection<string>? ids, string? actorName, CancellationToken cancellationToken)
    {
        if (ids is null || ids.Count is 0 or > MaxBulkItems || ids.Any(id => !IsObjectId(id)))
        {
            return Invalid("Danh sách ID không hợp lệ.");
        }

        string[] distinctIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        IReadOnlyList<ProductRecord> before = CanAudit(actorName)
            ? await reads.FindByIdsAsync(distinctIds, true, cancellationToken)
            : [];
        ProductMutationResult result = await repository.DeleteManyAsync(distinctIds, cancellationToken);
        if (CanAudit(actorName) && result.Status == ProductMutationStatus.Success && before.Count > 0)
        {
            ActivityLogWriteEntry[] entries = before
                .Select(product => ActivityLogEntries.DeleteProduct(actorName!, product, bulk: true))
                .ToArray();
            await activityLogs.TryAppendManyAsync(entries, cancellationToken);
        }
        return result;
    }

    public Task<ProductMutationResult> AddVariantAsync(string id, ProductVariantMutation variant, CancellationToken cancellationToken) =>
        AddVariantAsync(id, variant, null, cancellationToken);

    public async Task<ProductMutationResult> AddVariantAsync(
        string id, ProductVariantMutation variant, string? actorName, CancellationToken cancellationToken)
    {
        ProductVariantMutation? normalized = NormalizeVariant(variant, includeInventory: true);
        if (!IsIdentifier(id) || normalized is null) return Invalid("Dữ liệu phiên bản không hợp lệ");
        ProductMutationResult result = await repository.AddVariantAsync(id, normalized, cancellationToken);
        if (CanAudit(actorName) && result is { Status: ProductMutationStatus.Success, Product: not null } &&
            result.Product.Variants.Count > 0)
        {
            ProductVariant added = result.Product.Variants[^1];
            await activityLogs.TryAppendAsync(ActivityLogEntries.AddVariant(
                actorName!, result.Product, added, result.Product.Variants.Count - 1), cancellationToken);
        }
        return result;
    }

    public Task<ProductMutationResult> UpdateVariantAsync(
        string id, int index, ProductVariantMutation variant, CancellationToken cancellationToken) =>
        UpdateVariantAsync(id, index, variant, null, cancellationToken);

    public async Task<ProductMutationResult> UpdateVariantAsync(
        string id, int index, ProductVariantMutation variant, string? actorName, CancellationToken cancellationToken)
    {
        ProductVariantMutation? normalized = NormalizeVariant(variant, includeInventory: false);
        if (!IsIdentifier(id) || index < 0 || normalized is null) return Invalid("Dữ liệu phiên bản không hợp lệ");
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.UpdateVariantAsync(id, index, normalized, cancellationToken);
        if (before is not null && index < before.Variants.Count &&
            result is { Status: ProductMutationStatus.Success, Product: not null } && index < result.Product.Variants.Count &&
            ActivityLogEntries.UpdateVariant(actorName!, result.Product, before.Variants[index], result.Product.Variants[index], index) is { } entry)
            await activityLogs.TryAppendAsync(entry, cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> DeleteVariantAsync(string id, int index, CancellationToken cancellationToken) =>
        DeleteVariantAsync(id, index, null, cancellationToken);

    public async Task<ProductMutationResult> DeleteVariantAsync(
        string id, int index, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id) || index < 0) return Invalid("Phiên bản không hợp lệ");
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.DeleteVariantAsync(id, index, cancellationToken);
        if (before is not null && index < before.Variants.Count &&
            result is { Status: ProductMutationStatus.Success, Product: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteVariant(
                actorName!, result.Product, before.Variants[index], index), cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> UpdateEarnAsync(
        string id, int index, double earn, CancellationToken cancellationToken) =>
        UpdateEarnAsync(id, index, earn, null, cancellationToken);

    public async Task<ProductMutationResult> UpdateEarnAsync(
        string id, int index, double earn, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id) || index < 0 || !double.IsFinite(earn) || earn < 0)
            return Invalid("Earn phải là một số không âm");
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.UpdateVariantEarnAsync(id, index, earn, cancellationToken);
        if (before is not null && index < before.Variants.Count &&
            result is { Status: ProductMutationStatus.Success, Product: not null } && index < result.Product.Variants.Count &&
            ActivityLogEntries.UpdateEarn(actorName!, result.Product, before.Variants[index], result.Product.Variants[index], index) is { } entry)
            await activityLogs.TryAppendAsync(entry, cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> UpdateImportPriceAsync(
        string id, int index, string? importPrice, CancellationToken cancellationToken) =>
        UpdateImportPriceAsync(id, index, importPrice, null, cancellationToken);

    public async Task<ProductMutationResult> UpdateImportPriceAsync(
        string id, int index, string? importPrice, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id) || index < 0 || ParsePrice(importPrice) is not >= 0 || string.IsNullOrWhiteSpace(importPrice))
            return Invalid("ImportPrice phải là một chuỗi số hợp lệ");
        ProductRecord? before = CanAudit(actorName) ? await reads.FindByIdAsync(id, true, cancellationToken) : null;
        ProductMutationResult result = await repository.UpdateVariantImportPriceAsync(id, index, importPrice.Trim(), cancellationToken);
        if (before is not null && index < before.Variants.Count &&
            result is { Status: ProductMutationStatus.Success, Product: not null } && index < result.Product.Variants.Count &&
            ActivityLogEntries.UpdateImportPrice(actorName!, result.Product, before.Variants[index], result.Product.Variants[index], index) is { } entry)
            await activityLogs.TryAppendAsync(entry, cancellationToken);
        return result;
    }

    public Task<ProductMutationResult> AdjustPurchaseCountAsync(
        string id, string? action, long amount, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id) || amount <= 0 || action is not ("increase" or "decrease"))
            return Task.FromResult(Invalid("Dữ liệu điều chỉnh không hợp lệ"));
        return repository.AdjustPurchaseCountAsync(id, action == "increase" ? amount : -amount, cancellationToken);
    }

    public Task<long> BackfillDisplayAsync(CancellationToken cancellationToken) => repository.BackfillDisplayAsync(cancellationToken);

    public async Task<(bool Valid, IReadOnlyList<ProductRecord> Products)> FindByCodesAsync(
        IReadOnlyCollection<string>? codes, ProductViewer? viewer, CancellationToken cancellationToken)
    {
        if (codes is null || codes.Count is 0 or > MaxBulkItems || codes.Any(code => string.IsNullOrWhiteSpace(code) || code.Length > 120))
            return (false, []);
        IReadOnlyList<ProductRecord> values = await repository.FindByCodesAsync(
            codes.Select(code => code.Trim()).Distinct(StringComparer.Ordinal).ToArray(),
            viewer?.IsPrivileged == true,
            cancellationToken);
        IReadOnlySet<string>? allowed = await accessScope.ResolveAllowedProductIdsAsync(
            viewer,
            cancellationToken);
        return (true, allowed is null
            ? values
            : values.Where(value => allowed.Contains(value.Id)).ToArray());
    }

    public Task<ProductTypeMutationResult> CreateTypeAsync(
        string? name, string? icon, CancellationToken cancellationToken) =>
        CreateTypeAsync(name, icon, null, includeIconInAudit: true, cancellationToken);

    public async Task<ProductTypeMutationResult> CreateTypeAsync(
        string? name, string? icon, string? actorName, bool includeIconInAudit, CancellationToken cancellationToken)
    {
        string normalizedName = NormalizeName(name);
        string normalizedIcon = NormalizeIcon(icon);
        if (!IsValidType(normalizedName, normalizedIcon))
            return new ProductTypeMutationResult(ProductMutationStatus.Invalid,
                Message: string.IsNullOrWhiteSpace(normalizedName)
                    ? "Vui lòng nhập tên loại sản phẩm"
                    : "Icon loại sản phẩm không hợp lệ");
        ProductTypeMutationResult result = await repository.CreateTypeAsync(normalizedName, normalizedIcon, cancellationToken);
        if (CanAudit(actorName) && result is { Status: ProductMutationStatus.Success, ProductType: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateType(
                actorName!, result.ProductType.Type, result.ProductType.Icon, includeIconInAudit), cancellationToken);
        return result;
    }

    public Task<ProductTypeMutationResult> UpdateTypeAsync(
        string id, string? name, string? icon, CancellationToken cancellationToken) =>
        UpdateTypeAsync(id, name, icon, null, cancellationToken);

    public async Task<ProductTypeMutationResult> UpdateTypeAsync(
        string id, string? name, string? icon, string? actorName, CancellationToken cancellationToken)
    {
        string normalizedName = NormalizeName(name);
        string normalizedIcon = NormalizeIcon(icon);
        if (!IsIdentifier(id) || !IsValidType(normalizedName, normalizedIcon))
            return new ProductTypeMutationResult(ProductMutationStatus.Invalid, Message: "Dữ liệu loại sản phẩm không hợp lệ");
        ProductTypeRecord? before = CanAudit(actorName)
            ? (await reads.ListTypesAsync(cancellationToken)).FirstOrDefault(type =>
                string.Equals(type.Id, id, StringComparison.OrdinalIgnoreCase))
            : null;
        ProductTypeMutationResult result = await repository.UpdateTypeAsync(id, normalizedName, normalizedIcon, cancellationToken);
        if (before is not null && result is { Status: ProductMutationStatus.Success, ProductType: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.UpdateType(actorName!, before.Type, before.Icon,
                result.ProductType.Type, result.ProductType.Icon), cancellationToken);
        return result;
    }

    public Task<ProductTypeMutationResult> DeleteTypeAsync(
        string id, bool requireUnused, CancellationToken cancellationToken) =>
        DeleteTypeAsync(id, requireUnused, null, cancellationToken);

    public async Task<ProductTypeMutationResult> DeleteTypeAsync(
        string id, bool requireUnused, string? actorName, CancellationToken cancellationToken)
    {
        if (!IsIdentifier(id))
            return new ProductTypeMutationResult(ProductMutationStatus.Invalid, Message: "Mã loại sản phẩm không hợp lệ");
        ProductTypeMutationResult result = await repository.DeleteTypeAsync(id, requireUnused, cancellationToken);
        if (CanAudit(actorName) && result is { Status: ProductMutationStatus.Success, ProductType: not null })
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteType(actorName!, result.ProductType.Type), cancellationToken);
        return result;
    }

    public Task<IReadOnlyList<ProductReview>?> GetReviewsAsync(string productId, CancellationToken cancellationToken) =>
        IsObjectId(productId)
            ? repository.GetReviewsAsync(productId, cancellationToken)
            : throw new ArgumentException("Product id không hợp lệ", nameof(productId));

    public Task<ProductReviewMutationResult> CreateReviewAsync(
        string productId, string? email, string? comment, double? rating, CancellationToken cancellationToken)
    {
        string? validation = ValidateReview(productId, null, email, comment, rating, ratingRequired: true);
        return validation is null
            ? repository.CreateReviewAsync(productId, email!.Trim(), NormalizeComment(comment), rating!.Value, cancellationToken)
            : Task.FromResult(new ProductReviewMutationResult(ProductMutationStatus.Invalid, Message: validation));
    }

    public Task<ProductReviewMutationResult> UpdateReviewAsync(
        string productId, string reviewId, string? comment, double? rating,
        string? actorEmail, string? actorRole, CancellationToken cancellationToken)
    {
        string? validation = ValidateReview(productId, reviewId, actorEmail, comment, rating, ratingRequired: false);
        return validation is null
            ? repository.UpdateReviewAsync(productId, reviewId, NormalizeComment(comment), rating,
                actorEmail!.Trim(), IsModerator(actorRole), cancellationToken)
            : Task.FromResult(new ProductReviewMutationResult(ProductMutationStatus.Invalid, Message: validation));
    }

    public Task<ProductReviewMutationResult> DeleteReviewAsync(
        string productId, string reviewId, string? actorEmail, string? actorRole, CancellationToken cancellationToken)
    {
        if (!IsObjectId(productId) || !IsObjectId(reviewId))
            return Task.FromResult(new ProductReviewMutationResult(ProductMutationStatus.Invalid, Message: "Product id hoặc Review id không hợp lệ"));
        if (string.IsNullOrWhiteSpace(actorEmail))
            return Task.FromResult(new ProductReviewMutationResult(ProductMutationStatus.Invalid, Message: "Tài khoản chưa có email"));
        return repository.DeleteReviewAsync(productId, reviewId, actorEmail.Trim(), IsModerator(actorRole), cancellationToken);
    }

    public Task<ProductStockMutationResult> AdjustStockAsync(
        string productId, int variantIndex, double quantity, string? userName,
        string? orderId, string? orderName, bool isAiScan, CancellationToken cancellationToken)
    {
        if (!IsObjectId(productId))
            return Task.FromResult(new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Product id không hợp lệ"));
        if (variantIndex < 0)
            return Task.FromResult(new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Invalid variant index"));
        if (!double.IsFinite(quantity) || quantity == 0)
            return Task.FromResult(new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Quantity must be a non-zero number"));
        if (!Within(orderId, 200) || !Within(orderName, 300) || !Within(userName, 300))
            return Task.FromResult(new ProductStockMutationResult(ProductMutationStatus.Invalid, Message: "Stock metadata is too long"));
        return repository.AdjustStockAsync(productId, variantIndex,
            new ProductStockMutation(quantity, Trim(userName), Trim(orderId), Trim(orderName), isAiScan), cancellationToken);
    }

    private Task<ProductMutationResult> RejectDuplicateCodeAsync(
        string? code,
        string? excludeId,
        CancellationToken cancellationToken) =>
        RejectDuplicateCodeAsync(code, excludeId, null, cancellationToken);

    private async Task<ProductMutationResult> RejectDuplicateCodeAsync(
        string? code,
        string? excludeId,
        Guid? companyId,
        CancellationToken cancellationToken)
    {
        string normalized = NormalizeCode(code);
        if (normalized.Length == 0) return new ProductMutationResult(ProductMutationStatus.Success);
        ProductRecord? existing = await repository.FindEquivalentCodeAsync(
            normalized,
            excludeId,
            companyId,
            cancellationToken);
        return existing is null
            ? new ProductMutationResult(ProductMutationStatus.Success)
            : new ProductMutationResult(ProductMutationStatus.Conflict, Message:
                $"Mã sản phẩm \"{code}\" đã tồn tại ({existing.Name}). Vui lòng dùng mã khác.");
    }

    private static ProductMutation? NormalizeProduct(ProductMutation product, bool requireNameAndDefaultEarn)
    {
        if (product.Documents?.Count > 5 || product.Variants?.Count > 100 ||
            product.Documents?.Any(document => document.Id is not null && !IsObjectId(document.Id)) == true) return null;
        // Legacy and AI-created products may legitimately omit classification and
        // warranty metadata. A create still needs a displayable product name;
        // updates may be partial.
        if (requireNameAndDefaultEarn && string.IsNullOrWhiteSpace(product.Name)) return null;
        if (!Within(product.Name, 300) || !Within(product.Code, 120) || !Within(product.Type, 120) ||
            !Within(product.Brand, 120) || !Within(product.Section, 200) || !Within(product.Value, 200) ||
            !Within(product.Warranty, 300)) return null;
        bool defaultVariantEarn = requireNameAndDefaultEarn;
        IReadOnlyList<ProductVariantMutation>? variants = product.Variants?.Select(item => NormalizeVariant(item, true, defaultVariantEarn)).ToArray()!;
        if (variants?.Any(item => item is null) == true) return null;
        return product with
        {
            Type = Trim(product.Type), Name = Trim(product.Name), Code = Trim(product.Code), Brand = Trim(product.Brand),
            Section = Trim(product.Section), Value = Trim(product.Value), Warranty = Trim(product.Warranty), Vat = Trim(product.Vat),
            Variants = variants,
        };
    }

    private static ProductVariantMutation? NormalizeVariant(
        ProductVariantMutation variant,
        bool includeInventory,
        bool defaultEarn = true)
    {
        if (!new[] { variant.Price, variant.ImportPrice, variant.ImageUrl, variant.Color, variant.Shape,
                variant.ButtonCount, variant.Frame, variant.Note }.All(value => Within(value, 2_000))) return null;
        if (variant.Earn.HasValue && (!double.IsFinite(variant.Earn.Value) || variant.Earn.Value < 0)) return null;
        if (includeInventory && ((variant.QuantityForSale.HasValue && variant.QuantityForSale < 0) ||
                                 (variant.QuantityInStorage.HasValue && variant.QuantityInStorage < 0))) return null;
        return defaultEarn ? variant with { Earn = variant.Earn ?? 25 } : variant;
    }

    private static ProductMutationResult Invalid(string message) => new(ProductMutationStatus.Invalid, Message: message);
    private static bool CanAudit(string? actorName) => !string.IsNullOrWhiteSpace(actorName);
    private static bool IsIdentifier(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 &&
        value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
    private static bool IsObjectId(string? value) => value?.Length == 24 && value.All(Uri.IsHexDigit);
    private static bool Within(string? value, int max) => value is null || value.Length <= max;
    private static string? Trim(string? value) => value?.Trim();
    private static string NormalizeCode(string? value) => new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static string NormalizeName(string? value) => string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string NormalizeIcon(string? value) => string.IsNullOrWhiteSpace(value) ? "ri-tb-box-multiple" : value.Trim();
    private static bool IsValidType(string name, string icon) => name.Length is > 0 and <= 120 && icon.Length <= 120 &&
        (icon.StartsWith("ri-", StringComparison.Ordinal) || icon.StartsWith("fa-", StringComparison.Ordinal)) &&
        icon.All(ch => char.IsLower(ch) || char.IsDigit(ch) || ch == '-');
    private static double? ParsePrice(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string normalized = value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)
            ? number : null;
    }

    private static string? ValidateReview(
        string productId, string? reviewId, string? email, string? comment, double? rating, bool ratingRequired)
    {
        if (!IsObjectId(productId)) return "Product id không hợp lệ";
        if (reviewId is not null && !IsObjectId(reviewId)) return "Review id không hợp lệ";
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320) return "Tài khoản chưa có email";
        if (comment is not null && comment.Length > 2_000) return "Field \"comment\" is too long.";
        if ((ratingRequired && !rating.HasValue) || rating.HasValue && (!double.IsFinite(rating.Value) || rating.Value is < 1 or > 5))
            return "Field \"rating\" must be a finite number from 1 to 5.";
        return null;
    }

    private static string? NormalizeComment(string? comment) => comment?.Trim();
    private static bool IsModerator(string? role) => role is "admin" or "superadmin" or "staff";
}
