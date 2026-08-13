using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Integrations;

namespace TTSmartEcom.Api.Integrations;

public sealed partial class ZaloOAuthClient(
    IHttpClientFactory httpClientFactory,
    IOptions<ZaloOAuthOptions> options,
    ILogger<ZaloOAuthClient> logger) : IZaloOAuthClient
{
    private const int MaxCodeLength = 2048;

    public string? BuildAuthorizationUrl(string appId, string redirectUri, string state)
    {
        ZaloOAuthOptions value = options.Value;
        if (!value.IsUsable || string.IsNullOrWhiteSpace(appId) || appId.Length > 100 ||
            !Uri.TryCreate(redirectUri, UriKind.Absolute, out Uri? redirect) ||
            redirect.Scheme is not ("http" or "https")) return null;

        return $"{value.AuthorizationEndpoint}?app_id={Uri.EscapeDataString(appId)}&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";
    }

    public async Task<ZaloOAuthTokenExchangeResult> ExchangeCodeAsync(
        string appId,
        string secretKey,
        string code,
        CancellationToken cancellationToken)
    {
        ZaloOAuthOptions value = options.Value;
        if (!value.IsUsable || string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(code) || code.Length > MaxCodeLength)
        {
            return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.NotConfigured);
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, value.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["app_id"] = appId,
                    ["grant_type"] = "authorization_code",
                }),
            };
            request.Headers.TryAddWithoutValidation("secret_key", secretKey);
            using HttpResponseMessage response = await httpClientFactory.CreateClient("zalo")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            string body = await ReadBoundedAsync(response, value.MaxProviderResponseBytes, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                LogProviderFailure(logger, (int)response.StatusCode);
                return response.IsSuccessStatusCode
                    ? new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.InvalidResponse)
                    : new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.ProviderRejected);
            }

            using JsonDocument json = JsonDocument.Parse(body);
            JsonElement root = json.RootElement;
            if (!response.IsSuccessStatusCode || (root.TryGetProperty("error", out JsonElement error) && error.ValueKind is not JsonValueKind.Null and not JsonValueKind.False))
            {
                LogProviderFailure(logger, (int)response.StatusCode);
                return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.ProviderRejected);
            }

            if (!root.TryGetProperty("access_token", out JsonElement access) || access.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(access.GetString()))
            {
                LogProviderFailure(logger, (int)response.StatusCode);
                return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.InvalidResponse);
            }

            string? refresh = root.TryGetProperty("refresh_token", out JsonElement refreshElement) && refreshElement.ValueKind == JsonValueKind.String
                ? refreshElement.GetString()
                : null;
            int expiresIn = 0;
            if (root.TryGetProperty("expires_in", out JsonElement expiresElement))
            {
                if (expiresElement.ValueKind == JsonValueKind.Number && expiresElement.TryGetInt32(out int numeric)) expiresIn = numeric;
                else if (expiresElement.ValueKind == JsonValueKind.String && int.TryParse(expiresElement.GetString(), out int parsed)) expiresIn = parsed;
            }
            return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.Success, access.GetString(), refresh, expiresIn);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogProviderTimeout(logger);
            return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.Timeout);
        }
        catch (HttpRequestException exception)
        {
            LogProviderTransportFailure(logger, exception);
            return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.TransportFailure);
        }
        catch (JsonException exception)
        {
            LogProviderResponseFailure(logger, exception);
            return new ZaloOAuthTokenExchangeResult(ZaloOAuthTokenExchangeStatus.InvalidResponse);
        }
    }

    private static async Task<string> ReadBoundedAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is long length && length > maxBytes)
        {
            return string.Empty;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[Math.Min(maxBytes, 8192)];
        int total = 0;
        while (true)
        {
            int read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            total += read;
            if (total > maxBytes) return string.Empty;
            buffer.Write(chunk, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    [LoggerMessage(EventId = 4801, Level = LogLevel.Warning, Message = "Zalo OAuth provider returned HTTP {StatusCode}")]
    private static partial void LogProviderFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 4802, Level = LogLevel.Warning, Message = "Zalo OAuth provider timed out")]
    private static partial void LogProviderTimeout(ILogger logger);

    [LoggerMessage(EventId = 4803, Level = LogLevel.Warning, Message = "Zalo OAuth provider transport failed")]
    private static partial void LogProviderTransportFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4804, Level = LogLevel.Warning, Message = "Zalo OAuth provider response was invalid")]
    private static partial void LogProviderResponseFailure(ILogger logger, Exception exception);
}
