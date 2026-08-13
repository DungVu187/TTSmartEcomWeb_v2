using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.Application.Integrations;

public interface IProviderSettingsRepository
{
    Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken);
    Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken);
    Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken);
    Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken);
    Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken);
    Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken);
    Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken);
    Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken);
    Task SaveZaloOAuthTokensAsync(
        string accessToken,
        string? refreshToken,
        DateTimeOffset expiresAt,
        string? oaId,
        CancellationToken cancellationToken);
}
