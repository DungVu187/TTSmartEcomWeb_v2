using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Cart;
using TTSmartEcom.Domain.Orders;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.UnitTests.Orders;

public sealed class SalesOrderStorageHistoryTests
{
    private const string OrderId = "507f1f77bcf86cd799439011";
    private const string ProductId = "507f191e810c19729de860ea";
    private const string VariantId = "507f191e810c19729de860eb";

    [Theory]
    [InlineData("Processing", "Completed", -2, "online_sale", "Đơn hàng bán online")]
    [InlineData("Completed", "Delivering", 2, "online_sale_revert", "Hoàn tác đơn bán online")]
    public async Task UpdateStatus_WritesExactLegacyHistoryAfterCommit(
        string previousStatus, string nextStatus, double quantity, string source, string note)
    {
        FakeRepository repository = new(Order(previousStatus));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        SalesOrderService service = Service(repository, stock, history);

        SalesOrder result = await service.UpdateFieldAsync(
            OrderId, "status", nextStatus, "Quản trị", CancellationToken.None);

        Assert.Equal(nextStatus, result.Status);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.True(repository.CommittedBeforeHistory);
        Assert.Equal(ProductId, entry.ProductId);
        Assert.Equal("Sản phẩm kiểm thử", entry.ProductName);
        Assert.Equal(quantity, entry.Quantity);
        Assert.Equal("Quản trị", entry.UserName);
        Assert.Equal("TTS-01", entry.OrderId);
        Assert.Equal("TTS-01", entry.OrderName);
        Assert.Equal(note, entry.Note);
        Assert.False(entry.IsAiScan);
        Assert.Equal(source, entry.Source);
    }

    [Fact]
    public async Task UpdateStatus_WhenHistoryFails_KeepsCommittedStockAndOrder()
    {
        FakeRepository repository = new(Order("Processing"));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new() { RejectWrites = true };
        SalesOrderService service = Service(repository, stock, history);

        SalesOrder result = await service.UpdateFieldAsync(
            OrderId, "status", "Completed", "Quản trị", CancellationToken.None);

        Assert.Equal("Completed", result.Status);
        Assert.NotNull(repository.Saved);
        Assert.Single(stock.Adjustments);
        Assert.Empty(stock.Rollbacks);
        Assert.Single(history.Attempts);
    }

    private static SalesOrderService Service(
        FakeRepository repository, FakeStockPort stock, FakeHistoryWriter history) =>
        new(repository, stock, new StubCartRepository(), new StubCartCatalog(), new StubStationRepository(),
            history, new StubNotificationScheduler(), NullLogger<SalesOrderService>.Instance);

    private static SalesOrder Order(string status) => new(
        OrderId,
        "TTS-01",
        "0900000000",
        "Khách hàng",
        [new SalesOrderItem(ProductId, 0, 2)],
        200000,
        status,
        false,
        "Processing",
        status == "Completed" ? DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture) : null,
        [],
        DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture),
        0);

    private sealed class FakeRepository(SalesOrder initial) : IOrderRepository
    {
        public SalesOrder? Saved { get; private set; }
        public bool CommittedBeforeHistory => Saved is not null;

        public Task<(IReadOnlyList<SalesOrder> Orders, long Total)> ListAsync(SalesOrderListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<SalesOrder>, long)>(([initial], 1));
        public Task<IReadOnlyList<SalesOrder>> ListByPhoneAsync(string phone, string? state, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SalesOrder>>([initial]);
        public Task<SalesOrder?> FindAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<SalesOrder?>(id == initial.Id ? initial : null);
        public Task<SalesOrder> InsertAsync(SalesOrder order, CancellationToken cancellationToken) => Task.FromResult(order);
        public Task<SalesOrder?> UpdateAsync(SalesOrder order, int expectedVersion, CancellationToken cancellationToken)
        {
            Saved = order with { Version = expectedVersion + 1 };
            return Task.FromResult<SalesOrder?>(Saved);
        }
        public Task<bool> DeleteAsync(string id, int expectedVersion, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<long> CountProcessingAsync(CancellationToken cancellationToken) => Task.FromResult(0L);
        public Task<long> NextOrderCodeAsync(CancellationToken cancellationToken) => Task.FromResult(1L);
    }

    private sealed class FakeStockPort : IOrderStockPort
    {
        public List<StockAdjustment> Adjustments { get; } = [];
        public List<StockAdjustment> Rollbacks { get; } = [];
        public Task<IReadOnlyList<StockAdjustment>> AdjustAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
        {
            Adjustments.AddRange(adjustments);
            return Task.FromResult<IReadOnlyList<StockAdjustment>>(adjustments.ToArray());
        }
        public Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
        {
            Rollbacks.AddRange(adjustments);
            return Task.CompletedTask;
        }
        public Task<ProductOrderSnapshot?> GetProductAsync(string productId, int variantIndex, CancellationToken cancellationToken) =>
            Task.FromResult<ProductOrderSnapshot?>(new(productId, variantIndex, VariantId, "Sản phẩm kiểm thử",
                "Nhãn hiệu", "SP-1", "100000", null, null, null, 100, 100, 25, true));
    }

    private sealed class FakeHistoryWriter : IStorageHistoryWriter
    {
        public bool RejectWrites { get; init; }
        public List<StorageHistoryWriteEntry> Attempts { get; } = [];
        public List<StorageHistoryWriteEntry> Entries { get; } = [];
        public Task AppendAsync(StorageHistoryWriteEntry entry, CancellationToken cancellationToken)
        {
            Attempts.Add(entry);
            if (RejectWrites) throw new InvalidOperationException("Lỗi history tổng hợp");
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class StubCartRepository : ICartRepository
    {
        public Task<CartOwner?> FindOwnerAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<CartOwner?>(null);
        public Task<IReadOnlyList<CartItem>> ReplaceAsync(string userId, IReadOnlyList<CartItem> items, int? expectedVersion, CancellationToken cancellationToken) => Task.FromResult(items);
        public Task UpdateAfterCustomerOrderAsync(string userId, IReadOnlyList<CartItem> items, string? stationId, int expectedVersion, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubCartCatalog : ICartProductCatalog
    {
        public Task<ProductVariantSnapshot?> FindVariantAsync(string productId, int variantIndex, CartOwner viewer, CancellationToken cancellationToken) => Task.FromResult<ProductVariantSnapshot?>(null);
        public Task<IReadOnlySet<string>?> GetVisibleProductIdsAsync(CartOwner viewer, CancellationToken cancellationToken) => Task.FromResult<IReadOnlySet<string>?>(null);
    }

    private sealed class StubStationRepository : IStationRepository
    {
        public Task<StationPage> ListAsync(int page, int limit, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> FindByIdAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> FindByCodeAsync(string code, bool publicProjection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Station>> FindByCodesAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Station>> FindByIdsAsync(IReadOnlyList<string> ids, bool publicProjection, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> CreateAsync(NewStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateAsync(string id, UpdateStationData station, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateProductsAsync(string id, IReadOnlyList<string> productIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> UpdateImageAsync(string id, string imageUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Station?> RemoveImageAsync(string id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubNotificationScheduler : ICustomerOrderNotificationScheduler
    {
        public bool TrySchedule(CustomerOrderNotification notification) => true;
    }
}
