namespace TTSmartEcom.Application.Audit;

public interface IStorageHistoryWriter
{
    Task AppendAsync(StorageHistoryWriteEntry entry, CancellationToken cancellationToken);
    Task UpdateTransactionDateAsync(string orderId, DateTimeOffset transactionDate, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed record StorageHistoryWriteEntry(
    string ProductId,
    string ProductName,
    double Quantity,
    string? UserName = null,
    string? OrderId = null,
    string? OrderName = null,
    string? Note = null,
    bool IsAiScan = false,
    string? Source = null,
    DateTimeOffset? TransactionDate = null,
    double? QuantityBefore = null,
    double? QuantityAfter = null);
