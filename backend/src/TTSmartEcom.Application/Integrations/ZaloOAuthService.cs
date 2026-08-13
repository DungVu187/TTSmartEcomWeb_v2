using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.Application.Integrations;

public sealed class ZaloOAuthService(
    IProviderSettingsRepository settings,
    IZaloOAuthStateService state,
    IZaloOAuthClient client,
    TimeProvider clock)
{
    private const int MaxCodeLength = 2048;
    private const int MaxStateLength = 4096;
    private const int MaxOaIdLength = 256;
    private const int DefaultExpiresInSeconds = 86_400;
    private const int MaxExpiresInSeconds = 31_536_000;

    public async Task<ZaloOAuthAuthorizationResult> CreateAuthorizationUrlAsync(
        string subject,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        ZaloSettings configuration = await settings.GetZaloAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(configuration.AppId))
        {
            return new ZaloOAuthAuthorizationResult(ZaloOAuthAuthorizationStatus.MissingAppId);
        }

        if (!state.IsAvailable || !state.TryCreate(subject, redirectUri, out string stateValue))
        {
            return new ZaloOAuthAuthorizationResult(ZaloOAuthAuthorizationStatus.StateUnavailable);
        }

        string? authorizationUrl = client.BuildAuthorizationUrl(configuration.AppId, redirectUri, stateValue);
        return string.IsNullOrWhiteSpace(authorizationUrl)
            ? new ZaloOAuthAuthorizationResult(ZaloOAuthAuthorizationStatus.StateUnavailable)
            : new ZaloOAuthAuthorizationResult(ZaloOAuthAuthorizationStatus.Success, authorizationUrl);
    }

    public async Task<ZaloOAuthCallbackResult> CompleteAsync(
        string? code,
        string? stateValue,
        string redirectUri,
        string? oaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength ||
            string.IsNullOrWhiteSpace(stateValue) || stateValue.Length > MaxStateLength ||
            oaId?.Length > MaxOaIdLength)
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.InvalidRequest);
        }

        if (!state.IsAvailable)
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.StateUnavailable);
        }

        // Tiêu thụ state trước khi gọi provider để state hợp lệ không thể được
        // sử dụng lại sau một lần callback, kể cả khi provider trả lỗi.
        if (!state.TryConsume(stateValue, redirectUri))
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.InvalidState);
        }

        ZaloSettings configuration = await settings.GetZaloAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(configuration.AppId) || !configuration.SecretKeyConfigured)
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.MissingConfiguration);
        }

        string? secretKey = await settings.GetZaloSecretKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.MissingConfiguration);
        }

        ZaloOAuthTokenExchangeResult exchange = await client.ExchangeCodeAsync(
            configuration.AppId,
            // Secret không nằm trong ZaloSettings để tránh rò rỉ qua DTO. Ranh
            // giới repository cung cấp giá trị đã được giữ ở persistence.
            secretKey,
            code,
            cancellationToken);

        return exchange.Status switch
        {
            ZaloOAuthTokenExchangeStatus.NotConfigured => new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.MissingConfiguration),
            ZaloOAuthTokenExchangeStatus.ProviderRejected => new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.ProviderRejected),
            ZaloOAuthTokenExchangeStatus.Timeout or ZaloOAuthTokenExchangeStatus.TransportFailure => new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.ProviderUnavailable),
            ZaloOAuthTokenExchangeStatus.InvalidResponse => new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.InvalidProviderResponse),
            ZaloOAuthTokenExchangeStatus.Success => await SaveTokensAsync(exchange, oaId, cancellationToken),
            _ => new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.ProviderUnavailable),
        };
    }

    private async Task<ZaloOAuthCallbackResult> SaveTokensAsync(
        ZaloOAuthTokenExchangeResult exchange,
        string? oaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(exchange.AccessToken))
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.InvalidProviderResponse);
        }

        int expiresIn = exchange.ExpiresInSeconds <= 0 ? DefaultExpiresInSeconds : exchange.ExpiresInSeconds;
        if (expiresIn > MaxExpiresInSeconds)
        {
            return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.InvalidProviderResponse);
        }

        DateTimeOffset expiresAt = clock.GetUtcNow().AddSeconds(expiresIn);
        string? normalizedOaId = string.IsNullOrWhiteSpace(oaId) ? null : oaId.Trim();
        await settings.SaveZaloOAuthTokensAsync(
            exchange.AccessToken,
            exchange.RefreshToken,
            expiresAt,
            normalizedOaId,
            cancellationToken);
        return new ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus.Success);
    }
}
