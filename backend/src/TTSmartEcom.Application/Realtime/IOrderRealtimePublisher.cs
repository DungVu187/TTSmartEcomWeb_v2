namespace TTSmartEcom.Application.Realtime;

/// <summary>Publishes the bounded set of legacy order events consumed by the admin frontend.</summary>
public interface IOrderRealtimePublisher
{
    ValueTask PublishCreatedAsync(OrderCreatedRealtimeEvent message, CancellationToken cancellationToken);
    ValueTask PublishUpdatedAsync(OrderUpdatedRealtimeEvent message, CancellationToken cancellationToken);
    ValueTask PublishCancelledAsync(OrderCancelledRealtimeEvent message, CancellationToken cancellationToken);
    ValueTask PublishDeletedAsync(OrderDeletedRealtimeEvent message, CancellationToken cancellationToken);
}

public sealed record OrderCreatedRealtimeEvent(
    string OrderId,
    string? OrderCode,
    string UserPhone,
    decimal Total,
    DateTimeOffset? CreatedAt);

public sealed record OrderUpdatedRealtimeEvent(
    string OrderId,
    string UpdatedField,
    object? NewValue);

public sealed record OrderCancelledRealtimeEvent(
    string OrderId,
    string UserPhone);

public sealed record OrderDeletedRealtimeEvent(string OrderId);
