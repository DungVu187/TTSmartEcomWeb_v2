using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.Application.Integrations;

public sealed class ProviderSettingsService(IProviderSettingsRepository repository)
{
    private static readonly HashSet<string> RecipientTypes = ["personal", "group"];
    private static readonly HashSet<string> NotificationTypes = ["new_order", "order_updated", "order_cancelled"];

    public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) => repository.GetTelegramAsync(cancellationToken);
    public Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken) => repository.SetTelegramEnabledAsync(enabled, cancellationToken);

    public Task<TelegramRecipient> AddRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken)
    {
        TelegramRecipientInput normalized = Normalize(input, false);
        return repository.AddTelegramRecipientAsync(normalized, cancellationToken);
    }

    public Task<TelegramRecipient?> UpdateRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken)
    {
        if (!MongoId(id)) throw Error(400, "Mã người nhận không hợp lệ");
        return repository.UpdateTelegramRecipientAsync(id, Normalize(input, true), cancellationToken);
    }

    public Task<bool> DeleteRecipientAsync(string id, CancellationToken cancellationToken)
    {
        if (!MongoId(id)) throw Error(400, "Mã người nhận không hợp lệ");
        return repository.DeleteTelegramRecipientAsync(id, cancellationToken);
    }

    public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) => repository.GetZaloAsync(cancellationToken);

    public Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken)
    {
        ValidateText(input.AppId, 100, "App ID");
        ValidateText(input.SecretKey, 500, "Secret Key");
        ValidateText(input.OaId, 100, "OA ID");
        ValidateText(input.RecipientUserId, 100, "Recipient User ID");
        return repository.UpdateZaloAsync(new ZaloSettingsInput(input.AppId?.Trim(), input.SecretKey?.Trim(), input.OaId?.Trim(), input.RecipientUserId?.Trim()), cancellationToken);
    }

    private static TelegramRecipientInput Normalize(TelegramRecipientInput input, bool update)
    {
        string? chatId = input.ChatId?.Trim();
        if ((!update || input.ChatId is not null) && string.IsNullOrWhiteSpace(chatId)) throw Error(400, "Chat ID không được để trống");
        ValidateText(chatId, 160, "Chat ID");
        string? label = input.Label?.Trim();
        ValidateText(label, 160, "Nhãn");
        string? type = input.Type?.Trim().ToLowerInvariant();
        if (type is not null && !RecipientTypes.Contains(type)) throw Error(400, "Loại người nhận không hợp lệ");
        IReadOnlyList<string>? notifications = input.NotifyTypes?.Select(static x => x.Trim()).Where(static x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (notifications is { Count: > 10 } || notifications?.Any(x => !NotificationTypes.Contains(x)) == true) throw Error(400, "Loại thông báo không hợp lệ");
        return new TelegramRecipientInput(label, chatId, type, input.Enabled, notifications);
    }

    private static void ValidateText(string? value, int maxLength, string field)
    {
        if (value is not null && value.Length > maxLength) throw Error(400, $"{field} vượt quá độ dài cho phép");
    }

    private static bool MongoId(string value) => value.Length == 24 && value.All(Uri.IsHexDigit);
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) => new(new ApplicationError($"TTS-INTEGRATION-{status}", 4700 + status, status, message));
}
