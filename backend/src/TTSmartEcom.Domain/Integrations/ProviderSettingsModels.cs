using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Integrations;

public sealed record TelegramSettings(
    bool Enabled,
    IReadOnlyList<TelegramRecipient> Recipients);

public sealed record TelegramRecipient(
    [property: JsonPropertyName("_id")] string Id,
    string Label,
    string ChatId,
    string Type,
    bool Enabled,
    IReadOnlyList<string> NotifyTypes);

public sealed record TelegramRecipientInput(
    string? Label,
    string? ChatId,
    string? Type,
    bool? Enabled,
    IReadOnlyList<string>? NotifyTypes);

public sealed record ZaloSettings(
    string AppId,
    string OaId,
    string RecipientUserId,
    bool IsLinked,
    DateTimeOffset? ExpiresAt,
    bool SecretKeyConfigured);

public sealed record ZaloSettingsInput(
    string? AppId,
    string? SecretKey,
    string? OaId,
    string? RecipientUserId);
