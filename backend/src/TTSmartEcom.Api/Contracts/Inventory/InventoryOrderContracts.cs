using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TTSmartEcom.Api.Contracts.Inventory;

public sealed record InventoryOrderLineRequest(
    [property: Required, RegularExpression("^[a-fA-F0-9]{24}$")] string ProductId,
    [property: StringLength(100)] string? Price,
    [property: StringLength(100)] string? ImportPriceSnapshot,
    [property: Range(0, 100)] double? ProfitPercent,
    [property: StringLength(100)] string? Unit,
    [property: Range(0, 1_000_000)] int Quantity,
    double? QuantityRe = null,
    [property: JsonPropertyName("quantityEx")] double? ExportedQuantity = null,
    [property: StringLength(2_000)] string? Note = null,
    [property: StringLength(100)] string? Vat = null,
    bool? Status = null,
    bool? SkipStockUpdate = null,
    bool? IsAIScan = null);

public sealed record UpdateInventoryOrderLineRequest(
    [property: RegularExpression("^[a-fA-F0-9]{24}$")] string? ProductId = null,
    [property: StringLength(100)] string? Price = null,
    [property: StringLength(100)] string? ImportPriceSnapshot = null,
    [property: Range(0, 100)] double? ProfitPercent = null,
    [property: StringLength(100)] string? Unit = null,
    [property: Range(0, 1_000_000)] int? Quantity = null,
    double? QuantityRe = null,
    [property: JsonPropertyName("quantityEx")] double? ExportedQuantity = null,
    [property: StringLength(2_000)] string? Note = null,
    [property: StringLength(100)] string? Vat = null,
    bool? Status = null,
    bool? SkipStockUpdate = null,
    bool? IsAIScan = null);

public sealed record CreateInventoryOrderRequest(
    [property: StringLength(200)] string? OrderName,
    [property: StringLength(2_000)] string? Note,
    [property: MaxLength(500)] IReadOnlyList<InventoryOrderLineRequest>? ProductList);

public sealed record UpdateInventoryOrderRequest(
    [property: StringLength(200)] string? OrderName,
    [property: StringLength(2_000)] string? Note,
    [property: MaxLength(20)] IReadOnlyList<string>? Images);

public sealed record InventoryOrderStatusRequest(bool? Status);
public sealed record ReorderInventoryOrderRequest([property: Required, MaxLength(500)] IReadOnlyList<InventoryOrderLineRequest> ProductList);
