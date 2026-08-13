using TTSmartEcom.Domain.Inventory;

namespace TTSmartEcom.Application.Inventory;

public interface IInventoryOrderService
{
    Task<InventoryOrderListResult> ListAsync(InventoryOrderKind kind, InventoryOrderListQuery query, CancellationToken cancellationToken);
    Task<InventoryOrderProductSummaryResult> ListProductsAsync(InventoryOrderKind kind, int page, CancellationToken cancellationToken);
    Task<InventoryOrder?> GetAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken);
    Task<InventoryOrder> CreateAsync(InventoryOrderKind kind, string userName, string? orderName, string? note, IReadOnlyList<InventoryOrderLineInput> lines, CancellationToken cancellationToken);
    Task<InventoryOrder> UpdateMetadataAsync(InventoryOrderKind kind, string id, string? orderName, string? note, IReadOnlyList<string>? images, CancellationToken cancellationToken);
    Task<InventoryOrder> UpdateNameAsync(InventoryOrderKind kind, string id, string? orderName, string? note, CancellationToken cancellationToken);
    Task<InventoryOrder> SetStatusAsync(InventoryOrderKind kind, string id, bool status, CancellationToken cancellationToken);
    Task<InventoryOrder> SetLineStatusAsync(InventoryOrderKind kind, string id, int index, bool status, CancellationToken cancellationToken);
    Task<InventoryOrder> CompleteAsync(InventoryOrderKind kind, string id, bool status, CancellationToken cancellationToken);
    Task<InventoryOrder> CompleteAsync(InventoryOrderKind kind, string id, bool status, string? actorName, CancellationToken cancellationToken) =>
        CompleteAsync(kind, id, status, cancellationToken);
    Task<InventoryOrder> CompleteLineAsync(InventoryOrderKind kind, string id, int index, bool status, CancellationToken cancellationToken);
    Task<InventoryOrder> CompleteLineAsync(InventoryOrderKind kind, string id, int index, bool status, string? actorName, CancellationToken cancellationToken) =>
        CompleteLineAsync(kind, id, index, status, cancellationToken);
    Task<InventoryOrder> AddLineAsync(InventoryOrderKind kind, string id, InventoryOrderLineInput line, CancellationToken cancellationToken);
    Task<InventoryOrder> AddLineAsync(InventoryOrderKind kind, string id, InventoryOrderLineInput line, string? actorName, CancellationToken cancellationToken) =>
        AddLineAsync(kind, id, line, cancellationToken);
    Task<InventoryOrder> UpdateLineAsync(InventoryOrderKind kind, string id, int index, InventoryOrderLineUpdateInput line, CancellationToken cancellationToken);
    Task<InventoryOrder> UpdateLineAsync(InventoryOrderKind kind, string id, int index, InventoryOrderLineUpdateInput line, string? actorName, CancellationToken cancellationToken) =>
        UpdateLineAsync(kind, id, index, line, cancellationToken);
    Task<InventoryOrder> DeleteLineAsync(InventoryOrderKind kind, string id, int index, CancellationToken cancellationToken);
    Task<InventoryOrder> ReorderLinesAsync(InventoryOrderKind kind, string id, IReadOnlyList<InventoryOrderLineInput> lines, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken);
}

public sealed record InventoryOrderListResult(IReadOnlyList<InventoryOrder> Orders, int CurrentPage, int TotalPages, int TotalItems);
public sealed record InventoryOrderProductSummaryResult(IReadOnlyList<InventoryOrderProductSummary> Products, int CurrentPage, int TotalPages, int TotalItems);

public interface IInventoryOrderRepository
{
    Task<(IReadOnlyList<InventoryOrder> Orders, long Total)> ListAsync(InventoryOrderKind kind, InventoryOrderListQuery query, CancellationToken cancellationToken);
    Task<(IReadOnlyList<InventoryOrderProductSummary> Products, long Total)> ListProductsAsync(InventoryOrderKind kind, int page, CancellationToken cancellationToken);
    Task<InventoryOrder?> FindAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken);
    Task<InventoryOrder> InsertAsync(InventoryOrder order, CancellationToken cancellationToken);
    Task<InventoryOrder?> UpdateAsync(InventoryOrder order, int expectedVersion, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(InventoryOrderKind kind, string id, int expectedVersion, CancellationToken cancellationToken);
}
