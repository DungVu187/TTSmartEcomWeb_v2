using TTSmartEcom.Application.Realtime;

namespace TTSmartEcom.Api.Realtime;

internal sealed class SocketIoOrderRealtimePublisher(SocketIoServer server) : IOrderRealtimePublisher
{
    public ValueTask PublishCreatedAsync(
        OrderCreatedRealtimeEvent message,
        CancellationToken cancellationToken) => server.BroadcastAsync(
            "order_created",
            new
            {
                orderId = message.OrderId,
                orderCode = message.OrderCode,
                userPhone = message.UserPhone,
                total = message.Total,
                createdAt = message.CreatedAt,
            },
            cancellationToken);

    public ValueTask PublishUpdatedAsync(
        OrderUpdatedRealtimeEvent message,
        CancellationToken cancellationToken) => server.BroadcastAsync(
            "order_updated",
            new
            {
                orderId = message.OrderId,
                updatedField = message.UpdatedField,
                newValue = message.NewValue,
            },
            cancellationToken);

    public ValueTask PublishCancelledAsync(
        OrderCancelledRealtimeEvent message,
        CancellationToken cancellationToken) => server.BroadcastAsync(
            "order_cancelled",
            new { orderId = message.OrderId, userPhone = message.UserPhone },
            cancellationToken);

    public ValueTask PublishDeletedAsync(
        OrderDeletedRealtimeEvent message,
        CancellationToken cancellationToken) => server.BroadcastAsync(
            "order_deleted",
            new { orderId = message.OrderId },
            cancellationToken);
}
