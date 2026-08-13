using System.Buffers;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.Api.Integrations;

public sealed partial class ZaloOrderMessageSender(
    IZaloOrderCredentialRepository credentials,
    IHttpClientFactory httpClientFactory,
    IOptions<ZaloOAuthOptions> options,
    TimeProvider clock,
    ILogger<ZaloOrderMessageSender> logger) : IZaloOrderMessageSender
{
    private const string MessageEndpoint = "https://openapi.zalo.me/v2.0/oa/message/cs";
    private const int DefaultExpiresInSeconds = 86_400;
    private const int MaxExpiresInSeconds = 31_536_000;
    private const int MaximumMessageLength = 4_096;
    private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(15);

    public async Task<bool> SendAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > MaximumMessageLength) return false;
        ZaloOrderDeliveryCredentials? configuration = await credentials.FindAsync(cancellationToken);
        if (!Usable(configuration))
        {
            LogUnavailable(logger);
            return false;
        }

        string? accessToken = await GetAccessTokenAsync(configuration!, cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken)) return false;

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, MessageEndpoint)
            {
                Content = JsonContent.Create(new
                {
                    recipient = new { user_id = configuration!.RecipientUserId },
                    message = new { text = message },
                }),
            };
            request.Headers.TryAddWithoutValidation("access_token", accessToken);
            using HttpResponseMessage response = await httpClientFactory.CreateClient("zalo")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            JsonDocument? payload = await ReadJsonAsync(
                response.Content,
                options.Value.MaxProviderResponseBytes,
                cancellationToken);
            using (payload)
            {
                bool success = response.IsSuccessStatusCode &&
                    payload is not null &&
                    payload.RootElement.TryGetProperty("error", out JsonElement error) &&
                    error.ValueKind == JsonValueKind.Number &&
                    error.TryGetInt32(out int errorCode) &&
                    errorCode == 0;
                if (!success) LogSendFailure(logger, (int)response.StatusCode);
                return success;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimeout(logger, "send");
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            LogTransportFailure(logger, "send", exception.GetType().Name);
            return false;
        }
    }

    private async Task<string?> GetAccessTokenAsync(
        ZaloOrderDeliveryCredentials configuration,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (configuration.ExpiresAt is DateTimeOffset expiresAt &&
            expiresAt - now >= RefreshWindow)
        {
            return configuration.AccessToken;
        }

        RefreshResult? refresh = await RefreshAsync(configuration, cancellationToken);
        if (refresh is null) return null;
        bool saved = await credentials.TryUpdateTokensAsync(
            configuration.ConfigurationId,
            configuration.Version,
            refresh.AccessToken,
            refresh.RefreshToken,
            refresh.ExpiresAt,
            cancellationToken);
        if (saved) return refresh.AccessToken;

        // Một request khác có thể đã refresh trước. Chỉ dùng token vừa đọc lại
        // khi credential vẫn đầy đủ và còn ngoài cửa sổ refresh.
        ZaloOrderDeliveryCredentials? winner = await credentials.FindAsync(cancellationToken);
        return Usable(winner) &&
            winner!.ExpiresAt is DateTimeOffset winnerExpiry &&
            winnerExpiry - clock.GetUtcNow() >= RefreshWindow
                ? winner.AccessToken
                : null;
    }

    private async Task<RefreshResult?> RefreshAsync(
        ZaloOrderDeliveryCredentials configuration,
        CancellationToken cancellationToken)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post, options.Value.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["refresh_token"] = configuration.RefreshToken,
                    ["app_id"] = configuration.AppId,
                    ["grant_type"] = "refresh_token",
                }),
            };
            request.Headers.TryAddWithoutValidation("secret_key", configuration.SecretKey);
            using HttpResponseMessage response = await httpClientFactory.CreateClient("zalo")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            JsonDocument? payload = await ReadJsonAsync(
                response.Content,
                options.Value.MaxProviderResponseBytes,
                cancellationToken);
            using (payload)
            {
                if (!response.IsSuccessStatusCode || payload is null ||
                    HasProviderError(payload.RootElement))
                {
                    LogRefreshFailure(logger, (int)response.StatusCode);
                    return null;
                }

                JsonElement root = payload.RootElement;
                string? accessToken = ReadString(root, "access_token");
                string refreshToken = ReadString(root, "refresh_token") ?? configuration.RefreshToken;
                if (!ValidCredential(accessToken, 8_192) || !ValidCredential(refreshToken, 8_192))
                {
                    LogInvalidResponse(logger, "refresh");
                    return null;
                }

                int expiresIn = ReadExpiresIn(root);
                if (expiresIn is < 1 or > MaxExpiresInSeconds)
                {
                    LogInvalidResponse(logger, "refresh");
                    return null;
                }
                return new RefreshResult(
                    accessToken!,
                    refreshToken,
                    clock.GetUtcNow().AddSeconds(expiresIn));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogTimeout(logger, "refresh");
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            LogTransportFailure(logger, "refresh", exception.GetType().Name);
            return null;
        }
    }

    private static async Task<JsonDocument?> ReadJsonAsync(
        HttpContent content,
        int configuredMaximum,
        CancellationToken cancellationToken)
    {
        int maximum = Math.Clamp(configuredMaximum, 1, 65_536);
        if (content.Headers.ContentLength is long length && length > maximum) return null;
        await using Stream stream = await content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        byte[] chunk = ArrayPool<byte>.Shared.Rent(Math.Min(maximum + 1, 8_192));
        try
        {
            int total = 0;
            while (true)
            {
                int read = await stream.ReadAsync(
                    chunk.AsMemory(0, Math.Min(chunk.Length, maximum + 1 - total)),
                    cancellationToken);
                if (read == 0) break;
                total = checked(total + read);
                if (total > maximum) return null;
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk, clearArray: true);
        }
        if (buffer.Length == 0) return null;
        buffer.Position = 0;
        return await JsonDocument.ParseAsync(buffer, cancellationToken: cancellationToken);
    }

    private static bool Usable(ZaloOrderDeliveryCredentials? value) =>
        value is not null &&
        ValidCredential(value.AppId, 100) &&
        ValidCredential(value.SecretKey, 8_192) &&
        ValidCredential(value.RecipientUserId, 256) &&
        ValidCredential(value.AccessToken, 8_192) &&
        ValidCredential(value.RefreshToken, 8_192);

    private static bool ValidCredential(string? value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;

    private static string? ReadString(JsonElement root, string property) =>
        root.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool HasProviderError(JsonElement root)
    {
        if (!root.TryGetProperty("error", out JsonElement value)) return false;
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.False) return false;
        return value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int errorCode) ||
            errorCode != 0;
    }

    private static int ReadExpiresIn(JsonElement root)
    {
        if (!root.TryGetProperty("expires_in", out JsonElement value)) return DefaultExpiresInSeconds;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric)) return numeric;
        return value.ValueKind == JsonValueKind.String &&
            int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
    }

    [LoggerMessage(EventId = 4931, Level = LogLevel.Warning, Message = "Zalo order notification provider is not configured")]
    private static partial void LogUnavailable(ILogger logger);

    [LoggerMessage(EventId = 4932, Level = LogLevel.Warning, Message = "Zalo order notification {Operation} timed out")]
    private static partial void LogTimeout(ILogger logger, string operation);

    [LoggerMessage(EventId = 4933, Level = LogLevel.Warning, Message = "Zalo order notification {Operation} failed with {ErrorType}")]
    private static partial void LogTransportFailure(ILogger logger, string operation, string errorType);

    [LoggerMessage(EventId = 4934, Level = LogLevel.Warning, Message = "Zalo order notification refresh returned HTTP {StatusCode}")]
    private static partial void LogRefreshFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 4935, Level = LogLevel.Warning, Message = "Zalo order notification send returned HTTP {StatusCode}")]
    private static partial void LogSendFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 4936, Level = LogLevel.Warning, Message = "Zalo order notification {Operation} returned an invalid response")]
    private static partial void LogInvalidResponse(ILogger logger, string operation);

    private sealed record RefreshResult(
        string AccessToken,
        string RefreshToken,
        DateTimeOffset ExpiresAt);
}
