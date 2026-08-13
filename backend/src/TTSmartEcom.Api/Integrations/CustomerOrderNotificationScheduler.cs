using System.Collections.Concurrent;
using TTSmartEcom.Application.Orders;

namespace TTSmartEcom.Api.Integrations;

/// <summary>
/// Scheduler best-effort, bounded và không có queue. Mỗi task tạo DI scope mới;
/// khi đủ capacity, notification mới bị bỏ ngay để request tạo đơn không bị chặn.
/// </summary>
public sealed partial class CustomerOrderNotificationScheduler(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<CustomerOrderNotificationScheduler> logger) :
    ICustomerOrderNotificationScheduler,
    IHostedService
{
    private const int MaximumConcurrency = 4;
    private readonly ConcurrentDictionary<long, Task> active = new();
    private readonly object lifecycleGate = new();
    private long sequence;
    private int activeCount;
    private int stopping;

    public bool TrySchedule(CustomerOrderNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (Volatile.Read(ref stopping) != 0 || !TryAcquireCapacity())
        {
            LogDropped(logger);
            return false;
        }

        lock (lifecycleGate)
        {
            if (Volatile.Read(ref stopping) != 0)
            {
                Interlocked.Decrement(ref activeCount);
                LogDropped(logger);
                return false;
            }

            long id = Interlocked.Increment(ref sequence);
            Task task = Task.Run(
                () => RunAsync(notification, lifetime.ApplicationStopping),
                CancellationToken.None);
            active[id] = task;
            _ = task.ContinueWith(
                static (_, state) =>
                {
                    var values = ((ConcurrentDictionary<long, Task> Active, long Id))state!;
                    values.Active.TryRemove(values.Id, out Task? _);
                },
                (active, id),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        return true;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Task[] pending;
        lock (lifecycleGate)
        {
            Interlocked.Exchange(ref stopping, 1);
            pending = active.Values.ToArray();
        }
        if (pending.Length == 0) return;
        try
        {
            await Task.WhenAll(pending).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogShutdownBudgetExpired(logger);
        }
    }

    private async Task RunAsync(
        CustomerOrderNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ICustomerOrderNotificationDispatcher dispatcher =
                scope.ServiceProvider.GetRequiredService<ICustomerOrderNotificationDispatcher>();
            await dispatcher.DispatchAsync(notification, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCancelled(logger);
        }
        catch (Exception exception)
        {
            LogFailure(logger, exception.GetType().Name);
        }
        finally
        {
            Interlocked.Decrement(ref activeCount);
        }
    }

    private bool TryAcquireCapacity()
    {
        while (true)
        {
            int current = Volatile.Read(ref activeCount);
            if (current >= MaximumConcurrency) return false;
            if (Interlocked.CompareExchange(ref activeCount, current + 1, current) == current)
            {
                return true;
            }
        }
    }

    [LoggerMessage(EventId = 4941, Level = LogLevel.Warning, Message = "Order notification was dropped because the bounded scheduler is unavailable")]
    private static partial void LogDropped(ILogger logger);

    [LoggerMessage(EventId = 4942, Level = LogLevel.Information, Message = "Order notification stopped during application shutdown")]
    private static partial void LogCancelled(ILogger logger);

    [LoggerMessage(EventId = 4943, Level = LogLevel.Warning, Message = "Order notification dispatch failed with {ErrorType}")]
    private static partial void LogFailure(ILogger logger, string errorType);

    [LoggerMessage(EventId = 4944, Level = LogLevel.Warning, Message = "Order notification shutdown wait exceeded the host shutdown budget")]
    private static partial void LogShutdownBudgetExpired(ILogger logger);
}
