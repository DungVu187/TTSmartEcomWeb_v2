using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TTSmartEcom.Api.Integrations;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class CustomerOrderNotificationSchedulerTests
{
    [Fact]
    public async Task TrySchedule_ForEachNotification_ShouldCreateAndDisposeANewScope()
    {
        GatedDispatcher dispatcher = new(expectedCalls: 2);
        ScopeTrackingFactory scopes = new(dispatcher);
        using FakeHostApplicationLifetime lifetime = new();
        CustomerOrderNotificationScheduler scheduler = new(scopes, lifetime, new ListLogger());

        Assert.True(scheduler.TrySchedule(Notification("first")));
        Assert.True(scheduler.TrySchedule(Notification("second")));
        await dispatcher.WaitForStartedAsync();

        Assert.Equal(2, scopes.CreatedScopes);
        dispatcher.Release();
        await dispatcher.WaitForCompletedAsync();
        await StopAsync(scheduler);

        Assert.Equal(2, scopes.DisposedScopes);
    }

    [Fact]
    public async Task TrySchedule_WhenDispatcherFails_ShouldNotPropagateAndShouldRestoreCapacity()
    {
        ThrowingDispatcher dispatcher = new();
        ScopeTrackingFactory scopes = new(dispatcher);
        using FakeHostApplicationLifetime lifetime = new();
        ListLogger logger = new();
        CustomerOrderNotificationScheduler scheduler = new(scopes, lifetime, logger);

        Assert.True(scheduler.TrySchedule(Notification("first")));
        Assert.True(scheduler.TrySchedule(Notification("second")));
        await WaitUntilAsync(() => logger.Entries.Count(entry => entry.EventId.Id == 4943) == 2);

        Assert.Equal(2, dispatcher.Calls);
        string logs = string.Join('\n', logger.Entries.Select(static entry => entry.Message));
        Assert.Contains(nameof(InvalidOperationException), logs, StringComparison.Ordinal);
        Assert.DoesNotContain(ThrowingDispatcher.SensitiveMessage, logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TrySchedule_WhenFourNotificationsAreActive_ShouldDropFifthWithoutQueueing()
    {
        GatedDispatcher dispatcher = new(expectedCalls: 4);
        ScopeTrackingFactory scopes = new(dispatcher);
        using FakeHostApplicationLifetime lifetime = new();
        ListLogger logger = new();
        CustomerOrderNotificationScheduler scheduler = new(scopes, lifetime, logger);

        Assert.True(scheduler.TrySchedule(Notification("one")));
        Assert.True(scheduler.TrySchedule(Notification("two")));
        Assert.True(scheduler.TrySchedule(Notification("three")));
        Assert.True(scheduler.TrySchedule(Notification("four")));
        await dispatcher.WaitForStartedAsync();

        Assert.False(scheduler.TrySchedule(Notification("dropped")));
        Assert.Equal(4, dispatcher.Calls);
        Assert.Contains(logger.Entries, static entry => entry.EventId.Id == 4941);

        dispatcher.Release();
        await dispatcher.WaitForCompletedAsync();
        await StopAsync(scheduler);
    }

    [Fact]
    public async Task StopAsync_WhenApplicationStoppingIsCancelled_ShouldCancelActiveDispatch()
    {
        CancellationObservingDispatcher dispatcher = new();
        ScopeTrackingFactory scopes = new(dispatcher);
        using FakeHostApplicationLifetime lifetime = new();
        ListLogger logger = new();
        CustomerOrderNotificationScheduler scheduler = new(scopes, lifetime, logger);

        Assert.True(scheduler.TrySchedule(Notification("stopping")));
        await dispatcher.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        lifetime.StopApplication();
        await dispatcher.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await StopAsync(scheduler);

        Assert.Contains(logger.Entries, static entry => entry.EventId.Id == 4942);
        Assert.Equal(1, scopes.DisposedScopes);
    }

    [Fact]
    public async Task StopAsync_WhenShutdownBudgetExpires_ShouldReturnAndLogWithoutOrderData()
    {
        GatedDispatcher dispatcher = new(expectedCalls: 1);
        ScopeTrackingFactory scopes = new(dispatcher);
        using FakeHostApplicationLifetime lifetime = new();
        ListLogger logger = new();
        CustomerOrderNotificationScheduler scheduler = new(scopes, lifetime, logger);
        const string sensitiveOrderId = "synthetic-sensitive-order-id";

        Assert.True(scheduler.TrySchedule(Notification(sensitiveOrderId)));
        await dispatcher.WaitForStartedAsync();
        using CancellationTokenSource expiredBudget = new();
        await expiredBudget.CancelAsync();

        await scheduler.StopAsync(expiredBudget.Token);

        LogEntry entry = Assert.Single(logger.Entries, static value => value.EventId.Id == 4944);
        Assert.DoesNotContain(sensitiveOrderId, entry.Message, StringComparison.Ordinal);
        dispatcher.Release();
        await dispatcher.WaitForCompletedAsync();
    }

    private static async Task StopAsync(CustomerOrderNotificationScheduler scheduler)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await scheduler.StopAsync(timeout.Token);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!predicate())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static CustomerOrderNotification Notification(string id) => new(
        id,
        "0900000000",
        "Khách hàng tổng hợp",
        1000,
        new DateTimeOffset(2026, 8, 13, 1, 2, 0, TimeSpan.Zero),
        "Trạm A",
        "STA");

    private sealed class GatedDispatcher(int expectedCalls) : ICustomerOrderNotificationDispatcher
    {
        private readonly TaskCompletionSource release = NewCompletionSource();
        private readonly TaskCompletionSource started = NewCompletionSource();
        private readonly TaskCompletionSource completed = NewCompletionSource();
        private int calls;
        private int completions;

        public int Calls => Volatile.Read(ref calls);

        public async Task DispatchAsync(
            CustomerOrderNotification notification,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == expectedCalls) started.TrySetResult();
            try
            {
                await release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                if (Interlocked.Increment(ref completions) == expectedCalls) completed.TrySetResult();
            }
        }

        public Task WaitForStartedAsync() => started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public Task WaitForCompletedAsync() => completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void Release() => release.TrySetResult();
    }

    private sealed class ThrowingDispatcher : ICustomerOrderNotificationDispatcher
    {
        public const string SensitiveMessage = "synthetic-customer-and-token-value";
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        public Task DispatchAsync(
            CustomerOrderNotification notification,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            throw new InvalidOperationException(SensitiveMessage);
        }
    }

    private sealed class CancellationObservingDispatcher : ICustomerOrderNotificationDispatcher
    {
        public TaskCompletionSource Started { get; } = NewCompletionSource();

        public TaskCompletionSource Cancelled { get; } = NewCompletionSource();

        public async Task DispatchAsync(
            CustomerOrderNotification notification,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Cancelled.TrySetResult();
                throw;
            }
        }
    }

    private sealed class ScopeTrackingFactory(
        ICustomerOrderNotificationDispatcher dispatcher) : IServiceScopeFactory
    {
        private int createdScopes;
        private int disposedScopes;

        public int CreatedScopes => Volatile.Read(ref createdScopes);

        public int DisposedScopes => Volatile.Read(ref disposedScopes);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref createdScopes);
            return new TrackingScope(dispatcher, () => Interlocked.Increment(ref disposedScopes));
        }

        private sealed class TrackingScope(
            ICustomerOrderNotificationDispatcher dispatcher,
            Action dispose) : IServiceScope, IServiceProvider
        {
            private int disposed;

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType) =>
                serviceType == typeof(ICustomerOrderNotificationDispatcher) ? dispatcher : null;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0) dispose();
            }
        }
    }

    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource started = new();
        private readonly CancellationTokenSource stopping = new();
        private readonly CancellationTokenSource stopped = new();

        public CancellationToken ApplicationStarted => started.Token;

        public CancellationToken ApplicationStopping => stopping.Token;

        public CancellationToken ApplicationStopped => stopped.Token;

        public void StopApplication()
        {
            stopping.Cancel();
            stopped.Cancel();
        }

        public void Dispose()
        {
            started.Dispose();
            stopping.Dispose();
            stopped.Dispose();
        }
    }

    private sealed class ListLogger : ILogger<CustomerOrderNotificationScheduler>
    {
        private readonly ConcurrentQueue<LogEntry> entries = new();

        public IReadOnlyCollection<LogEntry> Entries => entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, eventId, formatter(state, exception)));
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Message);
}
