using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.Application.Products;

public static partial class ProductVoiceQueryService
{
    private static readonly string[] ValidIntents =
        ["search_product", "add_to_cart", "update_item", "delete_item", "export_history"];
    private static VoiceVocabularySnapshot current = VoiceVocabularySnapshot.Create(VoiceVocabularyDefaults.Create());

    internal static void RefreshVoiceVocabulary(VoiceVocabulary vocabulary) =>
        Volatile.Write(ref current, VoiceVocabularySnapshot.Create(vocabulary));

    public static VoiceQueryResult FromText(string text)
    {
        VoiceVocabularySnapshot snapshot = Volatile.Read(ref current);
        string transcript = text.Trim();
        string[] tokens = transcript.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int start = 0;
        while (start < tokens.Length && snapshot.Stopwords.Contains(Normalize(tokens[start]))) start++;
        int end = tokens.Length;
        while (end > start && snapshot.Stopwords.Contains(Normalize(tokens[end - 1]))) end--;
        string keyword = start < end ? string.Join(' ', tokens[start..end]) : transcript;
        return Normalize(snapshot, transcript, keyword, null, null, null, null, null);
    }

    public static VoiceQueryResult FromProvider(JsonElement payload)
    {
        VoiceVocabularySnapshot snapshot = Volatile.Read(ref current);
        if (payload.ValueKind != JsonValueKind.Object)
            return Normalize(snapshot, payload.ToString(), payload.ToString(), null, null, null, null, null);
        string transcript = String(payload, "transcript")?.Trim() ?? string.Empty;
        string keyword = String(payload, "keyword")?.Trim() ?? string.Empty;
        string? intent = String(payload, "intent")?.Trim();
        string? brand = null;
        string? type = null;
        string? code = null;
        if (payload.TryGetProperty("filters", out JsonElement filters) && filters.ValueKind == JsonValueKind.Object)
        {
            brand = String(filters, "brand")?.Trim();
            type = String(filters, "type")?.Trim();
            code = String(filters, "code")?.Trim();
        }
        HistoryExport? history = ParseProviderHistory(payload);
        return Normalize(snapshot, transcript, keyword, intent, brand, type, code, history);
    }

    public static string BuildAudioPrompt()
    {
        VoiceVocabularySnapshot snapshot = Volatile.Read(ref current);
        string brands = string.Join(", ", snapshot.Brands.Select(static item => $"'{item}'"));
        string types = string.Join(", ", snapshot.Types.Select(static item => $"'{item}'"));
        string brandAliases = string.Join(
            "; ",
            snapshot.BrandAliases.Select(item => $"{PromptAliases(item.Aliases)} -> {item.Brand}"));
        string typeAliases = string.Join(
            "; ",
            snapshot.TypeAliases.Select(item => $"{PromptAliases(item.Aliases)} -> {item.Type}"));
        string intentAliases = string.Join(
            "; ",
            snapshot.IntentAliases.Select(item => $"{PromptAliases(item.Aliases)} -> {item.Intent}"));
        string intents = string.Join(", ", ValidIntents.Select(static item => $"\"{item}\""));

        return $$"""
            Bạn là trợ lý ảo thông minh phụ trách quản lý kho hàng của công ty thiết bị điện/thiết bị tự động hóa TTSmart.
            Hãy nghe file âm thanh được cung cấp (giọng nói tiếng Việt của người dùng) và thực hiện 2 nhiệm vụ:
            1. Ghi lại chính xác (transcribe) những gì người dùng đã nói (giữ nguyên tiếng Việt có dấu, viết hoa các từ cần thiết như Siemens, Mitsubishi, GPC1202, S7-1200, FX3U,...).
            2. Phân tích ý định (intent) của người dùng để trích xuất ra từ khóa tìm kiếm chính (keyword) và các bộ lọc (filters) thích hợp, tối ưu hóa cho tất cả các cách gọi khác nhau của người dùng.

            CƠ SỞ DỮ LIỆU ĐANG CÓ SẴN CÁC THƯƠNG HIỆU (BRANDS) VÀ LOẠI SẢN PHẨM (TYPES) SAU:
            - Thương hiệu khả dụng: {{brands}}
            - Loại sản phẩm khả dụng: {{types}}

            BẢNG ÁNH XẠ CÁCH ĐỌC LÓNG (tự động cập nhật khi admin thêm từ mới; cách đọc đã bỏ dấu):
            - Thương hiệu: {{brandAliases}}
            - Loại sản phẩm: {{typeAliases}}
            - Ý định: {{intentAliases}}

            Quy tắc phân tách và xử lý từ khóa:
            - Intent chỉ được là một trong các giá trị: {{intents}}. Nếu người dùng chỉ hỏi/xem/tra cứu sản phẩm thì dùng "search_product". Nếu câu là lệnh thêm vào giỏ thì dùng "add_to_cart"; lệnh sửa/cập nhật thì dùng "update_item"; lệnh xóa/bỏ/hủy thì dùng "delete_item". Dù intent là thêm/sửa/xóa, vẫn phải trích xuất keyword và filters như bình thường; không tự thực hiện thao tác giỏ hàng hay đơn hàng.
            - Nếu người dùng yêu cầu xuất/tải Excel lịch sử nhập kho hoặc xuất kho thì dùng intent "export_history". Điền historyExport.direction là "import" cho lịch sử nhập và "export" cho lịch sử xuất. Nhận diện mốc thời gian thành historyExport.datePreset: "today" cho hôm nay, "yesterday" cho hôm qua, "this_week" cho tuần này, "this_month" cho tháng này, không nhắc thời gian thì "all".
            - Nếu người dùng nói khoảng ngày cụ thể theo dạng "từ ngày ... đến/tới ngày ..." thì dùng historyExport.datePreset là "custom", đồng thời trả startDate và endDate theo định dạng YYYY-MM-DD.
            - Khớp đúng Thương hiệu (filters.brand): Nếu người dùng nhắc tới tên thương hiệu, bạn PHẢI ánh xạ chính xác về một trong những thương hiệu khả dụng ở trên (Ví dụ: "siemens" -> "Siemens", "mit su bi shi" -> "Mitsubishi", "ôm ron" -> "Omron", "vê chi" -> "VEICHI", "en tơ nét" -> "Autonics"). Nếu câu nói không chứa tên thương hiệu, "filters.brand" bắt buộc phải là null (Tuyệt đối KHÔNG tự ý gán bừa thương hiệu mặc định).
            - Khớp đúng Loại sản phẩm (filters.type): Ánh xạ từ khóa về một trong các loại sản phẩm khả dụng ở danh sách trên.
              + Nếu nhắc đến: "át", "át tô mát", "áp tô mát", "aptomat", "cầu dao tự động" -> filters.type: "Aptomat", keyword: "Aptomat".
              + Nếu nhắc đến: "khởi", "khởi động từ", "công tắc tơ", "contactor" -> filters.type: "Contactor", keyword: "Contactor".
              + Nếu nhắc đến: "biến tần", "inverter", "bộ biến tần" -> filters.type: "Biến tần", keyword: "biến tần".
              + Nếu nhắc đến: "cảm biến", "sensor", "thiết bị cảm biến" -> filters.type: "Cảm biến", keyword: "cảm biến".
              + Nếu nhắc đến: "nút nhấn", "nút bấm" -> filters.type: "Nút Nhấn", keyword: "nút nhấn".
              + Nếu nhắc đến: "nguồn", "nguồn tổ ong", "nguồn xung" -> filters.type: "Nguồn", keyword: "nguồn".
              + Nếu nhắc đến: "bộ điều khiển", "bộ lập trình", "plc" -> filters.type: "PLC", keyword: "PLC".
              + Nếu nhắc đến: "rơ le trung gian", "relay trung gian" -> filters.type: "Relay Trung Gian", keyword: "relay trung gian".
              + Nếu nhắc đến: "rơ le thời gian", "relay thời gian", "timer" -> filters.type: "Relay Thời Gian", keyword: "relay thời gian".
              + Nếu nhắc đến: "rơ le nhiệt", "relay nhiệt" -> filters.type: "Relay Nhiệt", keyword: "relay nhiệt".
              + Nếu nhắc đến: "ti" -> filters.type: "TI", keyword: "TI".
              + Nếu nhắc đến: "đèn báo", "đèn chỉ thị", "đèn" -> filters.type: "Đèn", keyword: "đèn".
              + Nếu nhắc đến: "xi lanh khí nén", "ty ben" -> filters.type: "Xy lanh khí nén", keyword: "xy lanh".
            - Trường hợp Đặc biệt:
              + Màn hình / HMI: Vì trong danh mục sản phẩm của hệ thống KHÔNG có loại "HMI" (các màn hình HMI đang được xếp vào loại "PLC" hoặc loại khác), nên nếu người dùng nói "HMI", "màn hình HMI", "màn hình cảm ứng", bạn phải đặt "filters.type" là null và đặt "keyword" là "HMI" hoặc "màn hình" để tìm kiếm theo tên chuỗi văn bản.
            - Tách biệt tên thương hiệu: Nếu người dùng nhắc cả loại và hãng (ví dụ: "tìm plc siemens"), bạn PHẢI tách thương hiệu ra đưa vào "filters.brand" (ví dụ: "Siemens"), và đưa loại sản phẩm vào "keyword" (ví dụ: "PLC") đồng thời loại bỏ tên hãng khỏi "keyword" để tránh việc tìm kiếm chuỗi trong cơ sở dữ liệu bị lỗi.
            - Chỉ gán "filters.brand" tự động khi người dùng đọc mã/model thiết bị đặc thù thuộc về duy nhất một hãng (ví dụ: "S7-1200" hoặc "S7-1500" -> hãng "Siemens"; "FX3U" hoặc "FX5U" -> hãng "Mitsubishi").
            - Giữ lại thông số kỹ thuật chi tiết: Nếu câu nói chứa tên model và các thông số chi tiết (ví dụ: "SM1231 8 AI RTD", "S7-1200 1214C", "FX3U 16MR"), bạn PHẢI trích xuất mã dòng sản phẩm chính vào "filters.code" (ví dụ: "SM1231", "S7-1200", "FX3U"), nhưng đối với trường "keyword", bạn bắt buộc PHẢI giữ nguyên toàn bộ tên model kèm thông số chi tiết đó (ví dụ: "SM1231 8 AI RTD") để backend có thể đối sánh chính xác.
            - Giữ lại thuộc tính mô tả chi tiết: Nếu người dùng đọc kèm mô tả cụ thể (ví dụ: "nút đỏ", "nút đỏ không đèn", "nút nhấn màu xanh", "relay nhiệt mười tám a", "rơ le nhiệt 18A"), bạn PHẢI giữ nguyên cụm từ mô tả chi tiết đó làm "keyword" (ví dụ: "nút đỏ", "nút đỏ không đèn", "nút nhấn màu xanh", "relay nhiệt 18A"), TUYỆT ĐỐI không được rút ngắn keyword thành tên loại sản phẩm chung chung (như "nút nhấn" hoặc "relay nhiệt") vì hệ thống cần từ khóa chi tiết để lọc sản phẩm theo màu sắc/dòng điện.


            Ví dụ cụ thể:
            1. Người dùng nói: "tìm plc siemens"
            -> transcript: "tìm plc siemens", keyword: "PLC", intent: "search_product", filters: { brand: "Siemens", type: "PLC", code: null }

            2. Người dùng nói: "tìm bộ lập trình mitsubishi"
            -> transcript: "tìm bộ lập trình mitsubishi", keyword: "PLC", intent: "search_product", filters: { brand: "Mitsubishi", type: "PLC", code: null }

            3. Người dùng nói: "giá màn hình hmi delta"
            -> transcript: "giá màn hình hmi delta", keyword: "HMI", intent: "search_product", filters: { brand: "Delta", type: null, code: null }

            4. Người dùng nói: "tìm plc"
            -> transcript: "tìm plc", keyword: "PLC", intent: "search_product", filters: { brand: null, type: "PLC", code: null }

            5. Người dùng nói: "tìm cảm biến omron"
            -> transcript: "tìm cảm biến omron", keyword: "cảm biến", intent: "search_product", filters: { brand: "Omron", type: "Cảm biến", code: null }

            6. Người dùng nói: "khớp nối gpc mười hai không hai còn hàng không"
            -> transcript: "khớp nối gpc mười hai không hai còn hàng không", keyword: "khớp nối GPC1202", intent: "search_product", filters: { brand: null, type: null, code: "GPC1202" }

            7. Người dùng nói: "cho tôi xem sản phẩm của hãng siemens"
            -> transcript: "cho tôi xem sản phẩm của hãng siemens", keyword: "", intent: "search_product", filters: { brand: "Siemens", type: null, code: null }

            8. Người dùng nói: "tìm thiết bị s7 mười hai trăm"
            -> transcript: "tìm thiết bị s7 mười hai trăm", keyword: "S7-1200", intent: "search_product", filters: { brand: "Siemens", type: "PLC", code: "S7-1200" }

            9. Người dùng nói: "fx3u còn hàng không"
            -> transcript: "fx3u còn hàng không", keyword: "FX3U", intent: "search_product", filters: { brand: "Mitsubishi", type: "PLC", code: "FX3U" }

            10. Người dùng nói: "thêm biến tần omron"
            -> transcript: "thêm biến tần omron", keyword: "biến tần", intent: "add_to_cart", filters: { brand: "Omron", type: "Biến tần", code: null }

            11. Người dùng nói: "cập nhật plc siemens"
            -> transcript: "cập nhật plc siemens", keyword: "PLC", intent: "update_item", filters: { brand: "Siemens", type: "PLC", code: null }

            12. Người dùng nói: "xóa fx3u"
            -> transcript: "xóa fx3u", keyword: "FX3U", intent: "delete_item", filters: { brand: "Mitsubishi", type: "PLC", code: "FX3U" }

            13. Người dùng nói: "xuất excel lịch sử nhập đơn hôm nay"
            -> transcript: "xuất excel lịch sử nhập đơn hôm nay", keyword: "", intent: "export_history", historyExport: { direction: "import", datePreset: "today" }, filters: { brand: null, type: null, code: null }

            14. Người dùng nói: "xuất file excel lịch sử xuất kho tháng này"
            -> transcript: "xuất file excel lịch sử xuất kho tháng này", keyword: "", intent: "export_history", historyExport: { direction: "export", datePreset: "this_month" }, filters: { brand: null, type: null, code: null }

            15. Người dùng nói: "xuất excel lịch sử nhập kho từ ngày 01/08/2026 đến ngày 05/08/2026"
            -> transcript: "xuất excel lịch sử nhập kho từ ngày 01/08/2026 đến ngày 05/08/2026", keyword: "", intent: "export_history", historyExport: { direction: "import", datePreset: "custom", startDate: "2026-08-01", endDate: "2026-08-05" }, filters: { brand: null, type: null, code: null }

            Định dạng phản hồi BẮT BUỘC là một đối tượng JSON trực tiếp (không nằm trong thẻ markdown và không có văn bản giải thích đi kèm):
            {
              "transcript": "...",
              "keyword": "...",
              "intent": "search_product | add_to_cart | update_item | delete_item | export_history",
              "historyExport": {
                "direction": "import | export | null",
                "datePreset": "all | today | yesterday | this_week | this_month | custom",
                "startDate": "YYYY-MM-DD | null",
                "endDate": "YYYY-MM-DD | null"
              },
              "filters": {
                "brand": null,
                "type": null,
                "code": null
              }
            }
            """;
    }

    private static string PromptAliases(IEnumerable<string> aliases) =>
        string.Join('/', aliases.Select(static alias => $"\"{alias}\""));

    private static VoiceQueryResult Normalize(
        VoiceVocabularySnapshot snapshot,
        string transcript,
        string keyword,
        string? rawIntent,
        string? rawBrand,
        string? rawType,
        string? rawCode,
        HistoryExport? rawHistory)
    {
        string probe = string.Join(' ', new[] { transcript, keyword, rawCode }.Where(value => !string.IsNullOrWhiteSpace(value)));
        CodeMatch? codeMatch = DetectCode(snapshot, probe);
        string? detectedBrand = codeMatch?.Brand ?? DetectBrand(snapshot, transcript.Length > 0 ? transcript : keyword);
        string? brand = detectedBrand ?? (transcript.Length == 0 && snapshot.BrandSet.Contains(rawBrand ?? string.Empty) ? rawBrand : null);
        TypeMatch typeMatch = DetectType(snapshot, probe);
        string? type = IsKnownType(snapshot, rawType) ? rawType : codeMatch?.Type ?? typeMatch.Type;
        string? code = codeMatch?.Code ?? Bound(rawCode, 120)?.ToUpperInvariant();
        HistoryExport? history = DetectHistoryExport(transcript) ?? (rawIntent == "export_history" ? rawHistory : null);
        string intent = history is not null ? "export_history" : IsIntent(rawIntent) ? rawIntent! : DetectIntent(snapshot, transcript);

        string resultKeyword = codeMatch?.Keyword ?? (typeMatch.Keyword is not null && IsGenericTypeKeyword(snapshot, keyword, typeMatch) ? typeMatch.Keyword : Bound(keyword, 300) ?? typeMatch.Keyword ?? string.Empty);
        if (brand is not null) resultKeyword = RemoveBrand(snapshot, resultKeyword, brand);
        return new VoiceQueryResult(transcript[..Math.Min(transcript.Length, 1_000)], resultKeyword, intent,
            new VoiceFilters(brand, type, code), history);
    }

    private static CodeMatch? DetectCode(VoiceVocabularySnapshot snapshot, string text)
    {
        string normalized = Normalize(text);
        string compact = normalized.Replace(" ", string.Empty, StringComparison.Ordinal);
        foreach (CodeEntry entry in snapshot.CodeMap)
        {
            bool patternHit = entry.Patterns.Any(pattern => pattern.IsMatch(normalized));
            bool compactHit = entry.Compact.Length > 0 && compact.Contains(entry.Compact, StringComparison.Ordinal);
            if (patternHit || compactHit)
                return new(entry.Code, entry.Keyword, entry.Brand, entry.Type);
        }
        Match match = GenericCode().Match(text);
        if (!match.Success) return null;
        string code = string.Concat(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value).ToUpperInvariant();
        return snapshot.BrandSet.Contains(code) ? null : new(code, code, null, null);
    }

    private static string? DetectBrand(VoiceVocabularySnapshot snapshot, string text)
    {
        string normalized = Normalize(text);
        foreach (string brand in snapshot.Brands)
            if (ContainsPhrase(normalized, Normalize(brand))) return brand;
        foreach (BrandAliasEntry entry in snapshot.BrandAliases)
            if (entry.Aliases.Any(alias => ContainsPhrase(normalized, alias))) return entry.Brand;
        return null;
    }

    private static TypeMatch DetectType(VoiceVocabularySnapshot snapshot, string text)
    {
        string normalized = Normalize(text);
        if (ContainsPhrase(normalized, "hmi") || ContainsPhrase(normalized, "man hinh cam ung"))
            return new(null, ContainsPhrase(normalized, "hmi") ? "HMI" : "màn hình");
        foreach (TypeAliasEntry entry in snapshot.TypeAliases)
            if (entry.Aliases.Any(alias => ContainsPhrase(normalized, alias))) return new(entry.Type, entry.Keyword);
        return new(null, null);
    }

    private static string DetectIntent(VoiceVocabularySnapshot snapshot, string text)
    {
        string normalized = Normalize(text);
        foreach (IntentAliasEntry entry in snapshot.IntentAliases)
        {
            if (!IsIntent(entry.Intent)) continue;
            if (entry.Aliases.Any(alias => ContainsPhrase(normalized, alias))) return entry.Intent;
        }
        return "search_product";
    }

    private static HistoryExport? DetectHistoryExport(string text)
    {
        string normalized = Normalize(text);
        if (!Regex.IsMatch(normalized, @"\b(?:xuat|tai|download)\s+(?:file\s+)?excel\b", RegexOptions.CultureInvariant)) return null;
        string? direction = Regex.IsMatch(normalized, @"\bnhap(?:\s+(?:don|kho))?\b", RegexOptions.CultureInvariant) ? "import"
            : Regex.IsMatch(normalized, @"\bxuat\s+kho\b", RegexOptions.CultureInvariant) ? "export" : null;
        if (direction is null) return null;
        string preset = ContainsPhrase(normalized, "hom nay") ? "today"
            : ContainsPhrase(normalized, "hom qua") ? "yesterday"
            : ContainsPhrase(normalized, "tuan nay") ? "this_week"
            : ContainsPhrase(normalized, "thang nay") ? "this_month" : "all";
        Match range = DateRange().Match(normalized);
        if (!range.Success) return new(direction, preset, null, null);
        string? start = ToIsoDate(range.Groups[1].Value, range.Groups[2].Value, range.Groups[3].Value);
        string? end = ToIsoDate(range.Groups[4].Value, range.Groups[5].Value, range.Groups[6].Value);
        return start is not null && end is not null && string.CompareOrdinal(start, end) <= 0
            ? new HistoryExport(direction, "custom", start, end)
            : new HistoryExport(direction, "custom", null, null);
    }

    private static HistoryExport? ParseProviderHistory(JsonElement payload)
    {
        if (!payload.TryGetProperty("historyExport", out JsonElement value) || value.ValueKind != JsonValueKind.Object) return null;
        string? direction = String(value, "direction");
        string preset = String(value, "datePreset") is "today" or "yesterday" or "this_week" or "this_month" or "custom" ? String(value, "datePreset")! : "all";
        return direction is "import" or "export"
            ? new(direction, preset, Iso(String(value, "startDate")), Iso(String(value, "endDate")))
            : null;
    }

    private static string Normalize(string value)
    {
        string decomposed = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(character is 'đ' ? 'd' : character is 'Đ' ? 'D' : character);
        }
        return NonAlphanumeric().Replace(builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant(), " ").Trim();
    }

    private static bool ContainsPhrase(string normalized, string phrase) =>
        Regex.IsMatch(normalized, $@"(?:^|\s){Regex.Escape(phrase)}(?=\s|$)", RegexOptions.CultureInvariant);
    private static string RemoveBrand(VoiceVocabularySnapshot snapshot, string value, string brand)
    {
        string cleaned = value;
        IEnumerable<string> aliases = snapshot.BrandAliases
            .Where(item => string.Equals(item.Brand, brand, StringComparison.Ordinal))
            .SelectMany(item => item.Aliases)
            .Prepend(brand);
        foreach (string alias in aliases)
            cleaned = Regex.Replace(cleaned, $@"\b{Regex.Escape(alias)}\b", " ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return Regex.Replace(cleaned, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }

    private static bool IsGenericTypeKeyword(VoiceVocabularySnapshot snapshot, string keyword, TypeMatch typeMatch)
    {
        if (typeMatch.Type is null || typeMatch.Keyword is null) return false;
        string normalized = Normalize(keyword);
        TypeAliasEntry entry = snapshot.TypeAliases.First(item => item.Type == typeMatch.Type);
        string withoutBrand = normalized;
        foreach (BrandAliasEntry brandAlias in snapshot.BrandAliases)
            foreach (string alias in brandAlias.Aliases)
                withoutBrand = Regex.Replace(withoutBrand, $@"(?:^|\s){Regex.Escape(alias)}(?=\s|$)", " ", RegexOptions.CultureInvariant).Trim();
        return entry.Aliases.Any(alias => string.Equals(withoutBrand, alias, StringComparison.Ordinal))
            || string.Equals(withoutBrand, Normalize(entry.Type), StringComparison.Ordinal)
            || string.Equals(withoutBrand, Normalize(entry.Keyword), StringComparison.Ordinal);
    }
    private static string? Bound(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static bool IsIntent(string? value) => value is "search_product" or "add_to_cart" or "update_item" or "delete_item" or "export_history";
    private static bool IsKnownType(VoiceVocabularySnapshot snapshot, string? value) => value is not null && snapshot.TypeSet.Contains(value);
    private static string? String(JsonElement value, string property) => value.TryGetProperty(property, out JsonElement item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
    private static string? Iso(string? value) => value is not null && IsoDate().IsMatch(value) ? value : null;
    private static string? ToIsoDate(string day, string month, string year) => DateOnly.TryParseExact($"{day}/{month}/{year}", "d/M/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly result) ? result.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)] private static partial Regex NonAlphanumeric();
    [GeneratedRegex(@"\b[A-Za-z]{1,5}[-\s]?\d{2,5}[A-Za-z]{0,3}\b", RegexOptions.CultureInvariant)] private static partial Regex GenericCode();
    [GeneratedRegex(@"\btu(?:\s+ngay)?\s+(\d{1,2})[\s/]+(?:thang\s+)?(\d{1,2})[\s/]+(?:nam\s+)?(\d{4})\s+(?:den|toi)\s+(?:ngay\s+)?(\d{1,2})[\s/]+(?:thang\s+)?(\d{1,2})[\s/]+(?:nam\s+)?(\d{4})", RegexOptions.CultureInvariant)] private static partial Regex DateRange();
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant)] private static partial Regex IsoDate();

    private sealed record CodeMatch(string Code, string Keyword, string? Brand, string? Type);
    private sealed record TypeMatch(string? Type, string? Keyword);

    private sealed record VoiceVocabularySnapshot(
        FrozenSet<string> Stopwords,
        ImmutableArray<string> Brands,
        FrozenSet<string> BrandSet,
        FrozenSet<string> TypeSet,
        ImmutableArray<string> Types,
        ImmutableArray<BrandAliasEntry> BrandAliases,
        ImmutableArray<TypeAliasEntry> TypeAliases,
        ImmutableArray<IntentAliasEntry> IntentAliases,
        ImmutableArray<CodeEntry> CodeMap)
    {
        public static VoiceVocabularySnapshot Create(VoiceVocabulary vocabulary) => new(
            vocabulary.Stopwords.Select(Normalize).ToFrozenSet(StringComparer.Ordinal),
            vocabulary.Brands.ToImmutableArray(),
            vocabulary.Brands.ToFrozenSet(StringComparer.Ordinal),
            vocabulary.Types.ToFrozenSet(StringComparer.Ordinal),
            vocabulary.Types.ToImmutableArray(),
            vocabulary.BrandAliases.Select(item => new BrandAliasEntry(
                item.Name,
                item.Aliases.Select(Normalize).ToImmutableArray())).ToImmutableArray(),
            vocabulary.TypeAliases.Select(item => new TypeAliasEntry(
                item.Type,
                item.Keyword,
                item.Aliases.Select(Normalize).ToImmutableArray())).ToImmutableArray(),
            vocabulary.IntentAliases.Select(item => new IntentAliasEntry(
                item.Intent,
                item.Aliases.Select(Normalize).ToImmutableArray())).ToImmutableArray(),
            vocabulary.CodeMap.Select(item => new CodeEntry(
                item.Code,
                item.Keyword,
                item.Brand,
                item.Type,
                Normalize(item.Compact).Replace(" ", string.Empty, StringComparison.Ordinal),
                item.Patterns.Select(CompilePattern).Where(static item => item is not null).Cast<Regex>().ToImmutableArray())).ToImmutableArray());

        private static Regex? CompilePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || pattern.Length > 500) return null;
            try
            {
                return new Regex(
                    pattern,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                return null;
            }
        }
    }

    private sealed record BrandAliasEntry(string Brand, ImmutableArray<string> Aliases);
    private sealed record TypeAliasEntry(string Type, string Keyword, ImmutableArray<string> Aliases);
    private sealed record IntentAliasEntry(string Intent, ImmutableArray<string> Aliases);
    private sealed record CodeEntry(
        string Code,
        string Keyword,
        string? Brand,
        string? Type,
        string Compact,
        ImmutableArray<Regex> Patterns);
}

public interface IVoiceVocabularyRuntime
{
    void Refresh(VoiceVocabulary vocabulary);
}

public sealed class VoiceVocabularyRuntime : IVoiceVocabularyRuntime
{
    public void Refresh(VoiceVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ProductVoiceQueryService.RefreshVoiceVocabulary(vocabulary);
    }
}

public sealed record VoiceQueryResult(string Transcript, string Keyword, string Intent, VoiceFilters Filters, HistoryExport? HistoryExport);
public sealed record VoiceFilters(string? Brand, string? Type, string? Code);
public sealed record HistoryExport(string? Direction, string DatePreset, string? StartDate, string? EndDate);
