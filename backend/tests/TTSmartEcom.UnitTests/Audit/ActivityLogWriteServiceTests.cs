using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using TTSmartEcom.Application.Audit;

namespace TTSmartEcom.UnitTests.Audit;

public sealed class ActivityLogWriteServiceTests
{
    private static readonly ActivityLogWriteEntry Entry = new(
        "Quản trị viên",
        "create_product",
        "507f191e810c19729de860ea",
        "Sản phẩm kiểm thử",
        [new ActivityLogWriteDetail("Tạo mới", "", "Sản phẩm kiểm thử")]);

    [Fact]
    public async Task TryAppendAsync_WhenWriterSucceeds_ReturnsTrueAndForwardsEntry()
    {
        FakeWriter writer = new();
        ActivityLogWriteService service = CreateService(writer);

        bool result = await service.TryAppendAsync(Entry, CancellationToken.None);

        Assert.True(result);
        Assert.Same(Entry, Assert.Single(writer.Entries));
    }

    [Fact]
    public async Task TryAppendAsync_WhenWriterFails_ReturnsFalseWithoutEscaping()
    {
        FakeWriter writer = new() { RejectWrites = true };
        ActivityLogWriteService service = CreateService(writer);

        bool result = await service.TryAppendAsync(Entry, CancellationToken.None);

        Assert.False(result);
        Assert.Same(Entry, Assert.Single(writer.Attempts));
    }

    [Fact]
    public async Task TryAppendManyAsync_WithNoEntries_DoesNotCallWriter()
    {
        FakeWriter writer = new();
        ActivityLogWriteService service = CreateService(writer);

        bool result = await service.TryAppendManyAsync([], CancellationToken.None);

        Assert.True(result);
        Assert.Equal(0, writer.BatchAttempts);
    }

    [Fact]
    public async Task TryAppendManyAsync_WhenWriterFails_ReturnsFalseWithoutEscaping()
    {
        FakeWriter writer = new() { RejectWrites = true };
        ActivityLogWriteService service = CreateService(writer);

        bool result = await service.TryAppendManyAsync([Entry, Entry], CancellationToken.None);

        Assert.False(result);
        Assert.Equal(1, writer.BatchAttempts);
    }

    [Fact]
    public async Task TryAppendAsync_WhenWriterFails_LogsOnlyRedactedMetadata()
    {
        FakeWriter writer = new() { RejectWrites = true };
        CollectingLogger logger = new();
        ActivityLogWriteService service = new(writer, logger);

        await service.TryAppendAsync(Entry, CancellationToken.None);

        LogRecord record = Assert.Single(logger.Records);
        Assert.Equal(4691, record.EventId);
        Assert.Contains("create_product", record.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Entry.UserName, record.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Entry.ProductName!, record.Message, StringComparison.Ordinal);
    }

    private sealed class FakeWriter : IActivityLogWriter
    {
        public bool RejectWrites { get; init; }
        public int BatchAttempts { get; private set; }
        public List<ActivityLogWriteEntry> Attempts { get; } = [];
        public List<ActivityLogWriteEntry> Entries { get; } = [];

        public Task AppendAsync(ActivityLogWriteEntry entry, CancellationToken cancellationToken)
        {
            Attempts.Add(entry);
            if (RejectWrites)
            {
                throw new InvalidOperationException("Synthetic activity-log failure");
            }
            Entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task AppendManyAsync(
            IReadOnlyCollection<ActivityLogWriteEntry> entries,
            CancellationToken cancellationToken)
        {
            BatchAttempts++;
            Attempts.AddRange(entries);
            if (RejectWrites)
            {
                throw new InvalidOperationException("Synthetic activity-log failure");
            }
            Entries.AddRange(entries);
            return Task.CompletedTask;
        }
    }

    private static ActivityLogWriteService CreateService(IActivityLogWriter writer) =>
        new(writer, NullLogger<ActivityLogWriteService>.Instance);

    private sealed record LogRecord(int EventId, string Message);

    private sealed class CollectingLogger : ILogger<ActivityLogWriteService>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Records.Add(new LogRecord(eventId.Id, formatter(state, exception)));
    }
}
