using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Configuration;

public sealed class ZaloOAuthOptions
{
    public const string SectionName = "ZaloOAuth";

    /// <summary>
    /// Khóa ký state. Không đặt giá trị mặc định; khi thiếu hoặc quá ngắn,
    /// OAuth bị vô hiệu hóa có chủ đích.
    /// </summary>
    public string? StateSecret { get; init; }

    [Range(60, 900)]
    public int StateLifetimeSeconds { get; init; } = 300;

    [Range(1, 65_536)]
    public int MaxProviderResponseBytes { get; init; } = 65_536;

    [Range(16, 4_096)]
    public int MaxPendingStates { get; init; } = 2_048;

    public string AuthorizationEndpoint { get; init; } = "https://oauth.zalo.me/v4/oa/permission";

    public string TokenEndpoint { get; init; } = "https://oauth.zalo.me/v4/oa/access_token";

    public bool IsUsable => !string.IsNullOrWhiteSpace(StateSecret) &&
        System.Text.Encoding.UTF8.GetByteCount(StateSecret) >= 32 &&
        Uri.TryCreate(AuthorizationEndpoint, UriKind.Absolute, out Uri? authorization) &&
        authorization.Scheme == "https" &&
        Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out Uri? token) &&
        token.Scheme == "https";
}
