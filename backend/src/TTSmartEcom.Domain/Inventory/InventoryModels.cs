namespace TTSmartEcom.Domain.Inventory;

public enum InventoryOrderKind
{
    Import,
    Export,
}

public sealed record InventoryOrderLine(
    bool Status,
    string? ProductId,
    string? Price,
    string? ImportPriceSnapshot,
    double? ProfitPercent,
    string? Unit,
    int Quantity,
    double ProgressQuantity,
    double? StockAppliedQuantity,
    bool StockUpdateSkipped,
    string? Note,
    string? Vat,
    string? Name = null,
    string? Brand = null,
    string? Image = null,
    string? SubdocumentId = null);

public sealed record InventoryOrder(
    string Id,
    string OrderName,
    string Note,
    string UserName,
    IReadOnlyList<InventoryOrderLine> ProductList,
    IReadOnlyList<string> Images,
    string Total,
    bool Status,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Version,
    InventoryOrderKind Kind,
    DateTimeOffset? TransactionDate = null);

public sealed record InventoryOrderListQuery(
    int Page = 1,
    string? OrderName = null,
    string? UserName = null,
    bool? Status = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    bool ByCompletedDate = false);

public sealed record InventoryOrderProductVariant(
    string? Id,
    string? Price,
    string? ImportPrice,
    double? Earn,
    string? ImageUrl,
    string? Color,
    string? Shape,
    string? ButtonCount,
    string? Frame,
    double? QuantityForSale,
    double? QuantityInStorage,
    string? Note);

public sealed record InventoryOrderProductSummary(
    string Id,
    string? Name,
    string? Brand,
    IReadOnlyList<InventoryOrderProductVariant> Variants,
    double TotalOrdered);

public sealed record InventoryOrderLineInput(
    string ProductId,
    string? Price,
    string? ImportPriceSnapshot,
    double? ProfitPercent,
    string? Unit,
    int Quantity,
    double ProgressQuantity,
    string? Note,
    string? Vat,
    bool? SkipStockUpdate = null,
    bool? IsAiScan = null,
    bool? Status = null,
    bool? QuantityAdjustment = null);

public sealed record InventoryOrderLineUpdateInput(
    string? ProductId,
    string? Price,
    string? ImportPriceSnapshot,
    double? ProfitPercent,
    string? Unit,
    int? Quantity,
    double? ProgressQuantity,
    string? Note,
    string? Vat,
    bool? SkipStockUpdate = null,
    bool? IsAiScan = null,
    bool? Status = null,
    bool? QuantityAdjustment = null);
