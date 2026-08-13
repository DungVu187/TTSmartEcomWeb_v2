namespace TTSmartEcom.Domain.Voice;

public sealed record VoiceVocabulary(
    IReadOnlyList<string> Stopwords,
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Types,
    IReadOnlyList<VoiceBrandAlias> BrandAliases,
    IReadOnlyList<VoiceTypeAlias> TypeAliases,
    IReadOnlyList<VoiceIntentAlias> IntentAliases,
    IReadOnlyList<VoiceCodeMap> CodeMap,
    int Version);

public sealed record VoiceBrandAlias(string Name, IReadOnlyList<string> Aliases);
public sealed record VoiceTypeAlias(string Type, string Keyword, IReadOnlyList<string> Aliases);
public sealed record VoiceIntentAlias(string Intent, string Label, IReadOnlyList<string> Aliases);
public sealed record VoiceCodeMap(string Code, string Keyword, string? Brand, string? Type, IReadOnlyList<string> Patterns, string Compact);

public sealed record VoiceVocabularyMutation(
    string? Value = null,
    string? OldValue = null,
    string? NewValue = null,
    string? Name = null,
    string? Type = null,
    string? Intent = null,
    string? Code = null,
    string? Keyword = null,
    string? Label = null,
    string? Brand = null,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<string>? Patterns = null,
    string? Compact = null);

/// <summary>
/// Bộ từ vựng khởi tạo tương thích với <c>be/config/voiceVocab.defaults.js</c>.
/// Mỗi lần gọi tạo một object graph mới để dữ liệu runtime không thể sửa ngược
/// nguồn mặc định dùng cho seed và fallback.
/// </summary>
public static class VoiceVocabularyDefaults
{
    public static VoiceVocabulary Create() => new(
        Stopwords:
        [
            "tim", "kiem", "timkiem", "cho", "toi", "xem", "gia", "la", "bao", "nhieu",
            "con", "hang", "khong", "co", "san", "pham", "sanpham", "giup", "minh",
            "cai", "chiec", "the", "nao", "oi", "nhe", "nha", "vay", "hoi", "kiem",
        ],
        Brands:
        [
            "Airtac", "Autonics", "Chaofan", "Delta", "Frecon", "Giga", "Goldcup",
            "Haitima", "Hanyoung", "Idec", "Keli", "Kinco", "Mitsubishi", "Nass",
            "Omron", "Parker", "STNC", "SangA", "Sangjin", "Schneider", "Selec",
            "Siemens", "Taiwan", "VEICHI",
        ],
        Types:
        [
            "Aptomat", "Biến tần", "Biến áp cách ly", "Bảo Vệ Mất, Ngược Pha",
            "Bộ lọc khí", "Contactor", "Cảm biến", "Cầu Đấu", "Dây điện", "Loadcell",
            "Lọc bụi", "Nguồn", "Nút Nhấn", "PLC", "Phụ kiện khí nén", "Relay Nhiệt",
            "Relay Thời Gian", "Relay Trung Gian", "TI", "Van khí nén", "Van điện từ",
            "Xy lanh khí nén", "Đèn", "Đồng Hồ",
        ],
        BrandAliases:
        [
            Brand("Siemens", "siemens", "simens", "xi men", "si men"),
            Brand("Mitsubishi", "mitsubishi", "mit su bi shi", "mit subishi", "mit su"),
            Brand("Omron", "omron", "om ron", "om rong"),
            Brand("VEICHI", "veichi", "ve chi", "v e i c h i"),
            Brand("Autonics", "autonics", "au tonics", "en to net"),
            Brand("Schneider", "schneider", "schnider", "s nai der"),
            Brand("Delta", "delta", "den ta"),
            Brand("Idec", "idec", "i dec"),
            Brand("Kinco", "kinco", "kin co"),
            Brand("Airtac", "airtac", "air tac"),
            Brand("Parker", "parker", "pa ker"),
            Brand("Selec", "selec", "se leck"),
            Brand("Hanyoung", "hanyoung", "han young"),
            Brand("Haitima", "haitima", "hai ti ma"),
            Brand("Frecon", "frecon", "fre con"),
            Brand("STNC", "stnc", "s t n c"),
            Brand("SangA", "sanga", "sang a"),
            Brand("Sangjin", "sangjin", "sang jin"),
            Brand("Goldcup", "goldcup", "gold cup"),
            Brand("Chaofan", "chaofan", "chao fan"),
            Brand("Giga", "giga", "gi ga"),
            Brand("Keli", "keli", "ke li"),
            Brand("Nass", "nass", "nas"),
            Brand("Taiwan", "taiwan", "dai loan"),
        ],
        TypeAliases:
        [
            Type("Aptomat", "Aptomat", "at", "at to mat", "ap to mat", "aptomat", "cau dao tu dong"),
            Type("Contactor", "Contactor", "khoi", "khoi dong tu", "cong tac to", "contactor"),
            Type("Biến tần", "biến tần", "bien tan", "inverter", "bo bien tan"),
            Type("Cảm biến", "cảm biến", "cam bien", "sensor", "thiet bi cam bien"),
            Type("Nút Nhấn", "nút nhấn", "nut nhan", "nut bam"),
            Type("Nguồn", "nguồn", "nguon", "nguon to ong", "nguon xung"),
            Type("PLC", "PLC", "plc", "bo dieu khien", "bo lap trinh"),
            Type("Relay Trung Gian", "relay trung gian", "ro le trung gian", "relay trung gian"),
            Type("Relay Thời Gian", "relay thời gian", "ro le thoi gian", "relay thoi gian", "timer"),
            Type("Relay Nhiệt", "relay nhiệt", "ro le nhiet", "relay nhiet"),
            Type("TI", "TI", "ti"),
            Type("Đèn", "đèn", "den bao", "den chi thi", "den"),
            Type("Xy lanh khí nén", "xy lanh", "xi lanh khi nen", "xy lanh khi nen", "ty ben"),
        ],
        IntentAliases:
        [
            Intent("search_product", "Tìm kiếm", "tim", "kiem", "tra", "tra cuu", "xem", "coi", "luc", "tim kiem", "search"),
            Intent("add_to_cart", "Thêm", "them", "bo sung", "cho them", "them vao", "add", "cho vao", "nap them"),
            Intent("update_item", "Sửa", "sua", "cap nhat", "chinh", "chinh sua", "doi", "thay doi", "edit", "update"),
            Intent("delete_item", "Xóa", "xoa", "bo", "loai bo", "huy", "xoa bo", "delete", "remove"),
            Intent("export_history", "Xuất Excel lịch sử", "xuat excel lich su", "xuat file excel lich su", "tai excel lich su"),
        ],
        CodeMap:
        [
            Code("FX3U", "FX3U", "Mitsubishi", "PLC", "fx3u", @"\bfx\s*3\s*u\b"),
            Code("FX5U", "FX5U", "Mitsubishi", "PLC", "fx5u", @"\bfx\s*5\s*u\b"),
            Code("S7-1200", "S7-1200", "Siemens", "PLC", "s71200", @"\bs\s*7\s*[- ]?\s*1200\b", @"\bs7\s*muoi\s*hai\s*tram\b"),
            Code("S7-1500", "S7-1500", "Siemens", "PLC", "s71500", @"\bs\s*7\s*[- ]?\s*1500\b", @"\bs7\s*muoi\s*lam\s*tram\b"),
            Code("GPC1202", "khớp nối GPC1202", null, null, "gpc1202", @"\bg\s*p\s*c\s*[- ]?\s*1202\b", @"\bgpc\s*muoi\s*hai\s*khong\s*hai\b"),
        ],
        Version: 0);

    private static VoiceBrandAlias Brand(string name, params string[] aliases) => new(name, aliases);
    private static VoiceTypeAlias Type(string type, string keyword, params string[] aliases) => new(type, keyword, aliases);
    private static VoiceIntentAlias Intent(string intent, string label, params string[] aliases) => new(intent, label, aliases);
    private static VoiceCodeMap Code(
        string code,
        string keyword,
        string? brand,
        string? type,
        string compact,
        params string[] patterns) => new(code, keyword, brand, type, patterns, compact);
}
