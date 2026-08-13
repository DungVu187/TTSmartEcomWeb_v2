using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Application.Integrations;

namespace TTSmartEcom.Api.Integrations;

public sealed partial class TelegramMessageSender(
    IHttpClientFactory httpClientFactory,
    IOptions<ExternalServicesOptions> options,
    ILogger<TelegramMessageSender> logger) : ITelegramMessageSender
{
    public async Task<bool> SendAsync(string chatId, string message, CancellationToken cancellationToken)
    {
        string? token = options.Value.TelegramBotToken;
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId)) return false;
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Post,
                $"https://api.telegram.org/bot{Uri.EscapeDataString(token)}/sendMessage")
            {
                Content = JsonContent.Create(new { chat_id = chatId, text = message, parse_mode = "HTML" }),
            };
            using HttpResponseMessage response = await httpClientFactory.CreateClient("telegram")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            LogProviderFailure(logger, (int)response.StatusCode);
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            LogProviderError(logger, nameof(OperationCanceledException));
            return false;
        }
        catch (HttpRequestException exception)
        {
            LogProviderError(logger, exception.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(EventId = 4701, Level = LogLevel.Warning, Message = "Telegram provider returned HTTP {StatusCode}")]
    private static partial void LogProviderFailure(ILogger logger, int statusCode);

    [LoggerMessage(EventId = 4702, Level = LogLevel.Warning, Message = "Telegram provider request failed with {ErrorType}")]
    private static partial void LogProviderError(ILogger logger, string errorType);
}
