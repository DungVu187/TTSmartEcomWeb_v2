using System.Globalization;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Inventory;
using TtsApplicationException = TTSmartEcom.Application.Common.Errors.ApplicationException;

namespace TTSmartEcom.UnitTests.Inventory;

public sealed class InventoryOrderServiceTests
{
    private const string OrderId = "507f1f77bcf86cd799439011";
    private const string ProductId = "507f191e810c19729de860ea";
    private const string VariantId = "507f191e810c19729de860eb";

    [Fact]
    public async Task CompleteImportLine_AppliesOnlyRemainingQuantity_ThenPersistsByVersion()
    {
        InventoryOrder order = Order(InventoryOrderKind.Import,
            Line(quantity: 10, progress: 6, applied: 4));
        FakeRepository repository = new(order);
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);

        InventoryOrder result = await service.CompleteLineAsync(
            InventoryOrderKind.Import, OrderId, 0, true, CancellationToken.None);

        StockAdjustment adjustment = Assert.Single(stock.Adjustments);
        Assert.Equal(6, adjustment.QuantityForSaleDelta);
        Assert.Equal(6, adjustment.QuantityInStorageDelta);
        Assert.Equal(VariantId, adjustment.ExpectedVariantId);
        Assert.Equal(0, repository.LastExpectedVersion);
        Assert.True(result.ProductList[0].Status);
        Assert.Equal(10, result.ProductList[0].ProgressQuantity);
        Assert.Equal(10, result.ProductList[0].StockAppliedQuantity);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.Equal(ProductId, entry.ProductId);
        Assert.Equal("Sản phẩm kiểm thử", entry.ProductName);
        Assert.Equal(6, entry.Quantity);
        Assert.Equal("Nhân viên kiểm thử", entry.UserName);
        Assert.Equal(OrderId, entry.OrderId);
        Assert.Equal("Đơn kiểm thử", entry.OrderName);
        Assert.Equal("Nhập kho (đơn nhập hoàn thành)", entry.Note);
        Assert.Equal("order_line_complete", entry.Source);
    }

    [Fact]
    public async Task CompleteExportOrder_PreservesLegacyUntrackedQuantity_AndAppliesTrackedRemainder()
    {
        InventoryOrder order = Order(InventoryOrderKind.Export,
            Line(quantity: 10, progress: 4, applied: 2),
            Line(quantity: 3, progress: 3, applied: 0, skipped: true));
        FakeRepository repository = new(order);
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);

        InventoryOrder result = await service.CompleteAsync(
            InventoryOrderKind.Export, OrderId, true, CancellationToken.None);

        StockAdjustment adjustment = Assert.Single(stock.Adjustments);
        Assert.Equal(-6, adjustment.QuantityForSaleDelta);
        Assert.Equal(-6, adjustment.QuantityInStorageDelta);
        Assert.All(result.ProductList, line => Assert.True(line.Status));
        Assert.Equal(10, result.ProductList[0].ProgressQuantity);
        Assert.Equal(8, result.ProductList[0].StockAppliedQuantity);
        Assert.Equal(3, result.ProductList[1].ProgressQuantity);
        Assert.Equal(0, result.ProductList[1].StockAppliedQuantity);
        Assert.True(result.Status);
        Assert.NotNull(result.CompletedAt);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.Equal(-6, entry.Quantity);
        Assert.Equal("Xuất kho (đơn xuất hoàn thành)", entry.Note);
        Assert.Equal("order_bulk_complete", entry.Source);
    }

    [Fact]
    public async Task CompleteLine_WhenOrderCasLoses_RollsBackAppliedStockAndReturnsConflict()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export,
            Line(quantity: 2, progress: 0, applied: 0)))
        {
            RejectUpdates = true,
        };
        FakeStockPort stock = new();
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.CompleteLineAsync(InventoryOrderKind.Export, OrderId, 0, true, CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
        Assert.Single(stock.Adjustments);
        StockAdjustment rollback = Assert.Single(stock.Rollbacks);
        Assert.Equal(-2, rollback.QuantityForSaleDelta);
        Assert.Equal(-2, rollback.QuantityInStorageDelta);
    }

    [Fact]
    public async Task CompleteLine_WhenRollbackFails_ReturnsDeterministicServerError()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import,
            Line(quantity: 2, progress: 0, applied: 0)))
        {
            RejectUpdates = true,
        };
        FakeStockPort stock = new() { RejectRollback = true };
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.CompleteLineAsync(InventoryOrderKind.Import, OrderId, 0, true, CancellationToken.None));

        Assert.Equal(500, error.Error.HttpStatus);
        Assert.Equal("TTS-INVORDER-ROLLBACK", error.Error.Code);
    }

    [Fact]
    public async Task CompleteLine_WithCorruptAppliedQuantity_RejectsBeforeStockMutation()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import,
            Line(quantity: 5, progress: 2, applied: 3)));
        FakeStockPort stock = new();
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.CompleteLineAsync(InventoryOrderKind.Import, OrderId, 0, true, CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
        Assert.Empty(stock.Adjustments);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task CompleteLine_WithFalseStatus_IsIdempotentAndDoesNotWrite()
    {
        InventoryOrder original = Order(InventoryOrderKind.Export,
            Line(quantity: 2, progress: 0, applied: 0));
        FakeRepository repository = new(original);
        FakeStockPort stock = new();
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        InventoryOrder result = await service.CompleteLineAsync(
            InventoryOrderKind.Export, OrderId, 0, false, CancellationToken.None);

        Assert.Same(original, result);
        Assert.Empty(stock.Adjustments);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task CompleteExportLine_WithInsufficientSaleStock_ReturnsLegacyBadRequestBeforeMutation()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export,
            Line(quantity: 3, progress: 0, applied: 0)));
        FakeStockPort stock = new() { QuantityForSale = 2, QuantityInStorage = 10 };
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.CompleteLineAsync(InventoryOrderKind.Export, OrderId, 0, true, CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
        Assert.Contains("Tồn khả dụng hiện có: 2", error.Error.ClientMessage, StringComparison.Ordinal);
        Assert.Empty(stock.Adjustments);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task CompleteImportLine_WhenHistoryFails_KeepsCommittedStockAndOrder()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import,
            Line(quantity: 2, progress: 0, applied: 0)));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new() { RejectWrites = true };
        InventoryOrderService service = new(repository, stock, history);

        InventoryOrder result = await service.CompleteLineAsync(
            InventoryOrderKind.Import, OrderId, 0, true, "Người thực hiện", CancellationToken.None);

        Assert.True(result.ProductList[0].Status);
        Assert.NotNull(repository.Saved);
        Assert.Single(stock.Adjustments);
        Assert.Empty(stock.Rollbacks);
        Assert.Single(history.Attempts);
        Assert.Equal("Người thực hiện", history.Attempts[0].UserName);
    }

    [Fact]
    public async Task AddImportLine_WithProgress_AppliesStockAndWritesManualHistory()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);
        InventoryOrderLineInput input = Input(quantity: 5, progress: 2, isAiScan: true);

        InventoryOrder result = await service.AddLineAsync(
            InventoryOrderKind.Import, OrderId, input, "Nhân viên kho", CancellationToken.None);

        StockAdjustment adjustment = Assert.Single(stock.Adjustments);
        Assert.Equal(2, adjustment.QuantityForSaleDelta);
        Assert.Equal(2, adjustment.QuantityInStorageDelta);
        InventoryOrderLine line = Assert.Single(result.ProductList);
        Assert.Equal(2, line.ProgressQuantity);
        Assert.Equal(2, line.StockAppliedQuantity);
        Assert.False(line.Status);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.Equal(2, entry.Quantity);
        Assert.Equal("Nhân viên kho", entry.UserName);
        Assert.Equal("Nhập kho (AI scan đơn nhập)", entry.Note);
        Assert.True(entry.IsAiScan);
        Assert.Null(entry.Source);
    }

    [Fact]
    public async Task AddImportLine_WhenProductIsNotAssignedToBranch_IsRejectedBeforePersistence()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import));
        FakeStockPort stock = new() { IsAssignedToBranch = false };
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AddLineAsync(
                InventoryOrderKind.Import,
                OrderId,
                Input(quantity: 5, progress: 0),
                CancellationToken.None));

        Assert.Equal(403, error.Error.HttpStatus);
        Assert.Null(repository.Saved);
        Assert.Empty(stock.Adjustments);
    }

    [Fact]
    public async Task CreateImportOrder_WithZeroQuantity_KeepsLineIncompleteLikeLegacy()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import));
        InventoryOrderService service = new(repository, new FakeStockPort(), new FakeHistoryWriter());

        InventoryOrder result = await service.CreateAsync(
            InventoryOrderKind.Import,
            "Nhân viên kho",
            "Đơn số lượng không",
            null,
            [Input(quantity: 0, progress: 0)],
            CancellationToken.None);

        Assert.False(Assert.Single(result.ProductList).Status);
    }

    [Fact]
    public async Task UpdateImportLine_WithPartialPayload_AdjustsOnlyTrackedDelta()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import,
            Line(quantity: 10, progress: 4, applied: 2)));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);
        InventoryOrderLineUpdateInput update = Update(progress: 7);

        InventoryOrder result = await service.UpdateLineAsync(
            InventoryOrderKind.Import, OrderId, 0, update, "Nhân viên kho", CancellationToken.None);

        StockAdjustment adjustment = Assert.Single(stock.Adjustments);
        Assert.Equal(3, adjustment.QuantityForSaleDelta);
        Assert.Equal(3, adjustment.QuantityInStorageDelta);
        Assert.Equal(7, result.ProductList[0].ProgressQuantity);
        Assert.Equal(5, result.ProductList[0].StockAppliedQuantity);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.Equal(3, entry.Quantity);
        Assert.Equal("Nhập kho (cập nhật đơn nhập)", entry.Note);
        Assert.Equal("order_line_manual", entry.Source);
    }

    [Fact]
    public async Task UpdateExportLine_WhenProgressIsReduced_RestoresStockAndKeepsPricing()
    {
        InventoryOrderLine original = Line(quantity: 10, progress: 6, applied: 5) with
        {
            Price = "125000",
            ImportPriceSnapshot = "100000",
            ProfitPercent = 25,
        };
        FakeRepository repository = new(Order(InventoryOrderKind.Export, original));
        FakeStockPort stock = new();
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);

        InventoryOrder result = await service.UpdateLineAsync(
            InventoryOrderKind.Export, OrderId, 0, Update(progress: 2), "Nhân viên kho", CancellationToken.None);

        StockAdjustment adjustment = Assert.Single(stock.Adjustments);
        Assert.Equal(4, adjustment.QuantityForSaleDelta);
        Assert.Equal(4, adjustment.QuantityInStorageDelta);
        Assert.Equal(2, result.ProductList[0].ProgressQuantity);
        Assert.Equal(1, result.ProductList[0].StockAppliedQuantity);
        Assert.Equal("125000", result.ProductList[0].Price);
        StorageHistoryWriteEntry entry = Assert.Single(history.Entries);
        Assert.Equal(4, entry.Quantity);
        Assert.Equal("Hoàn kho (cập nhật đơn xuất)", entry.Note);
    }

    [Fact]
    public async Task AddExportLine_WithAiSkip_RequiresZeroStockAndDoesNotMutateStock()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export));
        FakeStockPort stock = new() { QuantityForSale = 0, QuantityInStorage = 0 };
        FakeHistoryWriter history = new();
        InventoryOrderService service = new(repository, stock, history);

        InventoryOrder result = await service.AddLineAsync(
            InventoryOrderKind.Export, OrderId,
            Input(quantity: 2, progress: 2, skip: true, isAiScan: true),
            "Nhân viên kho", CancellationToken.None);

        InventoryOrderLine line = Assert.Single(result.ProductList);
        Assert.True(line.Status);
        Assert.True(line.StockUpdateSkipped);
        Assert.Equal(0, line.StockAppliedQuantity);
        Assert.Empty(stock.Adjustments);
        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task AddExportLine_WithAiSkip_WhenProductHasStock_IsRejected()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export));
        FakeStockPort stock = new() { QuantityForSale = 1, QuantityInStorage = 1 };
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.AddLineAsync(
                InventoryOrderKind.Export, OrderId,
                Input(quantity: 1, progress: 1, skip: true, isAiScan: true),
                CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
        Assert.Empty(stock.Adjustments);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task UpdateExportLine_WithInsufficientStock_ReturnsBadRequestBeforeMutation()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export,
            Line(quantity: 5, progress: 0, applied: 0)));
        FakeStockPort stock = new() { QuantityForSale = 1, QuantityInStorage = 10 };
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpdateLineAsync(
                InventoryOrderKind.Export, OrderId, 0, Update(progress: 2), CancellationToken.None));

        Assert.Equal(400, error.Error.HttpStatus);
        Assert.Contains("Tồn khả dụng hiện có: 1", error.Error.ClientMessage, StringComparison.Ordinal);
        Assert.Empty(stock.Adjustments);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task DeleteExportLine_WithSkippedButZeroProgress_IsAllowed()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Export,
            Line(quantity: 2, progress: 0, applied: 0, skipped: true)));
        InventoryOrderService service = new(repository, new FakeStockPort(), new FakeHistoryWriter());

        InventoryOrder result = await service.DeleteLineAsync(
            InventoryOrderKind.Export, OrderId, 0, CancellationToken.None);

        Assert.Empty(result.ProductList);
    }

    [Fact]
    public async Task UpdateLine_WhenOrderCasLoses_RollsBackStockDelta()
    {
        FakeRepository repository = new(Order(InventoryOrderKind.Import,
            Line(quantity: 5, progress: 1, applied: 1))) { RejectUpdates = true };
        FakeStockPort stock = new();
        InventoryOrderService service = new(repository, stock, new FakeHistoryWriter());

        TtsApplicationException error = await Assert.ThrowsAsync<TtsApplicationException>(() =>
            service.UpdateLineAsync(
                InventoryOrderKind.Import, OrderId, 0, Update(progress: 3), CancellationToken.None));

        Assert.Equal(409, error.Error.HttpStatus);
        Assert.Single(stock.Adjustments);
        StockAdjustment rollback = Assert.Single(stock.Rollbacks);
        Assert.Equal(2, rollback.QuantityInStorageDelta);
        Assert.Equal(2, rollback.QuantityForSaleDelta);
    }

    private static InventoryOrderLineInput Input(
        int quantity,
        double progress,
        bool? skip = null,
        bool? isAiScan = null) => new(
            ProductId, "100000", null, null, "cái", quantity, progress, null, null, skip, isAiScan);

    private static InventoryOrderLineUpdateInput Update(double? progress = null) => new(
        null, null, null, null, null, null, progress, null, null);

    private static InventoryOrder Order(InventoryOrderKind kind, params InventoryOrderLine[] lines) => new(
        OrderId, "Đơn kiểm thử", string.Empty, "Nhân viên kiểm thử", lines, [], "0", false, null,
        DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-08-13T00:00:00Z", CultureInfo.InvariantCulture), 0, kind);

    private static InventoryOrderLine Line(int quantity, int progress, double? applied, bool skipped = false) => new(
        false, ProductId, "100000", null, null, "cái", quantity, progress, applied, skipped,
        null, null, SubdocumentId: "507f191e810c19729de860ec");

    private sealed class FakeRepository(InventoryOrder initial) : IInventoryOrderRepository
    {
        public bool RejectUpdates { get; init; }
        public int LastExpectedVersion { get; private set; } = -1;
        public InventoryOrder? Saved { get; private set; }

        public Task<(IReadOnlyList<InventoryOrder> Orders, long Total)> ListAsync(
            InventoryOrderKind kind, InventoryOrderListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<InventoryOrder>, long)>(([initial], 1));

        public Task<(IReadOnlyList<InventoryOrderProductSummary> Products, long Total)> ListProductsAsync(
            InventoryOrderKind kind, int page, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<InventoryOrderProductSummary>, long)>(([], 0));

        public Task<InventoryOrder?> FindAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken) =>
            Task.FromResult<InventoryOrder?>(kind == initial.Kind && id == initial.Id ? initial : null);

        public Task<InventoryOrder> InsertAsync(InventoryOrder order, CancellationToken cancellationToken) =>
            Task.FromResult(order);

        public Task<InventoryOrder?> UpdateAsync(InventoryOrder order, int expectedVersion, CancellationToken cancellationToken)
        {
            LastExpectedVersion = expectedVersion;
            if (RejectUpdates || expectedVersion != initial.Version) return Task.FromResult<InventoryOrder?>(null);
            Saved = order with { Version = expectedVersion + 1 };
            return Task.FromResult<InventoryOrder?>(Saved);
        }

        public Task<bool> DeleteAsync(InventoryOrderKind kind, string id, int expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeStockPort : IOrderStockPort
    {
        public bool RejectRollback { get; init; }
        public double QuantityForSale { get; init; } = 100;
        public double QuantityInStorage { get; init; } = 100;
        public bool IsAssignedToBranch { get; init; } = true;
        public List<StockAdjustment> Adjustments { get; } = [];
        public List<StockAdjustment> Rollbacks { get; } = [];

        public Task<IReadOnlyList<StockAdjustment>> AdjustAsync(
            IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
        {
            Adjustments.AddRange(adjustments);
            return Task.FromResult<IReadOnlyList<StockAdjustment>>(adjustments.ToArray());
        }

        public Task RollbackAsync(IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
        {
            Rollbacks.AddRange(adjustments);
            return RejectRollback
                ? Task.FromException(new InvalidOperationException("Lỗi rollback tổng hợp"))
                : Task.CompletedTask;
        }

        public Task<ProductOrderSnapshot?> GetProductAsync(
            string productId, int variantIndex, CancellationToken cancellationToken) =>
            Task.FromResult<ProductOrderSnapshot?>(new ProductOrderSnapshot(
                productId, variantIndex, VariantId, "Sản phẩm kiểm thử", "Nhãn hiệu", "SP-1", "100000", null,
                null, null, QuantityForSale, QuantityInStorage, 25, true,
                IsAssignedToBranch: IsAssignedToBranch,
                VariantName: "Mặc định"));
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
}
