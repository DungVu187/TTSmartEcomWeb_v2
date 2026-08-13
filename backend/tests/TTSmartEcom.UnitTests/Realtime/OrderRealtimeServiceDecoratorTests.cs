using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Realtime;
using TTSmartEcom.Domain.Orders;

namespace TTSmartEcom.UnitTests.Realtime;

public sealed class OrderRealtimeServiceDecoratorTests
{
    private const string OrderId = "507f1f77bcf86cd799439011";
    private static readonly DateTimeOffset CreatedAt = new(2026, 8, 13, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCustomer_PublishesCreatedEventFromPersistedOrder()
    {
        SalesOrder persisted = Order() with { OrderCode = "TTS-42", Total = 425_000m };
        var inner = new FakeOrderService(persisted);
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(inner, publisher);

        SalesOrder result = await service.CreateCustomerAsync(
            "user-1",
            new CustomerOrderCreate([]),
            CancellationToken.None);

        Assert.Same(persisted, result);
        OrderCreatedRealtimeEvent message = Assert.Single(publisher.Created);
        Assert.Equal(OrderId, message.OrderId);
        Assert.Equal("TTS-42", message.OrderCode);
        Assert.Equal("0900000000", message.UserPhone);
        Assert.Equal(425_000m, message.Total);
        Assert.Equal(CreatedAt, message.CreatedAt);
        publisher.AssertOnlyOneEvent();
    }

    [Fact]
    public async Task UpdateField_PublishesUpdatedEventWithPersistedValue()
    {
        SalesOrder persisted = Order() with { Status = "Completed" };
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(new FakeOrderService(persisted), publisher);

        SalesOrder result = await service.UpdateFieldAsync(
            OrderId,
            "status",
            "Delivering",
            CancellationToken.None);

        Assert.Same(persisted, result);
        OrderUpdatedRealtimeEvent message = Assert.Single(publisher.Updated);
        Assert.Equal(OrderId, message.OrderId);
        Assert.Equal("status", message.UpdatedField);
        Assert.Equal("Completed", message.NewValue);
        publisher.AssertOnlyOneEvent();
    }

    [Fact]
    public async Task Cancel_PublishesCancelledEventFromPersistedOrder()
    {
        SalesOrder persisted = Order() with { UserPhone = "0911222333", State = "Cancelled" };
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(new FakeOrderService(persisted), publisher);

        SalesOrder result = await service.CancelAsync(
            OrderId,
            "user-1",
            "0900000000",
            isAdmin: false,
            CancellationToken.None);

        Assert.Same(persisted, result);
        OrderCancelledRealtimeEvent message = Assert.Single(publisher.Cancelled);
        Assert.Equal(OrderId, message.OrderId);
        Assert.Equal("0911222333", message.UserPhone);
        publisher.AssertOnlyOneEvent();
    }

    [Fact]
    public async Task Delete_WhenMutationSucceeds_PublishesDeletedEvent()
    {
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(
            new FakeOrderService(Order()) { DeleteResult = true },
            publisher);

        bool result = await service.DeleteAsync(
            OrderId,
            "admin-1",
            null,
            isAdmin: true,
            CancellationToken.None);

        Assert.True(result);
        OrderDeletedRealtimeEvent message = Assert.Single(publisher.Deleted);
        Assert.Equal(OrderId, message.OrderId);
        publisher.AssertOnlyOneEvent();
    }

    [Fact]
    public async Task CreateDraft_DoesNotPublishRealtimeEvent()
    {
        SalesOrder persisted = Order();
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(new FakeOrderService(persisted), publisher);

        SalesOrder result = await service.CreateDraftAsync(CancellationToken.None);

        Assert.Same(persisted, result);
        Assert.Equal(0, publisher.EventCount);
    }

    [Fact]
    public async Task Delete_WhenMutationReturnsFalse_DoesNotPublishRealtimeEvent()
    {
        var publisher = new RecordingPublisher();
        var service = new OrderRealtimeServiceDecorator(
            new FakeOrderService(Order()) { DeleteResult = false },
            publisher);

        bool result = await service.DeleteAsync(
            OrderId,
            "admin-1",
            null,
            isAdmin: true,
            CancellationToken.None);

        Assert.False(result);
        Assert.Equal(0, publisher.EventCount);
    }

    [Theory]
    [InlineData(MutationKind.Create)]
    [InlineData(MutationKind.Update)]
    [InlineData(MutationKind.Cancel)]
    [InlineData(MutationKind.Delete)]
    public async Task PublisherFailure_DoesNotFailCommittedMutation(MutationKind mutation)
    {
        SalesOrder persisted = Order();
        var publisher = new RecordingPublisher { ThrowOnPublish = true };
        var service = new OrderRealtimeServiceDecorator(
            new FakeOrderService(persisted) { DeleteResult = true },
            publisher);

        object result = mutation switch
        {
            MutationKind.Create => await service.CreateAdminAsync(
                new AdminOrderCreate(persisted.UserPhone, persisted.UserName, []),
                CancellationToken.None),
            MutationKind.Update => await service.UpdateFieldAsync(
                OrderId,
                "payment",
                true,
                CancellationToken.None),
            MutationKind.Cancel => await service.CancelAsync(
                OrderId,
                "admin-1",
                null,
                isAdmin: true,
                CancellationToken.None),
            MutationKind.Delete => await service.DeleteAsync(
                OrderId,
                "admin-1",
                null,
                isAdmin: true,
                CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

        if (mutation == MutationKind.Delete)
        {
            Assert.Equal(true, result);
        }
        else
        {
            Assert.Same(persisted, result);
        }

        Assert.Equal(1, publisher.PublishAttempts);
    }

    private static SalesOrder Order() => new(
        OrderId,
        "TTS-01",
        "0900000000",
        "Khách tổng hợp",
        [],
        100_000m,
        "Processing",
        false,
        "Processing",
        null,
        [],
        CreatedAt,
        CreatedAt,
        0);

    public enum MutationKind
    {
        Create,
        Update,
        Cancel,
        Delete,
    }

    private sealed class RecordingPublisher : IOrderRealtimePublisher
    {
        public List<OrderCreatedRealtimeEvent> Created { get; } = [];
        public List<OrderUpdatedRealtimeEvent> Updated { get; } = [];
        public List<OrderCancelledRealtimeEvent> Cancelled { get; } = [];
        public List<OrderDeletedRealtimeEvent> Deleted { get; } = [];
        public bool ThrowOnPublish { get; init; }
        public int PublishAttempts { get; private set; }
        public int EventCount => Created.Count + Updated.Count + Cancelled.Count + Deleted.Count;

        public ValueTask PublishCreatedAsync(
            OrderCreatedRealtimeEvent message,
            CancellationToken cancellationToken) => Record(Created, message);

        public ValueTask PublishUpdatedAsync(
            OrderUpdatedRealtimeEvent message,
            CancellationToken cancellationToken) => Record(Updated, message);

        public ValueTask PublishCancelledAsync(
            OrderCancelledRealtimeEvent message,
            CancellationToken cancellationToken) => Record(Cancelled, message);

        public ValueTask PublishDeletedAsync(
            OrderDeletedRealtimeEvent message,
            CancellationToken cancellationToken) => Record(Deleted, message);

        public void AssertOnlyOneEvent() => Assert.Equal(1, EventCount);

        private ValueTask Record<T>(ICollection<T> target, T message)
        {
            PublishAttempts++;
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException("Lỗi realtime tổng hợp");
            }

            target.Add(message);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeOrderService(SalesOrder result) : IOrderService
    {
        public bool DeleteResult { get; init; } = true;

        public Task<SalesOrder> CreateCustomerAsync(
            string userId,
            CustomerOrderCreate request,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<SalesOrder> CreateAdminAsync(
            AdminOrderCreate request,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<SalesOrder> CreateDraftAsync(CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<SalesOrder> CancelAsync(
            string id,
            string requesterId,
            string? requesterPhone,
            bool isAdmin,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<bool> DeleteAsync(
            string id,
            string requesterId,
            string? requesterPhone,
            bool isAdmin,
            CancellationToken cancellationToken) => Task.FromResult(DeleteResult);

        public Task<SalesOrder> UpdateFieldAsync(
            string id,
            string field,
            object? value,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<SalesOrder> UpdateFieldAsync(
            string id,
            string field,
            object? value,
            string? actorName,
            CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<OrderListResult> ListAdminAsync(
            SalesOrderListQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<OrderListResult> ListUserAsync(
            string userPhone,
            string? state,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder?> GetAsync(
            string id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder?> GetAdminAsync(
            string id,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> ProcessingCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> AddItemAsync(
            string id,
            SalesOrderItem item,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> UpdateItemAsync(
            string id,
            int index,
            int quantity,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> DeleteItemAsync(
            string id,
            int index,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> ReorderItemsAsync(
            string id,
            IReadOnlyList<SalesOrderItem> items,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> UpdateCustomerAsync(
            string id,
            string? userName,
            string? userPhone,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder> UpdateImagesAsync(
            string id,
            IReadOnlyList<string> images,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SalesOrderItemDetail>> GetItemDetailsAsync(
            IReadOnlyList<SalesOrderItem> items,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
