using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Integrations;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class TelegramMessageSenderTests
{
    [Fact]
    public async Task SendAsync_WhenTransportFails_ShouldNotLogTokenChatIdOrMessage()
    {
        const string token = "synthetic-sensitive-bot-token";
        const string chatId = "synthetic-sensitive-chat-id";
        const string message = "Synthetic Customer 0900000000";
        ListLogger logger = new();
        TelegramMessageSender sender = Create(
            token,
            new ThrowingHandler(new HttpRequestException($"{token} {chatId} {message}")),
            logger);

        bool sent = await sender.SendAsync(chatId, message, CancellationToken.None);

        Assert.False(sent);
        string logs = string.Join('\n', logger.Entries.Select(static entry => entry.Message));
        Assert.DoesNotContain(token, logs, StringComparison.Ordinal);
        Assert.DoesNotContain(chatId, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("0900000000", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthetic Customer", logs, StringComparison.Ordinal);
        Assert.Contains(nameof(HttpRequestException), logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_WhenApplicationStops_ShouldPropagateCancellation()
    {
        using CancellationTokenSource stopping = new();
        await stopping.CancelAsync();
        TelegramMessageSender sender = Create(
            "synthetic-token",
            new ThrowingHandler(new OperationCanceledException(stopping.Token)),
            new ListLogger());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            sender.SendAsync("synthetic-chat", "Synthetic message", stopping.Token));
    }

    private static TelegramMessageSender Create(
        string token,
        HttpMessageHandler handler,
        ILogger<TelegramMessageSender> logger) => new(
        new FakeHttpClientFactory(new HttpClient(handler)),
        Options.Create(new ExternalServicesOptions { TelegramBotToken = token }),
        logger);

    private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class ListLogger : ILogger<TelegramMessageSender>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
