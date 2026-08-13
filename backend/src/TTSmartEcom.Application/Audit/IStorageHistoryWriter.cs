namespace TTSmartEcom.Application.Audit;

public interface IStorageHistoryWriter
{
    Task AppendAsync(StorageHistoryWriteEntry entry, CancellationToken cancellationToken);
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
    string? Source = null);
