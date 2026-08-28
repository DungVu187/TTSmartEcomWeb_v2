using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace TTSmartEcom.Api.Contracts.Inventory;

public sealed record InventoryOrderLineRequest(
    [param: Required, RegularExpression("^[a-fA-F0-9]{24}$")] string ProductId,
    [param: StringLength(100)] string? Price,
    [param: StringLength(100)] string? ImportPriceSnapshot,
    [param: Range(0, 100)] double? ProfitPercent,
    [param: StringLength(100)] string? Unit,
    [param: Range(0, 1_000_000)] int Quantity,
    double? QuantityRe = null,
    [property: JsonPropertyName("quantityEx")] double? ExportedQuantity = null,
    [param: StringLength(2_000)] string? Note = null,
    [param: StringLength(100)] string? Vat = null,
    bool? Status = null,
    bool? SkipStockUpdate = null,
    bool? IsAIScan = null,
    bool? QuantityAdjustment = null);

public sealed record UpdateInventoryOrderLineRequest(
    [param: RegularExpression("^[a-fA-F0-9]{24}$")] string? ProductId = null,
    [param: StringLength(100)] string? Price = null,
    [param: StringLength(100)] string? ImportPriceSnapshot = null,
    [param: Range(0, 100)] double? ProfitPercent = null,
    [param: StringLength(100)] string? Unit = null,
    [param: Range(0, 1_000_000)] int? Quantity = null,
    double? QuantityRe = null,
    [property: JsonPropertyName("quantityEx")] double? ExportedQuantity = null,
    [param: StringLength(2_000)] string? Note = null,
    [param: StringLength(100)] string? Vat = null,
    bool? Status = null,
    bool? SkipStockUpdate = null,
    bool? IsAIScan = null,
    bool? QuantityAdjustment = null);

public sealed record CreateInventoryOrderRequest(
    [param: StringLength(200)] string? OrderName,
    [param: StringLength(2_000)] string? Note,
    [param: MaxLength(500)] IReadOnlyList<InventoryOrderLineRequest>? ProductList,
    JsonElement TransactionDate = default);

public sealed record UpdateInventoryOrderRequest(
    [param: StringLength(200)] string? OrderName,
    [param: StringLength(2_000)] string? Note,
    [param: MaxLength(20)] IReadOnlyList<string>? Images,
    JsonElement TransactionDate = default);

public sealed record InventoryOrderStatusRequest(bool? Status);
public sealed record ReorderInventoryOrderRequest([param: Required, MaxLength(500)] IReadOnlyList<InventoryOrderLineRequest> ProductList);
