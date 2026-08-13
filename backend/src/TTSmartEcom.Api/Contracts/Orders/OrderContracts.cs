using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Contracts.Orders;

public sealed record OrderItemRequest(
    [property: Required, RegularExpression("^[a-fA-F0-9]{24}$")] string ProductId,
    [property: Range(0, 10_000)] int VariantIndex,
    [property: Range(1, 100_000)] int Quantity);

public sealed record CreateCustomerOrderRequest(
    [property: Required, MinLength(1), MaxLength(500)] IReadOnlyList<OrderItemRequest> CartItems,
    [property: StringLength(100)] string? StationCode = null);

public sealed record CreateAdminOrderRequest(
    [property: Required, RegularExpression("^[0-9]{10,11}$")] string UserPhone,
    [property: StringLength(160)] string? UserName,
    [property: Required, MinLength(1), MaxLength(500)] IReadOnlyList<OrderItemRequest> Items);

public sealed record UpdateOrderFieldRequest(
    [property: Required, RegularExpression("^(status|payment)$")] string Field,
    object? Value);

public sealed record UpdateOrderItemRequest([property: Range(1, 100_000)] int Quantity);
public sealed record ReorderOrderItemsRequest([property: Required, MaxLength(500)] IReadOnlyList<OrderItemRequest> CartItems);
public sealed record UpdateOrderCustomerRequest([property: StringLength(160)] string? UserName, [property: RegularExpression("^[0-9]{10,11}$")] string? UserPhone);
public sealed record UpdateOrderImagesRequest([property: Required, MaxLength(20)] IReadOnlyList<string> Images);
