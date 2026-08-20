using System.ComponentModel.DataAnnotations;

namespace TTSmartEcom.Api.Contracts.Orders;

public sealed record OrderItemRequest(
    [param: Required, RegularExpression("^[a-fA-F0-9]{24}$")] string ProductId,
    [param: Range(0, 10_000)] int VariantIndex,
    [param: Range(1, 100_000)] int Quantity);

public sealed record CreateCustomerOrderRequest(
    [param: Required, MinLength(1), MaxLength(500)] IReadOnlyList<OrderItemRequest> CartItems,
    [param: StringLength(100)] string? StationCode = null);

public sealed record CreateAdminOrderRequest(
    [param: Required, RegularExpression("^[0-9]{10,11}$")] string UserPhone,
    [param: StringLength(160)] string? UserName,
    [param: Required, MinLength(1), MaxLength(500)] IReadOnlyList<OrderItemRequest> Items);

public sealed record UpdateOrderFieldRequest(
    [param: Required, RegularExpression("^(status|payment)$")] string Field,
    object? Value);

public sealed record UpdateOrderItemRequest([param: Range(1, 100_000)] int Quantity);
public sealed record ReorderOrderItemsRequest([param: Required, MaxLength(500)] IReadOnlyList<OrderItemRequest> CartItems);
public sealed record UpdateOrderCustomerRequest([param: StringLength(160)] string? UserName, [param: RegularExpression("^[0-9]{10,11}$")] string? UserPhone);
public sealed record UpdateOrderImagesRequest([param: Required, MaxLength(20)] IReadOnlyList<string> Images);
