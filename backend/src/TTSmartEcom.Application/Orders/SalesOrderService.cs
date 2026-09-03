using System.Globalization;
using Microsoft.Extensions.Logging;
using TTSmartEcom.Application.Cart;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Orders;

namespace TTSmartEcom.Application.Orders;

public sealed partial class SalesOrderService(
    IOrderRepository orders,
    IOrderStockPort stock,
    ICartRepository carts,
    ICartProductCatalog cartCatalog,
    IStationRepository stations,
    IStorageHistoryWriter storageHistory,
    ICustomerOrderNotificationScheduler notifications,
    ILogger<SalesOrderService> logger) : IOrderService
{
    private static readonly HashSet<string> AllowedStatuses = ["Processing", "Delivering", "Completed"];

    public async Task<OrderListResult> ListAdminAsync(SalesOrderListQuery query, CancellationToken cancellationToken)
    {
        ValidateListQuery(query);
        (IReadOnlyList<SalesOrder> items, long total) = await orders.ListAsync(query, cancellationToken);
        int safeTotal = total > int.MaxValue ? int.MaxValue : (int)total;
        return new OrderListResult(items, safeTotal, query.Page, (int)Math.Ceiling(safeTotal / (double)query.Limit));
    }

    public async Task<OrderListResult> ListUserAsync(string userPhone, string? state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userPhone)) throw Error(400, "Số điện thoại người dùng không được cung cấp");
        if (state is not null && state is not "Processing" and not "Delivering" and not "Completed" and not "Cancelled") throw Error(400, "Invalid state value");
        IReadOnlyList<SalesOrder> items = await orders.ListByPhoneAsync(userPhone, state, cancellationToken);
        return new OrderListResult(items, items.Count, 1, items.Count == 0 ? 0 : 1,
            items.Count == 0 ? "Không tìm thấy đơn hàng cho số điện thoại này" : "Danh sách đơn hàng");
    }

    public Task<SalesOrder?> GetAsync(string id, CancellationToken cancellationToken) => FindValidatedAsync(id, cancellationToken);
    public Task<SalesOrder?> GetAdminAsync(string id, CancellationToken cancellationToken) => FindValidatedAsync(id, cancellationToken);

    public async Task<int> ProcessingCountAsync(CancellationToken cancellationToken)
    {
        long count = await orders.CountProcessingAsync(cancellationToken);
        return count > int.MaxValue ? int.MaxValue : (int)count;
    }

    public async Task<SalesOrder> CreateCustomerAsync(string userId, CustomerOrderCreate request, CancellationToken cancellationToken)
    {
        CartOwner owner = await carts.FindOwnerAsync(userId, cancellationToken) ?? throw Error(404, "Không tìm thấy người dùng.");
        IReadOnlySet<string>? stationProducts = null;
        TTSmartEcom.Domain.Stations.Station? selectedStation = null;
        if (owner.Role == "customer" && !string.IsNullOrWhiteSpace(request.StationCode))
        {
            selectedStation = await stations.FindByCodeAsync(request.StationCode.Trim(), false, cancellationToken);
            if (selectedStation is null) throw Error(404, "Không tìm thấy trạm được chọn.");
            if (owner.StationIds.Count > 0 && !owner.StationIds.Contains(selectedStation.Id, StringComparer.Ordinal)) throw Error(403, "Bạn không có quyền truy cập trạm này.");
            stationProducts = selectedStation.ProductIds.ToHashSet(StringComparer.Ordinal);
        }
        PreparedItems prepared = await PrepareItemsAsync(request.CartItems, owner.Role == "customer" ? owner : null, stationProducts, cancellationToken);
        IReadOnlyList<StockAdjustment> applied = await stock.AdjustAsync(prepared.Reservations, cancellationToken);
        SalesOrder saved;
        try
        {
            saved = await InsertAsync(owner.Phone, owner.Name, prepared.Items, prepared.Total, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }

        try
        {
            HashSet<(string ProductId, int VariantIndex)> ordered = prepared.Items.Select(x => (x.ProductId, x.VariantIndex)).ToHashSet();
            IReadOnlyList<TTSmartEcom.Domain.Cart.CartItem> remaining = owner.Items.Where(x => !ordered.Contains((x.ProductId, x.VariantIndex))).ToArray();
            await carts.UpdateAfterCustomerOrderAsync(
                owner.Id,
                remaining,
                selectedStation?.Id,
                owner.Version,
                cancellationToken);
        }
        catch (Exception exception)
        {
            LogPostCommitFailure(logger, "cart_station_cleanup", exception.GetType().Name);
        }

        (string? stationNames, string? stationCodes) = await ResolveNotificationStationsBestEffortAsync(
            owner,
            selectedStation,
            cancellationToken);
        try
        {
            notifications.TrySchedule(new CustomerOrderNotification(
                saved.OrderCode ?? saved.Id,
                saved.UserPhone,
                saved.UserName,
                saved.Total,
                saved.CreatedAt ?? DateTimeOffset.UtcNow,
                stationNames,
                stationCodes));
        }
        catch (Exception exception)
        {
            LogPostCommitFailure(logger, "notification_schedule", exception.GetType().Name);
        }
        return saved;
    }

    public async Task<SalesOrder> CreateAdminAsync(AdminOrderCreate request, CancellationToken cancellationToken)
    {
        if (!ValidPhone(request.UserPhone)) throw Error(400, "Số điện thoại không hợp lệ");
        PreparedItems prepared = await PrepareItemsAsync(request.Items, null, null, cancellationToken);
        IReadOnlyList<StockAdjustment> applied = await stock.AdjustAsync(prepared.Reservations, cancellationToken);
        try
        {
            return await InsertAsync(request.UserPhone, request.UserName, prepared.Items, prepared.Total, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
    }

    public async Task<SalesOrder> CreateDraftAsync(CancellationToken cancellationToken) => await InsertAsync(string.Empty, string.Empty, [], 0, cancellationToken);

    public async Task<SalesOrder> CancelAsync(string id, string requesterId, string? requesterPhone, bool isAdmin, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireOrderAsync(id, cancellationToken);
        EnsureOwner(order, requesterPhone, isAdmin, "hủy");
        if (order.Status == "Completed") throw Error(400, "Không thể hủy đơn hàng đã hoàn thành.");
        if (order.State == "Cancelled") throw Error(400, "Order is already cancelled.");
        IReadOnlyList<StockAdjustment> releases = await BuildAdjustmentsAsync(order.CartItems, +1, 0, 0, cancellationToken, skipMissing: true);
        IReadOnlyList<StockAdjustment> applied = await stock.AdjustAsync(releases, cancellationToken);
        try
        {
            return await SaveAsync(order with { State = "Cancelled" }, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id, string requesterId, string? requesterPhone, bool isAdmin, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireOrderAsync(id, cancellationToken);
        EnsureOwner(order, requesterPhone, isAdmin, "xóa");
        if (order.Status == "Completed") throw Error(400, "Không thể xóa đơn hàng đã hoàn thành.");
        IReadOnlyList<StockAdjustment> applied = [];
        if (order.State != "Cancelled")
        {
            IReadOnlyList<StockAdjustment> release = await BuildAdjustmentsAsync(order.CartItems, +1, 0, 0, cancellationToken, skipMissing: true);
            applied = await stock.AdjustAsync(release, cancellationToken);
        }
        bool deleted = await orders.DeleteAsync(order.Id, order.Version, cancellationToken);
        if (!deleted)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw Error(409, "Đơn hàng vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
        }
        return true;
    }

    public async Task<SalesOrder> UpdateFieldAsync(
        string id, string field, object? value, CancellationToken cancellationToken) =>
        await UpdateFieldAsync(id, field, value, null, cancellationToken);

    public async Task<SalesOrder> UpdateFieldAsync(
        string id, string field, object? value, string? actorName, CancellationToken cancellationToken)
    {
        if (field is not "status" and not "payment") throw Error(400, "Invalid field");
        SalesOrder order = await RequireOrderAsync(id, cancellationToken);
        if (field == "payment")
        {
            if (!TryBoolean(value, out bool payment)) throw Error(400, "Invalid payment value");
            return await SaveAsync(order with { Payment = payment }, cancellationToken);
        }
        string status = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (!AllowedStatuses.Contains(status)) throw Error(400, "Invalid status value");
        if (status == "Completed" && order.State == "Cancelled") throw Error(400, "Không thể hoàn thành đơn hàng đã bị hủy.");
        if (status == "Completed" && !ValidPhone(order.UserPhone)) throw Error(400, "Vui lòng nhập số điện thoại hợp lệ trước khi hoàn thành đơn.");

        IReadOnlyList<StockAdjustment> adjustments = [];
        IReadOnlyList<StorageHistoryWriteEntry> historyEntries = [];
        if (order.Status != status && status == "Completed")
        {
            (adjustments, historyEntries) = await BuildStatusTransitionAsync(
                order, 0, -1, +1, -1, "Đơn hàng bán online", "online_sale", actorName, cancellationToken);
        }
        else if (order.Status == "Completed" && status != "Completed")
        {
            (adjustments, historyEntries) = await BuildStatusTransitionAsync(
                order, 0, +1, -1, +1, "Hoàn tác đơn bán online", "online_sale_revert", actorName, cancellationToken);
        }
        IReadOnlyList<StockAdjustment> applied = await stock.AdjustAsync(adjustments, cancellationToken);
        SalesOrder saved;
        try
        {
            saved = await SaveAsync(order with { Status = status, CompletedAt = status == "Completed" ? DateTimeOffset.UtcNow : null }, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
        await AppendHistoryBestEffortAsync(historyEntries, cancellationToken);
        return saved;
    }

    public async Task<SalesOrder> AddItemAsync(string id, SalesOrderItem item, CancellationToken cancellationToken)
    {
        ValidateItem(item);
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        PreparedItems prepared = await PrepareItemsAsync([item], null, null, cancellationToken);
        IReadOnlyList<StockAdjustment> applied = await stock.AdjustAsync(prepared.Reservations, cancellationToken);
        try
        {
            SalesOrderItem[] items = [.. order.CartItems, item];
            return await SaveAsync(order with { CartItems = items, Total = await ComputeTotalAsync(items, cancellationToken) }, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
    }

    public async Task<SalesOrder> UpdateItemAsync(string id, int index, int quantity, CancellationToken cancellationToken)
    {
        if (quantity <= 0) throw Error(400, "Số lượng không hợp lệ");
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        SalesOrderItem line = RequireIndex(order, index);
        int delta = quantity - line.Quantity;
        ProductOrderSnapshot? product = await stock.GetProductAsync(line.ProductId, line.VariantIndex, cancellationToken);
        if (product is null) throw Error(404, "Không tìm thấy sản phẩm trong đơn hàng.");
        if (delta > 0 && !product.IsAssignedToBranch) throw Error(403, "Sản phẩm đã bị thu hồi khỏi chi nhánh.");
        IReadOnlyList<StockAdjustment> applied = delta == 0 ? [] : await stock.AdjustAsync([new StockAdjustment(line.ProductId, line.VariantIndex, -delta, 0, 0, product.VariantId, RequireActiveAssignment: delta > 0)], cancellationToken);
        try
        {
            List<SalesOrderItem> items = order.CartItems.ToList();
            items[index] = line with { Quantity = quantity };
            return await SaveAsync(order with { CartItems = items, Total = await ComputeTotalAsync(items, cancellationToken) }, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
    }

    public async Task<SalesOrder> DeleteItemAsync(string id, int index, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        SalesOrderItem line = RequireIndex(order, index);
        ProductOrderSnapshot? product = await stock.GetProductAsync(line.ProductId, line.VariantIndex, cancellationToken);
        IReadOnlyList<StockAdjustment> applied = product is null ? [] : await stock.AdjustAsync([new StockAdjustment(line.ProductId, line.VariantIndex, line.Quantity, 0, 0, product.VariantId, RequireActiveAssignment: false)], cancellationToken);
        try
        {
            List<SalesOrderItem> items = order.CartItems.ToList();
            items.RemoveAt(index);
            return await SaveAsync(order with { CartItems = items, Total = await ComputeTotalAsync(items, cancellationToken) }, cancellationToken);
        }
        catch (Exception)
        {
            await stock.RollbackAsync(applied, cancellationToken);
            throw;
        }
    }

    public async Task<SalesOrder> ReorderItemsAsync(string id, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        if (items.Count != order.CartItems.Count || items.Count > 500) throw Error(400, "Danh sách sắp xếp không hợp lệ");
        string[] original = order.CartItems.Select(Key).Order(StringComparer.Ordinal).ToArray();
        string[] requested = items.Select(Key).Order(StringComparer.Ordinal).ToArray();
        if (!original.SequenceEqual(requested, StringComparer.Ordinal)) throw Error(400, "Danh sách sắp xếp không khớp đơn hàng");
        Dictionary<string, Queue<SalesOrderItem>> existing = order.CartItems
            .GroupBy(Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new Queue<SalesOrderItem>(group), StringComparer.Ordinal);
        SalesOrderItem[] reordered = items.Select(item => existing[Key(item)].Dequeue()).ToArray();
        return await SaveAsync(order with { CartItems = reordered }, cancellationToken);
    }

    public async Task<SalesOrder> UpdateCustomerAsync(string id, string? userName, string? userPhone, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        if (userPhone is not null && !ValidPhone(userPhone)) throw Error(400, "Số điện thoại không hợp lệ");
        return await SaveAsync(order with { UserName = userName ?? order.UserName, UserPhone = userPhone ?? order.UserPhone }, cancellationToken);
    }

    public async Task<SalesOrder> UpdateImagesAsync(string id, IReadOnlyList<string> images, CancellationToken cancellationToken)
    {
        SalesOrder order = await RequireEditableAsync(id, cancellationToken);
        if (images.Count > 20 || images.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 500)) throw Error(400, "Danh sách ảnh không hợp lệ");
        return await SaveAsync(order with { Images = images.ToArray() }, cancellationToken);
    }

    public async Task<IReadOnlyList<SalesOrderItemDetail>> GetItemDetailsAsync(IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken)
    {
        List<SalesOrderItemDetail> result = [];
        foreach (SalesOrderItem item in items.Take(500))
        {
            ProductOrderSnapshot? product = await stock.GetProductAsync(item.ProductId, item.VariantIndex, cancellationToken);
            result.Add(new SalesOrderItemDetail(item.ProductId, item.VariantIndex, item.Quantity,
                item.ProductNameSnapshot ?? product?.Name,
                item.ProductCodeSnapshot ?? product?.Code,
                product?.Brand,
                product?.ImageUrl,
                item.UnitPriceSnapshot ?? product?.Price,
                product?.Color,
                product?.Shape));
        }
        return result;
    }

    private async Task<PreparedItems> PrepareItemsAsync(IReadOnlyList<SalesOrderItem> items, CartOwner? customer, IReadOnlySet<string>? selectedStationProducts, CancellationToken ct)
    {
        if (items.Count is < 1 or > 500) throw Error(400, "Danh sách sản phẩm không hợp lệ");
        decimal total = 0;
        Dictionary<(string, int), int> reserved = [];
        List<StockAdjustment> adjustments = [];
        foreach (SalesOrderItem item in items)
        {
            ValidateItem(item);
            ProductVariantSnapshot? visibleVariant = customer is null
                ? null
                : await cartCatalog.FindVariantAsync(item.ProductId, item.VariantIndex, customer, ct);
            if (customer is not null && visibleVariant is null) throw Error(403, "Sản phẩm không thuộc phạm vi trạm được gán cho tài khoản.");
            if (selectedStationProducts is not null && !selectedStationProducts.Contains(item.ProductId)) throw Error(403, "Sản phẩm không thuộc phạm vi trạm được chọn.");
            ProductOrderSnapshot product = await stock.GetProductAsync(item.ProductId, item.VariantIndex, ct)
                ?? throw Error(404, $"Sản phẩm với ID {item.ProductId} không tồn tại.");
            if (!product.IsAssignedToBranch) throw Error(403, "Sản phẩm chưa được phân phối cho chi nhánh hiện tại.");
            if (customer is not null && (!product.Display || product.Earn == 0 || ParsePrice(product.Price) <= 0 || product.QuantityForSale <= 0)) throw Error(409, $"Sản phẩm {product.Name} hiện chỉ nhận liên hệ.");
            (string, int) key = (item.ProductId, item.VariantIndex);
            int already = reserved.GetValueOrDefault(key);
            if (product.QuantityForSale - already < item.Quantity) throw Error(400, $"Không đủ hàng cho sản phẩm {product.Name}.");
            reserved[key] = checked(already + item.Quantity);
            total += ParsePrice(product.Price) * item.Quantity;
            adjustments.Add(new StockAdjustment(item.ProductId, item.VariantIndex, -item.Quantity, 0, 0, product.VariantId));
        }
        return new PreparedItems(items.ToArray(), total, adjustments);
    }

    private async Task<decimal> ComputeTotalAsync(IReadOnlyList<SalesOrderItem> items, CancellationToken ct)
    {
        decimal total = 0;
        foreach (SalesOrderItem item in items)
        {
            ProductOrderSnapshot? product = await stock.GetProductAsync(item.ProductId, item.VariantIndex, ct);
            if (product is not null) total += ParsePrice(product.Price) * item.Quantity;
        }
        return total;
    }

    private async Task<IReadOnlyList<StockAdjustment>> BuildAdjustmentsAsync(IReadOnlyList<SalesOrderItem> items, int saleSign, int storageSign, int purchaseSign, CancellationToken ct, bool skipMissing = false)
    {
        List<StockAdjustment> result = [];
        foreach (SalesOrderItem item in items)
        {
            ProductOrderSnapshot? product = await stock.GetProductAsync(item.ProductId, item.VariantIndex, ct);
            if (product is null)
            {
                if (skipMissing) continue;
                throw Error(404, "Không tìm thấy sản phẩm trong đơn hàng.");
            }
            result.Add(new StockAdjustment(item.ProductId, item.VariantIndex, saleSign * item.Quantity, storageSign * item.Quantity, purchaseSign * item.Quantity, product.VariantId, RequireActiveAssignment: !skipMissing));
        }
        return result;
    }

    private async Task<(IReadOnlyList<StockAdjustment> Adjustments, IReadOnlyList<StorageHistoryWriteEntry> HistoryEntries)> BuildStatusTransitionAsync(
        SalesOrder order, int saleSign, int storageSign, int purchaseSign, int historySign,
        string note, string source, string? actorName, CancellationToken cancellationToken)
    {
        List<StockAdjustment> adjustments = [];
        List<StorageHistoryWriteEntry> entries = [];
        foreach (SalesOrderItem item in order.CartItems)
        {
            ProductOrderSnapshot product = await stock.GetProductAsync(item.ProductId, item.VariantIndex, cancellationToken)
                ?? throw Error(404, "Không tìm thấy sản phẩm trong đơn hàng.");
            adjustments.Add(new StockAdjustment(item.ProductId, item.VariantIndex,
                saleSign * item.Quantity, storageSign * item.Quantity, purchaseSign * item.Quantity, product.VariantId,
                RequireActiveAssignment: false));
            entries.Add(new StorageHistoryWriteEntry(
                item.ProductId,
                product.Name ?? string.Empty,
                historySign * item.Quantity,
                actorName ?? "Hệ thống",
                order.OrderCode,
                order.OrderCode,
                note,
                Source: source));
        }
        return (adjustments, entries);
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
            catch (Exception)
            {
                LogStorageHistoryFailure(logger, entry.Source ?? "unknown");
            }
        }
    }

    private async Task<(string? Names, string? Codes)> ResolveNotificationStationsBestEffortAsync(
        CartOwner owner,
        TTSmartEcom.Domain.Stations.Station? selectedStation,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<TTSmartEcom.Domain.Stations.Station> values = selectedStation is not null
                ? [selectedStation]
                : owner.StationIds.Count == 0
                    ? []
                    : await stations.FindByIdsAsync(owner.StationIds, false, cancellationToken);
            string names = string.Join(", ", values
                .Select(static value => value.StationName)
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
            string codes = string.Join(", ", values
                .Select(static value => value.StationCode)
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
            return (names.Length == 0 ? null : names, codes.Length == 0 ? null : codes);
        }
        catch (Exception exception)
        {
            LogPostCommitFailure(logger, "station_notification_metadata", exception.GetType().Name);
            return (null, null);
        }
    }

    private async Task<SalesOrder> InsertAsync(string phone, string? name, IReadOnlyList<SalesOrderItem> items, decimal total, CancellationToken ct)
    {
        long sequence = await orders.NextOrderCodeAsync(ct);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SalesOrder order = new(string.Empty, $"TTS-{sequence:00}", phone, name, items, total, "Processing", false, "Processing", null, [], now, now, 0);
        return await orders.InsertAsync(order, ct);
    }

    private async Task<SalesOrder> SaveAsync(SalesOrder order, CancellationToken ct)
    {
        SalesOrder? updated = await orders.UpdateAsync(order, order.Version, ct);
        return updated ?? throw Error(409, "Đơn hàng vừa được thay đổi bởi thao tác khác, vui lòng tải lại.");
    }

    private async Task<SalesOrder?> FindValidatedAsync(string id, CancellationToken ct)
    {
        ValidateId(id);
        return await orders.FindAsync(id, ct);
    }
    private async Task<SalesOrder> RequireOrderAsync(string id, CancellationToken ct) => await FindValidatedAsync(id, ct) ?? throw Error(404, "Order not found");
    private async Task<SalesOrder> RequireEditableAsync(string id, CancellationToken ct)
    {
        SalesOrder order = await RequireOrderAsync(id, ct);
        if (order.Status == "Completed" || order.State == "Cancelled") throw Error(400, "Không thể chỉnh sửa đơn đã hoàn thành hoặc đã hủy.");
        return order;
    }

    private static SalesOrderItem RequireIndex(SalesOrder order, int index) => index < 0 || index >= order.CartItems.Count ? throw Error(404, "Không tìm thấy dòng sản phẩm") : order.CartItems[index];
    private static void EnsureOwner(SalesOrder order, string? phone, bool isAdmin, string action) { if (!isAdmin && (string.IsNullOrWhiteSpace(phone) || !string.Equals(order.UserPhone, phone, StringComparison.Ordinal))) throw Error(403, $"Bạn không có quyền {action} đơn hàng này."); }
    private static void ValidateId(string id) { if (!MongoId(id)) throw Error(400, "Invalid request"); }
    private static bool MongoId(string id) => id.Length == 24 && id.All(Uri.IsHexDigit);
    private static void ValidateItem(SalesOrderItem item) { if (!MongoId(item.ProductId) || item.VariantIndex < 0 || item.Quantity <= 0 || item.Quantity > 100_000) throw Error(400, "Dữ liệu sản phẩm không hợp lệ"); }
    private static void ValidateListQuery(SalesOrderListQuery query) { if (query.Page < 1 || query.Limit is < 1 or > 100 || query.EndDate < query.StartDate || query.EndDate - query.StartDate > TimeSpan.FromDays(366)) throw Error(400, "Invalid request"); }
    private static bool ValidPhone(string? value) => value is not null && value.Length is 10 or 11 && value.All(char.IsDigit);
    private static string Key(SalesOrderItem x) => $"{x.ProductId}:{x.VariantIndex}:{x.Quantity}";
    private static decimal ParsePrice(string? value) => decimal.TryParse((value ?? "0").Replace(".", "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number) ? number : 0;
    private static bool TryBoolean(object? value, out bool result) { if (value is bool b) { result = b; return true; } if (value is System.Text.Json.JsonElement e && e.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False) { result = e.GetBoolean(); return true; } result = false; return false; }
    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) => new(new ApplicationError($"TTS-ORDER-{status}", 4300 + status, status, message));

    [LoggerMessage(
        EventId = 4391,
        Level = LogLevel.Warning,
        Message = "Customer order post-commit step {Step} failed with {ErrorType}")]
    private static partial void LogPostCommitFailure(ILogger logger, string step, string errorType);

    [LoggerMessage(
        EventId = 4392,
        Level = LogLevel.Warning,
        Message = "Order storage-history persistence failed for source {Source}")]
    private static partial void LogStorageHistoryFailure(ILogger logger, string source);

    private sealed record PreparedItems(IReadOnlyList<SalesOrderItem> Items, decimal Total, IReadOnlyList<StockAdjustment> Reservations);
}
