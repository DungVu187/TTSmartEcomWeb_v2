using TTSmartEcom.Domain.Orders;

namespace TTSmartEcom.Application.Orders;

public interface IOrderService
{
    Task<OrderListResult> ListAdminAsync(SalesOrderListQuery query, CancellationToken cancellationToken);
    Task<OrderListResult> ListUserAsync(string userPhone, string? state, CancellationToken cancellationToken);
    Task<SalesOrder?> GetAsync(string id, CancellationToken cancellationToken);
    Task<SalesOrder?> GetAdminAsync(string id, CancellationToken cancellationToken);
    Task<int> ProcessingCountAsync(CancellationToken cancellationToken);
    Task<SalesOrder> CreateCustomerAsync(string userId, CustomerOrderCreate request, CancellationToken cancellationToken);
    Task<SalesOrder> CreateAdminAsync(AdminOrderCreate request, CancellationToken cancellationToken);
    Task<SalesOrder> CreateDraftAsync(CancellationToken cancellationToken);
    Task<SalesOrder> CancelAsync(string id, string requesterId, string? requesterPhone, bool isAdmin, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string id, string requesterId, string? requesterPhone, bool isAdmin, CancellationToken cancellationToken);
    Task<SalesOrder> UpdateFieldAsync(string id, string field, object? value, CancellationToken cancellationToken);
    Task<SalesOrder> UpdateFieldAsync(string id, string field, object? value, string? actorName, CancellationToken cancellationToken) =>
        UpdateFieldAsync(id, field, value, cancellationToken);
    Task<SalesOrder> AddItemAsync(string id, SalesOrderItem item, CancellationToken cancellationToken);
    Task<SalesOrder> UpdateItemAsync(string id, int index, int quantity, CancellationToken cancellationToken);
    Task<SalesOrder> DeleteItemAsync(string id, int index, CancellationToken cancellationToken);
    Task<SalesOrder> ReorderItemsAsync(string id, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken);
    Task<SalesOrder> UpdateCustomerAsync(string id, string? userName, string? userPhone, CancellationToken cancellationToken);
    Task<SalesOrder> UpdateImagesAsync(string id, IReadOnlyList<string> images, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesOrderItemDetail>> GetItemDetailsAsync(IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken);
}

public sealed record OrderListResult(IReadOnlyList<SalesOrder> Orders, int Total, int CurrentPage, int TotalPages, string? Message = null);

public interface IOrderRepository
{
    Task<(IReadOnlyList<SalesOrder> Orders, long Total)> ListAsync(SalesOrderListQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<SalesOrder>> ListByPhoneAsync(string phone, string? state, CancellationToken cancellationToken);
    Task<SalesOrder?> FindAsync(string id, CancellationToken cancellationToken);
    Task<SalesOrder> InsertAsync(SalesOrder order, CancellationToken cancellationToken);
    Task<SalesOrder?> UpdateAsync(SalesOrder order, int expectedVersion, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string id, int expectedVersion, CancellationToken cancellationToken);
    Task<long> CountProcessingAsync(CancellationToken cancellationToken);
    Task<long> NextOrderCodeAsync(CancellationToken cancellationToken);
}

public interface IOrderStockPort
{
    Task<IReadOnlyList<StockAdjustment>> AdjustAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken);
    Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken);
    Task<ProductOrderSnapshot?> GetProductAsync(string productId, int variantIndex, CancellationToken cancellationToken);
}

public sealed record StockAdjustment(string ProductId, int VariantIndex, double QuantityForSaleDelta, double QuantityInStorageDelta, double PurchaseCountDelta = 0, string? ExpectedVariantId = null);
public sealed record ProductOrderSnapshot(string ProductId, int VariantIndex, string? VariantId, string? Name, string? Brand, string? Code, string? Price, string? ImageUrl, string? Color, string? Shape, double QuantityForSale, double QuantityInStorage, double Earn, bool Display, string? ImportPrice = null);
public sealed record SalesOrderItemDetail(string ProductId, int VariantIndex, int Quantity, string? Name, string? Code, string? Brand, string? ImgUrl, string? Price, string? Color, string? Shape);
