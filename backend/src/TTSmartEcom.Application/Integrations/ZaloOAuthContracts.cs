namespace TTSmartEcom.Application.Integrations;

/// <summary>
/// Kết quả trao đổi mã OAuth với Zalo. Token chỉ được giữ trong ranh giới
/// application/infrastructure và không được đưa vào response HTTP hoặc log.
/// </summary>
public sealed record ZaloOAuthTokenExchangeResult(
    ZaloOAuthTokenExchangeStatus Status,
    string? AccessToken = null,
    string? RefreshToken = null,
    int ExpiresInSeconds = 0);

public enum ZaloOAuthTokenExchangeStatus
{
    Success,
    NotConfigured,
    ProviderRejected,
    InvalidResponse,
    Timeout,
    TransportFailure,
}

public sealed record ZaloOAuthAuthorizationResult(
    ZaloOAuthAuthorizationStatus Status,
    string? AuthorizationUrl = null);

public enum ZaloOAuthAuthorizationStatus
{
    Success,
    MissingAppId,
    StateUnavailable,
}

public sealed record ZaloOAuthCallbackResult(ZaloOAuthCallbackStatus Status);

public enum ZaloOAuthCallbackStatus
{
    Success,
    InvalidRequest,
    InvalidState,
    StateUnavailable,
    MissingConfiguration,
    ProviderRejected,
    ProviderUnavailable,
    InvalidProviderResponse,
}

public interface IZaloOAuthStateService
{
    bool IsAvailable { get; }

    bool TryCreate(string subject, string redirectUri, out string state);

    bool TryConsume(string state, string redirectUri);
}

public interface IZaloOAuthClient
{
    string? BuildAuthorizationUrl(string appId, string redirectUri, string state);

    Task<ZaloOAuthTokenExchangeResult> ExchangeCodeAsync(
        string appId,
        string secretKey,
        string code,
        CancellationToken cancellationToken);
}
