namespace TTSmartEcom.Api.Contracts.Integrations;

public sealed record TelegramEnabledRequest(bool? Enabled);
public sealed record TelegramRecipientRequest(string? Label, string? ChatId, string? Type, bool? Enabled, IReadOnlyList<string>? NotifyTypes);
public sealed record TelegramTestRequest(string? ChatId);
public sealed record ZaloSettingsRequest(string? AppId, string? SecretKey, string? OaId, string? RecipientUserId);
