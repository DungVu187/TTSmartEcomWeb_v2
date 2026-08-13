using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Application.Integrations;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Integrations;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class CustomerOrderNotificationDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_FiltersTelegramRecipientsAndIsolatesChannelFailures()
    {
        FakeEmail email = new() { Reject = true };
        FakeSettings settings = new(new TelegramSettings(true,
        [
            Recipient("eligible", true, ["new_order"]),
            Recipient("disabled", false, ["new_order"]),
            Recipient("wrong-type", true, ["order_updated"]),
        ]));
        FakeTelegram telegram = new() { Reject = true };
        FakeZalo zalo = new() { Reject = true };
        ListLogger logger = new();
        CustomerOrderNotificationDispatcher dispatcher = new(email, settings, telegram, zalo, logger);

        await dispatcher.DispatchAsync(Notification(), CancellationToken.None);

        Assert.Equal(1, email.Calls);
        Assert.Equal(["eligible"], telegram.ChatIds);
        Assert.Equal(1, zalo.Calls);
        Assert.Equal(3, logger.Entries.Count);
        Assert.All(logger.Entries, static entry =>
        {
            Assert.Equal(4911, entry.EventId.Id);
            Assert.Contains(nameof(InvalidOperationException), entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic", entry.Message, StringComparison.Ordinal);
        });
        Assert.Contains(logger.Entries, static entry => entry.Message.Contains("email", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, static entry => entry.Message.Contains("zalo", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, static entry => entry.Message.Contains("telegram-recipient", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DispatchAsync_WhenTelegramDisabled_ShouldNotCallSender()
    {
        FakeTelegram telegram = new();
        CustomerOrderNotificationDispatcher dispatcher = new(
            new FakeEmail(),
            new FakeSettings(new TelegramSettings(false, [Recipient("eligible", true, ["new_order"])])),
            telegram,
            new FakeZalo(),
            NullLogger<CustomerOrderNotificationDispatcher>.Instance);

        await dispatcher.DispatchAsync(Notification(), CancellationToken.None);

        Assert.Empty(telegram.ChatIds);
    }

    [Fact]
    public async Task DispatchAsync_ShouldEscapeDynamicTelegramHtml()
    {
        FakeTelegram telegram = new();
        CustomerOrderNotificationDispatcher dispatcher = new(
            new FakeEmail(),
            new FakeSettings(new TelegramSettings(true, [Recipient("eligible", true, ["new_order"])])),
            telegram,
            new FakeZalo(),
            NullLogger<CustomerOrderNotificationDispatcher>.Instance);
        CustomerOrderNotification notification = Notification() with
        {
            UserName = "<script>alert('x')</script>",
            StationNames = "A&B",
        };

        await dispatcher.DispatchAsync(notification, CancellationToken.None);

        string message = Assert.Single(telegram.Messages);
        Assert.DoesNotContain("<script>", message, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;", message, StringComparison.Ordinal);
        Assert.Contains("A&amp;B", message, StringComparison.Ordinal);
    }

    private static CustomerOrderNotification Notification() => new(
        "TTS-01", "0900000000", "Khách hàng", 251000,
        new DateTimeOffset(2026, 8, 13, 1, 2, 0, TimeSpan.Zero),
        "Trạm A", "STA");

    private static TelegramRecipient Recipient(
        string id,
        bool enabled,
        IReadOnlyList<string> notifyTypes) =>
        new(id, id, id, "personal", enabled, notifyTypes);

    private sealed class FakeEmail : ICustomerOrderEmailSender
    {
        public bool Reject { get; init; }
        public int Calls { get; private set; }
        public Task<bool> SendAsync(CustomerOrderNotification notification, CancellationToken cancellationToken)
        {
            Calls++;
            return Reject ? throw new InvalidOperationException("synthetic email failure") : Task.FromResult(true);
        }
    }

    private sealed class FakeTelegram : ITelegramMessageSender
    {
        public bool Reject { get; init; }
        public List<string> ChatIds { get; } = [];
        public List<string> Messages { get; } = [];
        public Task<bool> SendAsync(string chatId, string message, CancellationToken cancellationToken)
        {
            ChatIds.Add(chatId);
            Messages.Add(message);
            return Reject ? throw new InvalidOperationException("synthetic telegram failure") : Task.FromResult(true);
        }
    }

    private sealed class FakeZalo : IZaloOrderMessageSender
    {
        public bool Reject { get; init; }
        public int Calls { get; private set; }
        public Task<bool> SendAsync(string message, CancellationToken cancellationToken)
        {
            Calls++;
            return Reject ? throw new InvalidOperationException("synthetic zalo failure") : Task.FromResult(true);
        }
    }

    private sealed class FakeSettings(TelegramSettings telegram) : IProviderSettingsRepository
    {
        public Task<TelegramSettings> GetTelegramAsync(CancellationToken cancellationToken) => Task.FromResult(telegram);
        public Task<TelegramSettings> SetTelegramEnabledAsync(bool enabled, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TelegramRecipient> AddTelegramRecipientAsync(TelegramRecipientInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TelegramRecipient?> UpdateTelegramRecipientAsync(string id, TelegramRecipientInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteTelegramRecipientAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ZaloSettings> GetZaloAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ZaloSettings> UpdateZaloAsync(ZaloSettingsInput input, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetZaloSecretKeyAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveZaloOAuthTokensAsync(string accessToken, string? refreshToken, DateTimeOffset expiresAt, string? oaId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ListLogger : ILogger<CustomerOrderNotificationDispatcher>
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
