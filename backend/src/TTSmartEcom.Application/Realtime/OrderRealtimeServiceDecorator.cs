using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Orders;

namespace TTSmartEcom.Application.Realtime;

/// <summary>Adds the four legacy realtime side effects after successful order mutations.</summary>
public sealed partial class OrderRealtimeServiceDecorator(
    IOrderService inner,
    IOrderRealtimePublisher publisher,
    ILogger<OrderRealtimeServiceDecorator>? configuredLogger = null) : IOrderService
{
    private readonly ILogger<OrderRealtimeServiceDecorator> logger =
        configuredLogger ?? NullLogger<OrderRealtimeServiceDecorator>.Instance;

    public Task<OrderListResult> ListAdminAsync(SalesOrderListQuery query, CancellationToken cancellationToken) =>
        inner.ListAdminAsync(query, cancellationToken);

    public Task<OrderListResult> ListUserAsync(string userPhone, string? state, CancellationToken cancellationToken) =>
        inner.ListUserAsync(userPhone, state, cancellationToken);

    public Task<SalesOrder?> GetAsync(string id, CancellationToken cancellationToken) =>
        inner.GetAsync(id, cancellationToken);

    public Task<SalesOrder?> GetAdminAsync(string id, CancellationToken cancellationToken) =>
        inner.GetAdminAsync(id, cancellationToken);

    public Task<int> ProcessingCountAsync(CancellationToken cancellationToken) =>
        inner.ProcessingCountAsync(cancellationToken);

    public async Task<SalesOrder> CreateCustomerAsync(
        string userId,
        CustomerOrderCreate request,
        CancellationToken cancellationToken)
    {
        SalesOrder order = await inner.CreateCustomerAsync(userId, request, cancellationToken);
        await PublishBestEffortAsync("order_created", () => PublishCreatedAsync(order, cancellationToken));
        return order;
    }

    public async Task<SalesOrder> CreateAdminAsync(
        AdminOrderCreate request,
        CancellationToken cancellationToken)
    {
        SalesOrder order = await inner.CreateAdminAsync(request, cancellationToken);
        await PublishBestEffortAsync("order_created", () => PublishCreatedAsync(order, cancellationToken));
        return order;
    }

    // The legacy admin-draft route did not emit an order_created event.
    public Task<SalesOrder> CreateDraftAsync(CancellationToken cancellationToken) =>
        inner.CreateDraftAsync(cancellationToken);

    public async Task<SalesOrder> CancelAsync(
        string id,
        string requesterId,
        string? requesterPhone,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        SalesOrder order = await inner.CancelAsync(id, requesterId, requesterPhone, isAdmin, cancellationToken);
        await PublishBestEffortAsync("order_cancelled", () => publisher.PublishCancelledAsync(
            new OrderCancelledRealtimeEvent(order.Id, order.UserPhone), cancellationToken));
        return order;
    }

    public async Task<bool> DeleteAsync(
        string id,
        string requesterId,
        string? requesterPhone,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        bool deleted = await inner.DeleteAsync(id, requesterId, requesterPhone, isAdmin, cancellationToken);
        if (deleted)
        {
            await PublishBestEffortAsync("order_deleted", () => publisher.PublishDeletedAsync(
                new OrderDeletedRealtimeEvent(id), cancellationToken));
        }

        return deleted;
    }

    public async Task<SalesOrder> UpdateFieldAsync(
        string id,
        string field,
        object? value,
        CancellationToken cancellationToken)
    {
        SalesOrder order = await inner.UpdateFieldAsync(id, field, value, cancellationToken);
        await PublishBestEffortAsync("order_updated", () => PublishUpdatedAsync(order, field, cancellationToken));
        return order;
    }

    public async Task<SalesOrder> UpdateFieldAsync(
        string id,
        string field,
        object? value,
        string? actorName,
        CancellationToken cancellationToken)
    {
        SalesOrder order = await inner.UpdateFieldAsync(id, field, value, actorName, cancellationToken);
        await PublishBestEffortAsync("order_updated", () => PublishUpdatedAsync(order, field, cancellationToken));
        return order;
    }

    public Task<SalesOrder> AddItemAsync(
        string id,
        SalesOrderItem item,
        CancellationToken cancellationToken) => inner.AddItemAsync(id, item, cancellationToken);

    public Task<SalesOrder> UpdateItemAsync(
        string id,
        int index,
        int quantity,
        CancellationToken cancellationToken) => inner.UpdateItemAsync(id, index, quantity, cancellationToken);

    public Task<SalesOrder> DeleteItemAsync(
        string id,
        int index,
        CancellationToken cancellationToken) => inner.DeleteItemAsync(id, index, cancellationToken);

    public Task<SalesOrder> ReorderItemsAsync(
        string id,
        IReadOnlyList<SalesOrderItem> items,
        CancellationToken cancellationToken) => inner.ReorderItemsAsync(id, items, cancellationToken);

    public Task<SalesOrder> UpdateCustomerAsync(
        string id,
        string? userName,
        string? userPhone,
        CancellationToken cancellationToken) => inner.UpdateCustomerAsync(id, userName, userPhone, cancellationToken);

    public Task<SalesOrder> UpdateImagesAsync(
        string id,
        IReadOnlyList<string> images,
        CancellationToken cancellationToken) => inner.UpdateImagesAsync(id, images, cancellationToken);

    public Task<IReadOnlyList<SalesOrderItemDetail>> GetItemDetailsAsync(
        IReadOnlyList<SalesOrderItem> items,
        CancellationToken cancellationToken) => inner.GetItemDetailsAsync(items, cancellationToken);

    private ValueTask PublishCreatedAsync(SalesOrder order, CancellationToken cancellationToken) =>
        publisher.PublishCreatedAsync(
            new OrderCreatedRealtimeEvent(
                order.Id,
                order.OrderCode,
                order.UserPhone,
                order.Total,
                order.CreatedAt),
            cancellationToken);

    private ValueTask PublishUpdatedAsync(
        SalesOrder order,
        string field,
        CancellationToken cancellationToken)
    {
        object? persistedValue = field switch
        {
            "status" => order.Status,
            "payment" => order.Payment,
            _ => null,
        };

        return publisher.PublishUpdatedAsync(
            new OrderUpdatedRealtimeEvent(order.Id, field, persistedValue), cancellationToken);
    }

    private async ValueTask PublishBestEffortAsync(string eventName, Func<ValueTask> publish)
    {
        try
        {
            await publish();
        }
        catch (Exception exception)
        {
            LogPublishFailure(logger, eventName, exception.GetType().Name);
        }
    }

    [LoggerMessage(
        EventId = 4985,
        Level = LogLevel.Warning,
        Message = "Socket.IO order event {EventName} failed with {ErrorType} after mutation commit")]
    private static partial void LogPublishFailure(ILogger logger, string eventName, string errorType);
}
