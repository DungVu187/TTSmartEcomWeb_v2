using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Integrations;

namespace TTSmartEcom.Api.Integrations;

/// <summary>
/// State OAuth ký HMAC, ràng buộc với redirect URI và tiêu thụ một lần.
/// State chỉ nằm trong bộ nhớ process; không dùng để thay thế session store
/// phân tán khi triển khai nhiều instance.
/// </summary>
public sealed class ZaloOAuthStateService(
    IOptions<ZaloOAuthOptions> options,
    TimeProvider clock) : IZaloOAuthStateService
{
    private const int MaxStateLength = 4_096;
    private const int MaxRedirectUriLength = 2_048;
    private readonly ConcurrentDictionary<string, PendingState> pending = new(StringComparer.Ordinal);

    public bool IsAvailable => options.Value.IsUsable;

    public bool TryCreate(string subject, string redirectUri, out string state)
    {
        state = string.Empty;
        if (!IsAvailable || string.IsNullOrWhiteSpace(subject) || subject.Length > 256 ||
            !IsSafeRedirectUri(redirectUri)) return false;

        CleanupExpired();
        if (pending.Count >= options.Value.MaxPendingStates) return false;
        byte[] nonceBytes = RandomNumberGenerator.GetBytes(32);
        string nonce = Base64Url(nonceBytes);
        long expiresAt = clock.GetUtcNow().AddSeconds(options.Value.StateLifetimeSeconds).ToUnixTimeSeconds();
        pending[nonce] = new PendingState(subject, redirectUri, expiresAt);

        string payload = $"{nonce}.{expiresAt}";
        byte[] signature = Sign(payload);
        state = $"{Base64Url(Encoding.UTF8.GetBytes(payload))}.{Base64Url(signature)}";
        return true;
    }

    public bool TryConsume(string state, string redirectUri)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(state) || state.Length > MaxStateLength ||
            !IsSafeRedirectUri(redirectUri)) return false;
        string[] parts = state.Split('.', StringSplitOptions.None);
        if (parts.Length != 2) return false;

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        string payload = Encoding.UTF8.GetString(payloadBytes);
        byte[] expected = Sign(payload);
        if (signature.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(signature, expected)) return false;

        string[] payloadParts = payload.Split('.', StringSplitOptions.None);
        if (payloadParts.Length != 2 || !long.TryParse(payloadParts[1], out long expiresAt)) return false;
        DateTimeOffset now = clock.GetUtcNow();
        if (expiresAt <= now.ToUnixTimeSeconds()) return false;

        string nonce = payloadParts[0];
        if (!pending.TryGetValue(nonce, out PendingState? stored) ||
            stored.ExpiresAt != expiresAt ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(stored.RedirectUri), Encoding.UTF8.GetBytes(redirectUri)))
        {
            return false;
        }

        // TryRemove là thao tác nguyên tử giữa các callback cạnh tranh.
        return pending.TryRemove(new KeyValuePair<string, PendingState>(nonce, stored));
    }

    private byte[] Sign(string payload) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.StateSecret!), Encoding.UTF8.GetBytes(payload));

    private void CleanupExpired()
    {
        long now = clock.GetUtcNow().ToUnixTimeSeconds();
        foreach ((string key, PendingState value) in pending)
        {
            if (value.ExpiresAt <= now) pending.TryRemove(key, out _);
        }
    }

    private static bool IsSafeRedirectUri(string value) =>
        value.Length <= MaxRedirectUriLength &&
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        uri.Scheme is "https" or "http" &&
        !string.IsNullOrWhiteSpace(uri.Host) &&
        string.IsNullOrEmpty(uri.UserInfo);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
        return Convert.FromBase64String(normalized);
    }

    private sealed record PendingState(string Subject, string RedirectUri, long ExpiresAt);
}
