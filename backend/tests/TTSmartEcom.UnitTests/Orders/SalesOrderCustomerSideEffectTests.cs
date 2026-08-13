using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Cart;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class SalesOrderCustomerSideEffectTests
{
    private const string UserId = "507f1f77bcf86cd799439011";
    private const string ProductId = "507f191e810c19729de860ea";
    private const string OtherProductId = "507f191e810c19729de860eb";
    private const string StationId = "507f191e810c19729de860ec";

    [Fact]
    public async Task CreateCustomerAsync_WithSelectedStation_UpdatesUserAndSchedulesSnapshotAfterCommit()
    {
        FakeOrderRepository orders = new();
        FakeCartRepository carts = new(Owner());
        FakeScheduler scheduler = new(orders, carts);
        SalesOrderService service = Service(orders, carts, scheduler);

        SalesOrder result = await service.CreateCustomerAsync(
            UserId,
            new CustomerOrderCreate([new SalesOrderItem(ProductId, 0, 2)], "TRAM-01"),
            CancellationToken.None);

        Assert.Equal("TTS-42", result.OrderCode);
        Assert.True(carts.UpdateCalled);
        Assert.Equal(StationId, carts.StationId);
        CartItem remaining = Assert.Single(carts.RemainingItems);
        Assert.Equal(OtherProductId, remaining.ProductId);
        CustomerOrderNotification notification = Assert.Single(scheduler.Notifications);
        Assert.True(scheduler.OrderCommittedBeforeSchedule);
        Assert.True(scheduler.UserUpdatedBeforeSchedule);
        Assert.Equal("TTS-42", notification.OrderId);
        Assert.Equal("0900000000", notification.UserPhone);
        Assert.Equal("Khách kiểm thử", notification.UserName);
        Assert.Equal(200_000m, notification.Total);
        Assert.Equal("Trạm kiểm thử", notification.StationNames);
        Assert.Equal("TRAM-01", notification.StationCodes);
    }

    [Fact]
    public async Task CreateCustomerAsync_WhenPostCommitAdaptersFail_KeepsCommittedOrder()
    {
        FakeOrderRepository orders = new();
        FakeCartRepository carts = new(Owner()) { RejectUpdate = true };
        FakeScheduler scheduler = new(orders, carts) { RejectSchedule = true };
        SalesOrderService service = Service(orders, carts, scheduler);

        SalesOrder result = await service.CreateCustomerAsync(
            UserId,
            new CustomerOrderCreate([new SalesOrderItem(ProductId, 0, 1)], "TRAM-01"),
            CancellationToken.None);

        Assert.NotNull(orders.Inserted);
        Assert.Equal(orders.Inserted, result);
        Assert.True(carts.UpdateCalled);
        Assert.Equal(1, scheduler.Attempts);
    }

    [Fact]
    public async Task CreateAdminAndDraftAsync_DoNotScheduleCustomerNotifications()
    {
        FakeOrderRepository orders = new();
        FakeCartRepository carts = new(Owner());
        FakeScheduler scheduler = new(orders, carts);
        SalesOrderService service = Service(orders, carts, scheduler);

        await service.CreateAdminAsync(
            new AdminOrderCreate("0900000001", "Quản trị", [new SalesOrderItem(ProductId, 0, 1)]),
            CancellationToken.None);
        await service.CreateDraftAsync(CancellationToken.None);

        Assert.Empty(scheduler.Notifications);
        Assert.Equal(0, scheduler.Attempts);
        Assert.False(carts.UpdateCalled);
    }

    private static SalesOrderService Service(
        FakeOrderRepository orders,
        FakeCartRepository carts,
        FakeScheduler scheduler) =>
        new(
            orders,
            new FakeStockPort(),
            carts,
            new FakeCartCatalog(),
            new FakeStationRepository(),
            new NoopStorageHistoryWriter(),
            scheduler,
            NullLogger<SalesOrderService>.Instance);

    private static CartOwner Owner() => new(
        UserId,
        "0900000000",
        "Khách kiểm thử",
        "customer",
        [],
        [
            new CartItem(ProductId, 0, 2),
            new CartItem(OtherProductId, 0, 1),
        ],
        7);

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public SalesOrder? Inserted { get; private set; }

        public Task<(IReadOnlyList<SalesOrder> Orders, long Total)> ListAsync(
            SalesOrderListQuery query,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<SalesOrder>> ListByPhoneAsync(
            string phone,
            string? state,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<SalesOrder?> FindAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<SalesOrder?>(Inserted);

        public Task<SalesOrder> InsertAsync(SalesOrder order, CancellationToken cancellationToken)
        {
            Inserted = order with { Id = "507f191e810c19729de860ed" };
            return Task.FromResult(Inserted);
        }

        public Task<SalesOrder?> UpdateAsync(
            SalesOrder order,
            int expectedVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string id,
            int expectedVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<long> CountProcessingAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<long> NextOrderCodeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(42L);
    }

    private sealed class FakeCartRepository(CartOwner owner) : ICartRepository
    {
        public bool RejectUpdate { get; init; }
        public bool UpdateCalled { get; private set; }
        public string? StationId { get; private set; }
        public IReadOnlyList<CartItem> RemainingItems { get; private set; } = [];

        public Task<CartOwner?> FindOwnerAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<CartOwner?>(userId == owner.Id ? owner : null);

        public Task<IReadOnlyList<CartItem>> ReplaceAsync(
            string userId,
            IReadOnlyList<CartItem> items,
            int? expectedVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task UpdateAfterCustomerOrderAsync(
            string userId,
            IReadOnlyList<CartItem> items,
            string? stationId,
            int expectedVersion,
            CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            StationId = stationId;
            RemainingItems = items.ToArray();
            return RejectUpdate
                ? Task.FromException(new InvalidOperationException("Synthetic post-commit failure"))
                : Task.CompletedTask;
        }
    }

    private sealed class FakeCartCatalog : ICartProductCatalog
    {
        public Task<ProductVariantSnapshot?> FindVariantAsync(
            string productId,
            int variantIndex,
            CartOwner viewer,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProductVariantSnapshot?>(new(
                productId,
                variantIndex,
                "Sản phẩm kiểm thử",
                "Nhãn hiệu",
                "SP-01",
                "100000",
                null,
                10,
                10,
                25,
                true));

        public Task<IReadOnlySet<string>?> GetVisibleProductIdsAsync(
            CartOwner viewer,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>?>(null);
    }

    private sealed class FakeStockPort : IOrderStockPort
    {
        public Task<IReadOnlyList<StockAdjustment>> AdjustAsync(
            IReadOnlyList<StockAdjustment> adjustments,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockAdjustment>>(adjustments.ToArray());

        public Task RollbackAsync(
            IReadOnlyList<StockAdjustment> adjustments,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ProductOrderSnapshot?> GetProductAsync(
            string productId,
            int variantIndex,
            CancellationToken cancellationToken) =>
            Task.FromResult<ProductOrderSnapshot?>(new(
                productId,
                variantIndex,
                "507f191e810c19729de860ef",
                "Sản phẩm kiểm thử",
                "Nhãn hiệu",
                "SP-01",
                "100000",
                null,
                null,
                null,
                10,
                10,
                25,
                true));
    }

    private sealed class FakeStationRepository : IStationRepository
    {
        private static readonly Station Value = new(
            StationId,
            "Trạm kiểm thử",
            null,
            "TRAM-01",
            true,
            "Địa điểm",
            [ProductId]);

        public Task<Station?> FindByCodeAsync(
            string code,
            bool publicProjection,
            CancellationToken cancellationToken) =>
            Task.FromResult<Station?>(code == Value.StationCode ? Value : null);

        public Task<IReadOnlyList<Station>> FindByIdsAsync(
            IReadOnlyList<string> ids,
            bool publicProjection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Station>>(ids.Contains(StationId, StringComparer.Ordinal) ? [Value] : []);

        public Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopStorageHistoryWriter : IStorageHistoryWriter
    {
        public Task AppendAsync(
            StorageHistoryWriteEntry entry,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeScheduler(
        FakeOrderRepository orders,
        FakeCartRepository carts) : ICustomerOrderNotificationScheduler
    {
        public bool RejectSchedule { get; init; }
        public int Attempts { get; private set; }
        public bool OrderCommittedBeforeSchedule { get; private set; }
        public bool UserUpdatedBeforeSchedule { get; private set; }
        public List<CustomerOrderNotification> Notifications { get; } = [];

        public bool TrySchedule(CustomerOrderNotification notification)
        {
            Attempts++;
            OrderCommittedBeforeSchedule = orders.Inserted is not null;
            UserUpdatedBeforeSchedule = carts.UpdateCalled;
            if (RejectSchedule) throw new InvalidOperationException("Synthetic scheduler failure");
            Notifications.Add(notification);
            return true;
        }
    }
}
