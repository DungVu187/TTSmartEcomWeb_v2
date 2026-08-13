using TTSmartEcom.Domain.Voice;

namespace TTSmartEcom.Application.Audit;

/// <summary>
/// Xây dựng allowlist ActivityLog cho cấu hình provider và từ vựng voice.
/// Secret, token và Telegram chat ID không được type này nhận hoặc ghi lại.
/// </summary>
public static class IntegrationActivityLogEntries
{
    private const int MaximumAuditTextLength = 500;

    public static ActivityLogWriteEntry UpdateZaloSettings(string actor, string? appId) =>
        Entry(
            actor,
            "update_zalo_settings",
            "Cấu hình Zalo OA",
            "Zalo Config",
            $"Cập nhật các thông số Zalo OA (AppID: {Text(appId)})");

    public static ActivityLogWriteEntry UpdateTelegramSettings(string actor, bool enabled) =>
        TelegramEntry(
            actor,
            "update_telegram_settings",
            $"Đã {(enabled ? "bật" : "tắt")} thông báo Telegram");

    public static ActivityLogWriteEntry CreateTelegramRecipient(
        string actor,
        string? recipientLabel) =>
        TelegramEntry(
            actor,
            "create_telegram_recipient",
            $"Đã thêm người/nhóm nhận {RecipientLabel(recipientLabel)}");

    public static ActivityLogWriteEntry UpdateTelegramRecipient(
        string actor,
        string? recipientLabel) =>
        TelegramEntry(
            actor,
            "update_telegram_recipient",
            $"Đã cập nhật người/nhóm nhận {RecipientLabel(recipientLabel)}");

    public static ActivityLogWriteEntry DeleteTelegramRecipient(
        string actor,
        string? recipientLabel) =>
        TelegramEntry(
            actor,
            "delete_telegram_recipient",
            $"Đã xóa người/nhóm nhận {RecipientLabel(recipientLabel)}");

    public static ActivityLogWriteEntry CreateVoiceVocabulary(
        string actor,
        string group,
        VoiceVocabularyMutation mutation) =>
        VoiceEntry(actor, "create_voice_vocab", group, CreateVoiceDetail(group, mutation));

    public static ActivityLogWriteEntry UpdateVoiceVocabulary(
        string actor,
        string group,
        VoiceVocabularyMutation mutation) =>
        VoiceEntry(actor, "update_voice_vocab", group, UpdateVoiceDetail(group, mutation));

    public static ActivityLogWriteEntry DeleteVoiceVocabulary(
        string actor,
        string group,
        VoiceVocabularyMutation mutation) =>
        VoiceEntry(actor, "delete_voice_vocab", group, $"Xóa \"{VoiceKey(group, mutation)}\"");

    private static ActivityLogWriteEntry TelegramEntry(
        string actor,
        string action,
        string detail) =>
        Entry(actor, action, "Cấu hình Telegram", "Telegram", detail);

    private static ActivityLogWriteEntry VoiceEntry(
        string actor,
        string action,
        string group,
        string detail) =>
        Entry(actor, action, $"Từ vựng voice: {Text(group)}", Text(group), detail);

    private static ActivityLogWriteEntry Entry(
        string actor,
        string action,
        string productName,
        string field,
        string detail) =>
        new(
            Text(actor),
            action,
            null,
            Text(productName),
            [new ActivityLogWriteDetail(Text(field), "", Text(detail))]);

    private static string CreateVoiceDetail(string group, VoiceVocabularyMutation mutation) => group switch
    {
        "stopwords" or "brands" or "types" => $"Thêm \"{Text(mutation.Value)}\"",
        "brandAliases" => AliasDetail("Thêm alias cho", mutation.Name, mutation.Aliases),
        "typeAliases" => AliasDetail("Thêm alias cho", mutation.Type, mutation.Aliases),
        "intentAliases" => AliasDetail("Thêm alias cho", mutation.Intent, mutation.Aliases),
        "codeMap" => $"Thêm mã \"{Text(mutation.Code)}\"",
        _ => "Thêm mục từ vựng",
    };

    private static string UpdateVoiceDetail(string group, VoiceVocabularyMutation mutation) => group switch
    {
        "stopwords" or "brands" or "types" =>
            $"Sửa \"{Text(mutation.OldValue)}\" -> \"{Text(mutation.NewValue)}\"",
        "brandAliases" => AliasDetail("Sửa alias", mutation.Name, mutation.Aliases),
        "typeAliases" => AliasDetail("Sửa alias", mutation.Type, mutation.Aliases),
        "intentAliases" => AliasDetail("Sửa alias", mutation.Intent, mutation.Aliases),
        "codeMap" => $"Sửa mã \"{Text(mutation.Code)}\"",
        _ => "Cập nhật mục từ vựng",
    };

    private static string VoiceKey(string group, VoiceVocabularyMutation mutation) => group switch
    {
        "stopwords" or "brands" or "types" => Text(mutation.Value),
        "brandAliases" => Text(mutation.Name ?? mutation.Value),
        "typeAliases" => Text(mutation.Type ?? mutation.Value),
        "intentAliases" => Text(mutation.Intent ?? mutation.Value),
        "codeMap" => Text(mutation.Code ?? mutation.Value),
        _ => string.Empty,
    };

    private static string AliasDetail(
        string operation,
        string? key,
        IReadOnlyList<string>? aliases) =>
        $"{operation} \"{Text(key)}\": {Text(string.Join('/', NormalizeAliases(aliases)))}";

    private static IEnumerable<string> NormalizeAliases(IReadOnlyList<string>? aliases) =>
        (aliases ?? [])
            .Select(static alias => alias.Trim())
            .Where(static alias => alias.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string RecipientLabel(string? label) =>
        string.IsNullOrWhiteSpace(label) ? "(không có nhãn)" : Text(label);

    private static string Text(string? value)
    {
        string normalized = (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized[..Math.Min(normalized.Length, MaximumAuditTextLength)];
    }
}
