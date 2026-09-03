using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Inventory;

namespace TTSmartEcom.Application.Inventory;

public sealed partial class InventoryOrderService(
    IInventoryOrderRepository orders,
    IOrderStockPort products,
    IStorageHistoryWriter storageHistory,
    ILogger<InventoryOrderService>? configuredLogger = null) : IInventoryOrderService
{
    private readonly ILogger<InventoryOrderService> logger =
        configuredLogger ?? NullLogger<InventoryOrderService>.Instance;

    public async Task<InventoryOrderListResult> ListAsync(InventoryOrderKind kind, InventoryOrderListQuery query, CancellationToken cancellationToken)
    {
        if (query.Page is < 1 or > 10_000 || query.EndDate < query.StartDate || query.EndDate - query.StartDate > TimeSpan.FromDays(366)) throw Error(400, "Invalid request");
        (IReadOnlyList<InventoryOrder> items, long total) = await orders.ListAsync(kind, query, cancellationToken);
        int count = total > int.MaxValue ? int.MaxValue : (int)total;
        return new InventoryOrderListResult(items, query.Page, (int)Math.Ceiling(count / 20d), count);
    }

    public async Task<InventoryOrderProductSummaryResult> ListProductsAsync(
        InventoryOrderKind kind, int page, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10_000) throw Error(400, "Invalid request");
        (IReadOnlyList<InventoryOrderProductSummary> items, long total) =
            await orders.ListProductsAsync(kind, page, cancellationToken);
        int count = total > int.MaxValue ? int.MaxValue : (int)total;
        return new InventoryOrderProductSummaryResult(
            items, page, (int)Math.Ceiling(count / 10d), count);
    }

    public async Task<InventoryOrder?> GetAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken)
    {
        ValidateId(id);
        return await orders.FindAsync(kind, id, cancellationToken);
    }

    public Task<InventoryOrder> CreateAsync(InventoryOrderKind kind, string userName, string? orderName, string? note, IReadOnlyList<InventoryOrderLineInput> lines, CancellationToken cancellationToken) =>
        CreateAsync(kind, userName, orderName, note, null, lines, cancellationToken);

    public async Task<InventoryOrder> CreateAsync(InventoryOrderKind kind, string userName, string? orderName, string? note, DateTimeOffset? transactionDate, IReadOnlyList<InventoryOrderLineInput> lines, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName)) userName = "Hệ thống";
        if (lines.Count > 500) throw Error(400, "productList phải là một mảng hợp lệ.");
        InventoryOrderLine[] normalized = new InventoryOrderLine[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            InventoryOrderLineInput input = lines[i];
            if (input.ProgressQuantity != 0 || input.Status == true) throw Error(400, kind == InventoryOrderKind.Import
                ? "Đơn nhập mới chỉ được chứa sản phẩm chưa phát sinh nhập kho."
                : "Đơn xuất mới chỉ được khởi tạo với số lượng đã xuất bằng 0");
            normalized[i] = await BuildNewLineAsync(
                kind,
                input with { ProgressQuantity = 0, SkipStockUpdate = false },
                cancellationToken) with { Status = false };
        }
        DateTimeOffset now = DateTimeOffset.UtcNow;
        InventoryOrder order = new(string.Empty, Limit(orderName, 200), Limit(note, 2_000), Limit(userName, 160), normalized, [], Total(normalized), false, null, now, now, 0, kind, transactionDate ?? now);
        return await orders.InsertAsync(order, cancellationToken);
    }

    public Task<InventoryOrder> UpdateMetadataAsync(InventoryOrderKind kind, string id, string? orderName, string? note, IReadOnlyList<string>? images, CancellationToken cancellationToken) =>
        UpdateMetadataAsync(kind, id, orderName, note, images, false, null, cancellationToken);

    public async Task<InventoryOrder> UpdateMetadataAsync(InventoryOrderKind kind, string id, string? orderName, string? note, IReadOnlyList<string>? images, bool updateTransactionDate, DateTimeOffset? transactionDate, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        if (images is not null && (images.Count > 20 || images.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500))) throw Error(400, "Danh sách ảnh không hợp lệ");
        InventoryOrder saved = await SaveAsync(order with
        {
            OrderName = orderName is null ? order.OrderName : Limit(orderName, 200),
            Note = note is null ? order.Note : Limit(note, 2_000),
            Images = images?.ToArray() ?? order.Images,
            TransactionDate = updateTransactionDate ? transactionDate : order.TransactionDate,
        }, cancellationToken);
        if (updateTransactionDate && transactionDate.HasValue)
        {
            await storageHistory.UpdateTransactionDateAsync(saved.Id, transactionDate.Value, cancellationToken);
        }
        return saved;
    }

    public Task<InventoryOrder> UpdateNameAsync(InventoryOrderKind kind, string id, string? orderName, string? note, CancellationToken cancellationToken) =>
        UpdateMetadataAsync(kind, id, orderName, note, null, cancellationToken);

    public async Task<InventoryOrder> SetStatusAsync(InventoryOrderKind kind, string id, bool status, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        if (status && !order.ProductList.All(IsFullyApplied)) throw Error(400, kind == InventoryOrderKind.Import
            ? "Chỉ có thể hoàn tất đơn khi tất cả sản phẩm đã nhập đủ."
            : "Chỉ có thể hoàn tất đơn khi tất cả sản phẩm đã xuất đủ.");
        return await SaveAsync(order with { Status = status, CompletedAt = status ? DateTimeOffset.UtcNow : null }, cancellationToken);
    }

    public async Task<InventoryOrder> SetLineStatusAsync(InventoryOrderKind kind, string id, int index, bool status, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        InventoryOrderLine line = RequireIndex(order, index);
        if (status && !IsFullyApplied(line)) throw Error(400, kind == InventoryOrderKind.Import ? "Hãy dùng thao tác nhập kho để hoàn tất sản phẩm." : "Hãy dùng thao tác xuất kho để hoàn tất sản phẩm.");
        List<InventoryOrderLine> lines = order.ProductList.ToList();
        lines[index] = line with { Status = status };
        return await SaveAsync(order with { ProductList = lines }, cancellationToken);
    }

    public async Task<InventoryOrder> CompleteAsync(
        InventoryOrderKind kind, string id, bool status, CancellationToken cancellationToken) =>
        await CompleteAsync(kind, id, status, null, cancellationToken);

    public async Task<InventoryOrder> CompleteAsync(
        InventoryOrderKind kind, string id, bool status, string? actorName, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        if (!status)
        {
            return await SaveAsync(order with { Status = false, CompletedAt = null }, cancellationToken);
        }

        List<InventoryOrderLine> lines = order.ProductList.ToList();
        List<StockAdjustment> adjustments = [];
        List<StorageHistoryWriteEntry> historyEntries = [];
        for (int index = 0; index < lines.Count; index++)
        {
            (InventoryOrderLine completed, StockAdjustment? adjustment, string? productName) =
                await PrepareCompletionAsync(kind, lines[index], cancellationToken);
            lines[index] = completed;
            if (adjustment is not null)
            {
                adjustments.Add(adjustment);
                historyEntries.Add(HistoryEntry(kind, order, completed, productName,
                    adjustment.QuantityInStorageDelta, actorName, "order_bulk_complete"));
            }
        }

        InventoryOrder desired = order with
        {
            ProductList = lines,
            Status = true,
            CompletedAt = DateTimeOffset.UtcNow,
        };
        InventoryOrder saved = await AdjustAndSaveAsync(desired, adjustments, cancellationToken);
        await AppendHistoryBestEffortAsync(historyEntries, cancellationToken);
        return saved;
    }

    public async Task<InventoryOrder> CompleteLineAsync(
        InventoryOrderKind kind, string id, int index, bool status, CancellationToken cancellationToken) =>
        await CompleteLineAsync(kind, id, index, status, null, cancellationToken);

    public async Task<InventoryOrder> CompleteLineAsync(
        InventoryOrderKind kind, string id, int index, bool status, string? actorName, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        InventoryOrderLine line = RequireIndex(order, index);
        if (!status || line.Status)
        {
            return order;
        }

        (InventoryOrderLine completed, StockAdjustment? adjustment, string? productName) =
            await PrepareCompletionAsync(kind, line, cancellationToken);
        List<InventoryOrderLine> lines = order.ProductList.ToList();
        lines[index] = completed;
        InventoryOrder saved = await AdjustAndSaveAsync(
            order with { ProductList = lines }, adjustment is null ? [] : [adjustment], cancellationToken);
        if (adjustment is not null)
        {
            await AppendHistoryBestEffortAsync(
                [HistoryEntry(kind, order, completed, productName, adjustment.QuantityInStorageDelta, actorName, "order_line_complete")],
                cancellationToken);
        }
        return saved;
    }

    public Task<InventoryOrder> AddLineAsync(
        InventoryOrderKind kind, string id, InventoryOrderLineInput line, CancellationToken cancellationToken) =>
        AddLineAsync(kind, id, line, null, cancellationToken);

    public async Task<InventoryOrder> AddLineAsync(
        InventoryOrderKind kind, string id, InventoryOrderLineInput line, string? actorName, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        ProductOrderSnapshot? product = null;
        bool skipStock = false;
        if (kind == InventoryOrderKind.Export && line.SkipStockUpdate == true)
        {
            if (line.IsAiScan != true) throw Error(400, "Không được phép bỏ qua cập nhật tồn kho");
            product = await RequireProductAsync(line.ProductId, cancellationToken);
            if (product.QuantityForSale != 0 || product.QuantityInStorage != 0) throw Error(400,
                "Chỉ được bỏ qua tồn kho cho sản phẩm mới do AI tạo và chưa có tồn");
            skipStock = true;
        }

        InventoryOrderLine normalized = await BuildNewLineAsync(kind, line with { SkipStockUpdate = skipStock }, cancellationToken, product);
        double stockDelta = skipStock ? 0 : normalized.ProgressQuantity;
        StockAdjustment? adjustment = null;
        if (stockDelta != 0)
        {
            product ??= await RequireProductAsync(normalized.ProductId!, cancellationToken);
            adjustment = StockMovement(kind, product, stockDelta);
            EnsureAvailableForDecrease(product, adjustment);
        }
        InventoryOrderLine[] lines = [.. order.ProductList, normalized];
        InventoryOrder saved = await AdjustAndSaveAsync(
            order with { ProductList = lines, Total = Total(lines) },
            adjustment is null ? [] : [adjustment], cancellationToken);
        if (adjustment is not null)
        {
            await AppendHistoryBestEffortAsync(
                [ManualHistoryEntry(kind, order, normalized, product!, adjustment.QuantityInStorageDelta, actorName, isUpdate: false, line.IsAiScan == true)],
                cancellationToken);
        }
        return saved;
    }

    public Task<InventoryOrder> UpdateLineAsync(
        InventoryOrderKind kind, string id, int index, InventoryOrderLineUpdateInput line, CancellationToken cancellationToken) =>
        UpdateLineAsync(kind, id, index, line, null, cancellationToken);

    public async Task<InventoryOrder> UpdateLineAsync(
        InventoryOrderKind kind, string id, int index, InventoryOrderLineUpdateInput line, string? actorName, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        InventoryOrderLine current = RequireIndex(order, index);
        if (line.ProductId is not null && !string.Equals(current.ProductId, line.ProductId, StringComparison.Ordinal)) throw Error(400, kind == InventoryOrderKind.Import
            ? "Không thể thay đổi sản phẩm của một dòng đơn nhập hiện có."
            : "Không được đổi sản phẩm của dòng đã tạo; hãy xóa dòng cũ và thêm dòng mới");
        if (!MongoId(current.ProductId)) throw Error(400, "Mã sản phẩm không hợp lệ.");

        int targetQuantity = line.Quantity ?? current.Quantity;
        double targetProgress = line.ProgressQuantity ?? current.ProgressQuantity;
        ValidateQuantities(kind, targetQuantity, targetProgress);
        double currentApplied = AppliedChecked(kind, current);
        double untracked = Math.Max(0, current.ProgressQuantity - currentApplied);
        double targetApplied = Math.Max(0, targetProgress - untracked);
        double stockDelta = targetApplied - currentApplied;

        bool needsPricingProduct = kind == InventoryOrderKind.Export &&
            (string.IsNullOrWhiteSpace(current.ImportPriceSnapshot) || !current.ProfitPercent.HasValue);
        ProductOrderSnapshot? product = stockDelta != 0 || needsPricingProduct
            ? await RequireProductAsync(current.ProductId!, cancellationToken)
            : null;
        (string? price, string? importPrice, double? profit) = kind == InventoryOrderKind.Export
            ? ResolveUpdatedExportPricing(current, line, product)
            : (line.Price ?? current.Price, current.ImportPriceSnapshot, current.ProfitPercent);
        InventoryOrderLine replacement = current with
        {
            Status = targetProgress == targetQuantity,
            Price = LimitNullable(price, 100),
            ImportPriceSnapshot = LimitNullable(importPrice, 100),
            ProfitPercent = profit,
            Unit = line.Unit is null ? current.Unit : LimitNullable(line.Unit, 100),
            Quantity = targetQuantity,
            ProgressQuantity = targetProgress,
            StockAppliedQuantity = targetApplied,
            Note = line.Note is null ? current.Note : LimitNullable(line.Note, 2_000),
            Vat = line.Vat is null ? current.Vat : LimitNullable(line.Vat, 100),
        };
        StockAdjustment? adjustment = stockDelta == 0 ? null : StockMovement(kind, product!, stockDelta);
        if (adjustment is not null) EnsureAvailableForDecrease(product!, adjustment);
        List<InventoryOrderLine> lines = order.ProductList.ToList();
        lines[index] = replacement;
        InventoryOrder saved = await AdjustAndSaveAsync(
            order with { ProductList = lines, Total = Total(lines) },
            adjustment is null ? [] : [adjustment], cancellationToken);
        if (adjustment is not null)
        {
            await AppendHistoryBestEffortAsync(
                [ManualHistoryEntry(kind, order, replacement, product!, adjustment.QuantityInStorageDelta, actorName, isUpdate: true, line.IsAiScan == true, line.QuantityAdjustment == true, current.ProgressQuantity, targetProgress)],
                cancellationToken);
        }
        return saved;
    }

    public async Task<InventoryOrder> DeleteLineAsync(InventoryOrderKind kind, string id, int index, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        InventoryOrderLine current = RequireIndex(order, index);
        if (current.ProgressQuantity > 0 || Applied(current) > 0) throw Error(400, kind == InventoryOrderKind.Import
            ? "Không thể xóa sản phẩm đã phát sinh nhập kho. Hãy điều chỉnh số lượng nhập về 0 trước."
            : "Không thể xóa sản phẩm đã phát sinh xuất kho. Hãy hoàn số lượng xuất về 0 trước.");
        List<InventoryOrderLine> lines = order.ProductList.ToList();
        lines.RemoveAt(index);
        return await SaveAsync(order with { ProductList = lines, Total = Total(lines) }, cancellationToken);
    }

    public async Task<InventoryOrder> ReorderLinesAsync(InventoryOrderKind kind, string id, IReadOnlyList<InventoryOrderLineInput> lines, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        if (lines.Count != order.ProductList.Count || lines.Count > 500) throw Error(400, "productList must be an array");
        List<InventoryOrderLine> result = [];
        List<InventoryOrderLine> remaining = order.ProductList.ToList();
        foreach (InventoryOrderLineInput input in lines)
        {
            int index = remaining.FindIndex(x => SameLine(x, input));
            if (index < 0) throw Error(400, "API sắp xếp chỉ được thay đổi thứ tự sản phẩm.");
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return await SaveAsync(order with { ProductList = result }, cancellationToken);
    }

    public async Task<bool> DeleteAsync(InventoryOrderKind kind, string id, CancellationToken cancellationToken)
    {
        InventoryOrder order = await RequireAsync(kind, id, cancellationToken);
        if (order.ProductList.Any(x => x.ProgressQuantity > 0 || Applied(x) > 0)) throw Error(400, kind == InventoryOrderKind.Import
            ? "Không thể xóa đơn đã phát sinh nhập kho. Hãy hoàn tác các dòng nhập trước."
            : "Không thể xóa đơn đã phát sinh xuất kho. Hãy hoàn tác các dòng xuất trước.");
        bool deleted = await orders.DeleteAsync(kind, id, order.Version, cancellationToken);
        if (!deleted) throw Error(409, "Đơn vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
        return true;
    }

    private async Task<InventoryOrderLine> BuildNewLineAsync(
        InventoryOrderKind kind,
        InventoryOrderLineInput input,
        CancellationToken cancellationToken,
        ProductOrderSnapshot? loadedProduct = null)
    {
        if (!MongoId(input.ProductId)) throw Error(400, "Mã sản phẩm không hợp lệ.");
        ValidateQuantities(kind, input.Quantity, input.ProgressQuantity);
        string? price = input.Price;
        string? importPrice = null;
        double? profit = null;
        if (kind == InventoryOrderKind.Export)
        {
            ProductOrderSnapshot product = loadedProduct ?? await RequireProductAsync(input.ProductId, cancellationToken);
            (price, importPrice, profit) = ResolveNewExportPricing(input, product);
        }
        bool skipStockUpdate = kind == InventoryOrderKind.Export && input.SkipStockUpdate == true;
        return new InventoryOrderLine(
            input.ProgressQuantity == input.Quantity,
            input.ProductId,
            LimitNullable(price, 100),
            LimitNullable(importPrice, 100),
            profit,
            LimitNullable(input.Unit, 100),
            input.Quantity,
            input.ProgressQuantity,
            skipStockUpdate ? 0 : input.ProgressQuantity,
            skipStockUpdate,
            LimitNullable(input.Note, 2_000),
            LimitNullable(input.Vat, 100),
            SubdocumentId: null);
    }

    private async Task<(InventoryOrderLine Line, StockAdjustment? Adjustment, string? ProductName)> PrepareCompletionAsync(
        InventoryOrderKind kind, InventoryOrderLine line, CancellationToken cancellationToken)
    {
        if (!MongoId(line.ProductId)) throw Error(400, "Mã sản phẩm không hợp lệ.");
        if (line.Quantity < 0 || (kind == InventoryOrderKind.Export && line.Quantity == 0)) throw Error(400, kind == InventoryOrderKind.Import
            ? "Số lượng nhập còn lại không hợp lệ."
            : "Số lượng cần xuất phải là số nguyên lớn hơn 0");
        if (line.ProgressQuantity < 0 || line.ProgressQuantity > line.Quantity) throw Error(400, kind == InventoryOrderKind.Import
            ? "Số lượng nhập còn lại không hợp lệ."
            : "Số lượng đã xuất phải nằm trong khoảng từ 0 đến số lượng cần xuất");

        double applied = Applied(line);
        if (!double.IsFinite(applied) || applied < 0 || applied > line.ProgressQuantity) throw Error(409, kind == InventoryOrderKind.Import
            ? "Dữ liệu số lượng đã cộng kho của dòng nhập không hợp lệ."
            : "Dữ liệu số lượng đã trừ kho của dòng xuất không hợp lệ");

        ProductOrderSnapshot product = await products.GetProductAsync(line.ProductId!, 0, cancellationToken)
            ?? throw Error(404, $"Product {line.ProductId} not found");
        if (string.IsNullOrWhiteSpace(product.VariantId)) throw Error(400, "Sản phẩm không có biến thể");

        double targetApplied;
        double required;
        if (kind == InventoryOrderKind.Import)
        {
            targetApplied = line.Quantity;
            required = targetApplied - applied;
        }
        else
        {
            double untracked = Math.Max(0, line.ProgressQuantity - applied);
            targetApplied = Math.Max(0, line.Quantity - untracked);
            required = targetApplied - applied;
        }
        if (!double.IsFinite(required) || required < 0) throw Error(400, kind == InventoryOrderKind.Import
            ? "Số lượng nhập còn lại không hợp lệ."
            : "Số lượng xuất còn lại không hợp lệ.");
        if (kind == InventoryOrderKind.Export && required > product.QuantityForSale) throw Error(400,
            $"Không đủ hàng để bán cho sản phẩm {product.Name}. Tồn khả dụng hiện có: {product.QuantityForSale.ToString(CultureInfo.InvariantCulture)}.");
        if (kind == InventoryOrderKind.Export && required > product.QuantityInStorage) throw Error(400,
            $"Không đủ tồn kho vật lý cho sản phẩm {product.Name}. Tồn hiện có: {product.QuantityInStorage.ToString(CultureInfo.InvariantCulture)}.");

        InventoryOrderLine completed = line with
        {
            Status = true,
            ProgressQuantity = line.Quantity,
            StockAppliedQuantity = targetApplied,
        };
        if (required == 0) return (completed, null, product.Name);
        double delta = kind == InventoryOrderKind.Import ? required : -required;
        return (completed, new StockAdjustment(line.ProductId!, 0, delta, delta, ExpectedVariantId: product.VariantId, RequireActiveAssignment: false), product.Name);
    }

    private async Task<InventoryOrder> AdjustAndSaveAsync(
        InventoryOrder order, IReadOnlyList<StockAdjustment> adjustments, CancellationToken cancellationToken)
    {
        IReadOnlyList<StockAdjustment> applied = await products.AdjustAsync(adjustments, cancellationToken);
        try
        {
            return await SaveAsync(order, cancellationToken);
        }
        catch (Exception original)
        {
            try
            {
                await products.RollbackAsync(applied, cancellationToken);
            }
            catch (Exception rollback)
            {
                throw new TTSmartEcom.Application.Common.Errors.ApplicationException(
                    new ApplicationError("TTS-INVORDER-ROLLBACK", 4999, 500, "Không thể hoàn tác đầy đủ thay đổi tồn kho."),
                    new AggregateException(original, rollback));
            }
            throw;
        }
    }

    private async Task AppendHistoryBestEffortAsync(
        IReadOnlyList<StorageHistoryWriteEntry> entries, CancellationToken cancellationToken)
    {
        foreach (StorageHistoryWriteEntry entry in entries)
        {
            try
            {
                await storageHistory.AppendAsync(entry, cancellationToken);
            }
            catch (Exception exception)
            {
                LogStorageHistoryFailure(
                    logger,
                    entry.Source ?? "unknown",
                    exception.GetType().Name);
            }
        }
    }

    [LoggerMessage(
        EventId = 4591,
        Level = LogLevel.Warning,
        Message = "Inventory storage-history persistence failed for source {Source} with {ErrorType}")]
    private static partial void LogStorageHistoryFailure(
        ILogger logger,
        string source,
        string errorType);

    private static void ValidateQuantities(InventoryOrderKind kind, int quantity, double progress)
    {
        if (kind == InventoryOrderKind.Import && quantity < 0) throw Error(400,
            "Số lượng đặt phải là số nguyên lớn hơn hoặc bằng 0.");
        if (kind == InventoryOrderKind.Export && quantity <= 0) throw Error(400,
            "Số lượng cần xuất phải là số nguyên lớn hơn 0");
        if (!double.IsFinite(progress) || progress < 0 || progress > quantity ||
            (kind == InventoryOrderKind.Export && progress != Math.Truncate(progress))) throw Error(400,
            kind == InventoryOrderKind.Import
                ? "Số lượng đã nhập phải nằm trong khoảng từ 0 đến số lượng đặt."
                : "Số lượng đã xuất phải nằm trong khoảng từ 0 đến số lượng cần xuất");
    }

    private async Task<ProductOrderSnapshot> RequireProductAsync(string productId, CancellationToken cancellationToken)
    {
        ProductOrderSnapshot product = await products.GetProductAsync(productId, 0, cancellationToken)
            ?? throw Error(404, $"Product {productId} not found");
        if (!product.IsAssignedToBranch) throw Error(403, "Sản phẩm chưa được phân phối cho chi nhánh hiện tại.");
        if (string.IsNullOrWhiteSpace(product.VariantId)) throw Error(400, "Sản phẩm không có biến thể");
        return product;
    }

    private static StockAdjustment StockMovement(
        InventoryOrderKind kind, ProductOrderSnapshot product, double progressDelta)
    {
        double stockDelta = kind == InventoryOrderKind.Import ? progressDelta : -progressDelta;
        return new StockAdjustment(
            product.ProductId,
            product.VariantIndex,
            stockDelta,
            stockDelta,
            ExpectedVariantId: product.VariantId);
    }

    private static void EnsureAvailableForDecrease(
        ProductOrderSnapshot product, StockAdjustment adjustment)
    {
        if (adjustment.QuantityForSaleDelta < 0 &&
            product.QuantityForSale < Math.Abs(adjustment.QuantityForSaleDelta)) throw Error(400,
            $"Không đủ hàng để bán cho sản phẩm {product.Name}. Tồn khả dụng hiện có: {product.QuantityForSale.ToString(CultureInfo.InvariantCulture)}.");
        if (adjustment.QuantityInStorageDelta < 0 &&
            product.QuantityInStorage < Math.Abs(adjustment.QuantityInStorageDelta)) throw Error(400,
            $"Không đủ tồn kho vật lý cho sản phẩm {product.Name}. Tồn hiện có: {product.QuantityInStorage.ToString(CultureInfo.InvariantCulture)}.");
    }

    private static StorageHistoryWriteEntry ManualHistoryEntry(
        InventoryOrderKind kind,
        InventoryOrder order,
        InventoryOrderLine line,
        ProductOrderSnapshot product,
        double quantity,
        string? actorName,
        bool isUpdate,
        bool isAiScan,
        bool isQuantityAdjustment = false,
        double? quantityBefore = null,
        double? quantityAfter = null)
    {
        string note = kind switch
        {
            InventoryOrderKind.Import when !isUpdate && isAiScan => "Nhập kho (AI scan đơn nhập)",
            InventoryOrderKind.Import when !isUpdate => "Nhập kho (thêm sản phẩm đơn nhập)",
            InventoryOrderKind.Import when quantity > 0 => "Nhập kho (cập nhật đơn nhập)",
            InventoryOrderKind.Import => "Điều chỉnh giảm nhập kho",
            InventoryOrderKind.Export when !isUpdate && isAiScan => "Xuất kho (AI scan đơn xuất)",
            InventoryOrderKind.Export when !isUpdate => "Xuất kho (thêm sản phẩm đơn xuất)",
            InventoryOrderKind.Export when quantity < 0 => "Xuất kho (cập nhật đơn xuất)",
            _ => "Hoàn kho (cập nhật đơn xuất)",
        };
        StorageHistoryWriteEntry entry = new(
            line.ProductId!,
            product.Name ?? line.Name ?? string.Empty,
            quantity,
            actorName ?? order.UserName,
            order.Id,
            order.OrderName,
            note,
            isAiScan,
            isAiScan ? null : "order_line_manual",
            order.TransactionDate);
        return kind == InventoryOrderKind.Import && isQuantityAdjustment
            ? entry with
            {
                Note = $"Sửa số lượng nhập: {quantityBefore} → {quantityAfter}",
                Source = "import_quantity_adjustment",
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter,
            }
            : entry;
    }

    private static (string Price, string ImportPrice, double Profit) ResolveNewExportPricing(
        InventoryOrderLineInput input, ProductOrderSnapshot product)
    {
        double profit = NormalizeProfit(input.ProfitPercent ?? product.Earn);
        bool hasSnapshot = !string.IsNullOrWhiteSpace(input.ImportPriceSnapshot);
        if (hasSnapshot)
        {
            decimal import = ParsePrice(input.ImportPriceSnapshot);
            return (CalculateExportPrice(import, profit), FormatPrice(import), profit);
        }

        decimal productImport = ParsePrice(product.ImportPrice);
        if (productImport > 0 || input.Price is null)
        {
            return (CalculateExportPrice(productImport, profit), FormatPrice(productImport), profit);
        }

        decimal legacyPrice = ParsePrice(input.Price);
        decimal derivedImport = DivideByProfit(legacyPrice, profit);
        return (FormatPrice(legacyPrice), FormatPrice(derivedImport), profit);
    }

    private static (string Price, string ImportPrice, double Profit) ResolveUpdatedExportPricing(
        InventoryOrderLine current, InventoryOrderLineUpdateInput update, ProductOrderSnapshot? product)
    {
        double baseProfit = NormalizeProfit(current.ProfitPercent ?? product?.Earn ?? 0);
        decimal baseImport;
        if (!string.IsNullOrWhiteSpace(current.ImportPriceSnapshot))
        {
            baseImport = ParsePrice(current.ImportPriceSnapshot);
        }
        else
        {
            decimal productImport = ParsePrice(product?.ImportPrice);
            baseImport = productImport > 0 ? productImport : DivideByProfit(ParsePrice(current.Price), baseProfit);
        }

        if (update.ProfitPercent.HasValue)
        {
            double profit = NormalizeProfit(update.ProfitPercent.Value);
            return (CalculateExportPrice(baseImport, profit), FormatPrice(baseImport), profit);
        }
        if (update.Price is not null)
        {
            decimal requested = ParsePrice(update.Price);
            if (baseImport <= 0)
            {
                decimal derived = DivideByProfit(requested, baseProfit);
                return (FormatPrice(requested), FormatPrice(derived), baseProfit);
            }
            double profit = NormalizeProfit((double)((requested / baseImport - 1) * 100));
            return (CalculateExportPrice(baseImport, profit), FormatPrice(baseImport), profit);
        }
        return (string.IsNullOrWhiteSpace(current.Price) ? CalculateExportPrice(baseImport, baseProfit) : FormatPrice(ParsePrice(current.Price)), FormatPrice(baseImport), baseProfit);
    }

    private static double NormalizeProfit(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 100) throw Error(400,
            "% lợi nhuận phải nằm trong khoảng từ 0 đến 100");
        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal DivideByProfit(decimal price, double profit) =>
        Math.Round(price / (1 + (decimal)profit / 100), 0, MidpointRounding.AwayFromZero);

    private static string CalculateExportPrice(decimal importPrice, double profit) =>
        FormatPrice(importPrice * (1 + (decimal)profit / 100));

    private static string FormatPrice(decimal value) =>
        Math.Max(0, Math.Round(value, 0, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);

    private static StorageHistoryWriteEntry HistoryEntry(
        InventoryOrderKind kind,
        InventoryOrder order,
        InventoryOrderLine line,
        string? productName,
        double quantity,
        string? actorName,
        string source) => new(
            line.ProductId!,
            productName ?? line.Name ?? string.Empty,
            quantity,
            actorName ?? order.UserName,
            order.Id,
            order.OrderName,
            kind == InventoryOrderKind.Import
                ? "Nhập kho (đơn nhập hoàn thành)"
                : "Xuất kho (đơn xuất hoàn thành)",
            Source: source,
            TransactionDate: order.TransactionDate);

    private async Task<InventoryOrder> RequireAsync(InventoryOrderKind kind, string id, CancellationToken ct) => await GetAsync(kind, id, ct) ?? throw Error(404, "Order not found");
    private async Task<InventoryOrder> SaveAsync(InventoryOrder order, CancellationToken ct) => await orders.UpdateAsync(order, order.Version, ct) ?? throw Error(409, "Đơn vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
    private static InventoryOrderLine RequireIndex(InventoryOrder order, int index) => index < 0 || index >= order.ProductList.Count ? throw Error(400, "Invalid product index") : order.ProductList[index];
    private static double Applied(InventoryOrderLine line) => line.StockAppliedQuantity ?? line.ProgressQuantity;
    private static double AppliedChecked(InventoryOrderKind kind, InventoryOrderLine line)
    {
        double applied = Applied(line);
        if (!double.IsFinite(applied) || applied < 0 || applied > line.ProgressQuantity) throw Error(409,
            kind == InventoryOrderKind.Import
                ? "Dữ liệu số lượng đã cộng kho của dòng nhập không hợp lệ."
                : "Dữ liệu số lượng đã trừ kho của dòng xuất không hợp lệ");
        return applied;
    }
    private static bool IsFullyApplied(InventoryOrderLine line) => line.ProgressQuantity == line.Quantity && (Applied(line) == line.Quantity || line.StockUpdateSkipped);
    private static bool SameLine(InventoryOrderLine current, InventoryOrderLineInput input) => current.ProductId == input.ProductId && current.Price == (input.Price ?? current.Price) && current.ImportPriceSnapshot == (input.ImportPriceSnapshot ?? current.ImportPriceSnapshot) && current.Unit == input.Unit && current.Quantity == input.Quantity && current.ProgressQuantity == input.ProgressQuantity && current.Note == input.Note && current.Vat == input.Vat;
    private static string Total(IEnumerable<InventoryOrderLine> lines) => lines.Sum(x => ParsePrice(x.Price) * x.Quantity).ToString(CultureInfo.InvariantCulture);
    private static decimal ParsePrice(string? value) => decimal.TryParse((value ?? "0").Replace(".", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) ? n : 0;
    private static void ValidateId(string id) { if (!MongoId(id)) throw Error(400, "Invalid request"); }
    private static bool MongoId(string? id) => id is { Length: 24 } && id.All(Uri.IsHexDigit);
    private static string Limit(string? value, int max) => (value ?? string.Empty).Trim()[..Math.Min((value ?? string.Empty).Trim().Length, max)];
    private static string? LimitNullable(string? value, int max) => value is null ? null : Limit(value, max);
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) => new(new ApplicationError($"TTS-INVORDER-{status}", 4500 + status, status, message));
}
