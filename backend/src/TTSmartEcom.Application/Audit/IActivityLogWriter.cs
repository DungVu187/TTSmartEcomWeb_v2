using Microsoft.Extensions.Logging;

namespace TTSmartEcom.Application.Audit;

public interface IActivityLogWriter
{
    Task AppendAsync(ActivityLogWriteEntry entry, CancellationToken cancellationToken);

    Task AppendManyAsync(
        IReadOnlyCollection<ActivityLogWriteEntry> entries,
        CancellationToken cancellationToken);
}

public sealed record ActivityLogWriteEntry(
    string UserName,
    string Action,
    string? ProductId,
    string? ProductName,
    IReadOnlyList<ActivityLogWriteDetail> Details);

public sealed record ActivityLogWriteDetail(
    string? Field,
    string? OldValue = "",
    string? NewValue = "");

/// <summary>
/// Preserves the legacy mutation contract: the aggregate is committed before its
/// activity log is attempted, and an unavailable audit store does not roll the
/// aggregate back or turn the mutation into a failed response.
/// </summary>
public sealed partial class ActivityLogWriteService(
    IActivityLogWriter writer,
    ILogger<ActivityLogWriteService> logger)
{
    public async Task<bool> TryAppendAsync(
        ActivityLogWriteEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        try
        {
            await writer.AppendAsync(entry, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            LogAppendFailure(logger, entry.Action, exception.GetType().Name);
            return false;
        }
    }

    public async Task<bool> TryAppendManyAsync(
        IReadOnlyCollection<ActivityLogWriteEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return true;
        }

        try
        {
            await writer.AppendManyAsync(entries, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            string actions = string.Join(',', entries.Select(static entry => entry.Action)
                .Where(static action => !string.IsNullOrWhiteSpace(action))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
            LogAppendManyFailure(logger, actions, exception.GetType().Name, entries.Count);
            return false;
        }
    }

    [LoggerMessage(
        EventId = 4691,
        Level = LogLevel.Warning,
        Message = "ActivityLog append failed for action {Action}; error type {ErrorType}")]
    private static partial void LogAppendFailure(ILogger logger, string action, string errorType);

    [LoggerMessage(
        EventId = 4692,
        Level = LogLevel.Warning,
        Message = "ActivityLog batch append failed for actions {Actions}; error type {ErrorType}; entry count {EntryCount}")]
    private static partial void LogAppendManyFailure(
        ILogger logger,
        string actions,
        string errorType,
        int entryCount);
}
