using System.Text.Json.Serialization;

namespace TTSmartEcom.Domain.Orders;

public sealed record SalesOrderItem(
    string ProductId,
    int VariantIndex,
    int Quantity,
    [property: JsonPropertyName("_id")] string? SubdocumentId = null,
    string? ProductCodeSnapshot = null,
    string? ProductNameSnapshot = null,
    string? VariantNameSnapshot = null,
    string? VariantPublicIdSnapshot = null,
    string? UnitPriceSnapshot = null);

public sealed record SalesOrder(
    [property: JsonPropertyName("_id")] string Id,
    string? OrderCode,
    string UserPhone,
    string? UserName,
    IReadOnlyList<SalesOrderItem> CartItems,
    decimal Total,
    string Status,
    bool Payment,
    string State,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<string> Images,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Version);

public sealed record SalesOrderListQuery(
    int Page = 1,
    int Limit = 10,
    string? Status = null,
    bool? Payment = null,
    string? State = null,
    string? Phone = null,
    string? Name = null,
    string? IdOrCode = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    bool ByCompletedDate = false,
    string SortField = "createdAt");

public sealed record CustomerOrderCreate(
    IReadOnlyList<SalesOrderItem> CartItems,
    string? StationCode = null);

public sealed record AdminOrderCreate(
    string UserPhone,
    string? UserName,
    IReadOnlyList<SalesOrderItem> Items);

public sealed record OrderUpdateField(string Field, object? Value);
