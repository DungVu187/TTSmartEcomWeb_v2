using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Orders;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Orders;
using TTSmartEcom.Domain.Orders;

namespace TTSmartEcom.Api.Controllers.Orders;

[ApiController]
[Route("orders")]
public sealed class OrdersController(IOrderService orders, LocalMediaFileService mediaFiles) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("order.view")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int limit = 10,
        [FromQuery] string? status = null, [FromQuery] bool? payment = null,
        [FromQuery] string? state = null, [FromQuery] string? phone = null,
        [FromQuery] string? name = null, [FromQuery(Name = "id")] string? idOrCode = null,
        [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] bool byCompletedDate = false, CancellationToken ct = default)
    {
        string sort = byCompletedDate ? "completedAt" : "createdAt";
        OrderListResult result = await orders.ListAdminAsync(new SalesOrderListQuery(page, limit, status, payment, state, phone, name, idOrCode, startDate, endDate, byCompletedDate, sort), ct);
        return Ok(new { orders = result.Orders, total = result.Total, currentPage = result.CurrentPage, totalPages = result.TotalPages });
    }

    [HttpGet("userOrders")]
    [Authorize]
    public async Task<IActionResult> UserOrders([FromQuery] string? state, CancellationToken ct)
    {
        string? phone = User.FindFirstValue("phone");
        OrderListResult result = await orders.ListUserAsync(phone ?? string.Empty, state, ct);
        return result.Orders.Count == 0
            ? NotFound(new { message = result.Message })
            : Ok(new { message = result.Message, orders = result.Orders });
    }

    [HttpGet("customer-suggestions")]
    [PermissionAuthorize("order.view")]
    public async Task<IActionResult> CustomerSuggestions([FromQuery] string? search, CancellationToken ct)
    {
        SalesOrderListQuery query = new(1, 100, Name: search, Phone: search);
        OrderListResult result = await orders.ListAdminAsync(query, ct);
        object[] customers = result.Orders
            .Where(static x => !string.IsNullOrWhiteSpace(x.UserPhone))
            .GroupBy(static x => x.UserPhone, StringComparer.Ordinal)
            .Select(static group => group.OrderByDescending(x => x.CreatedAt).First())
            .Select(static x => new { userName = x.UserName, userPhone = x.UserPhone })
            .ToArray();
        return Ok(new { success = true, customers });
    }

    [HttpGet("processing-count")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessingCount(CancellationToken ct) => Ok(new { success = true, count = await orders.ProcessingCountAsync(ct) });

    [HttpPost("create-order")]
    [Authorize]
    public async Task<IActionResult> CreateCustomer(CreateCustomerOrderRequest request, CancellationToken ct)
    {
        SalesOrder order = await orders.CreateCustomerAsync(RequireUserId(), new CustomerOrderCreate(request.CartItems.Select(ToItem).ToArray(), request.StationCode), ct);
        return StatusCode(201, new { message = "Đặt hàng thành công", order });
    }

    [HttpPost("admin-create-order")]
    [PermissionAuthorize("order.create")]
    public async Task<IActionResult> CreateAdmin(CreateAdminOrderRequest request, CancellationToken ct)
    {
        SalesOrder order = await orders.CreateAdminAsync(new AdminOrderCreate(request.UserPhone, request.UserName, request.Items.Select(ToItem).ToArray()), ct);
        return StatusCode(201, new { success = true, message = "Tạo đơn hàng thành công", order });
    }

    [HttpPost("admin-draft")]
    [PermissionAuthorize("order.create")]
    public async Task<IActionResult> CreateDraft(CancellationToken ct) => StatusCode(201, new { success = true, order = await orders.CreateDraftAsync(ct) });

    [HttpGet("admin-detail/{id}")]
    [PermissionAuthorize("order.view")]
    public async Task<IActionResult> AdminDetail(string id, CancellationToken ct)
    {
        SalesOrder? order = await orders.GetAdminAsync(id, ct);
        if (order is null) return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });
        IReadOnlyList<SalesOrderItemDetail> details = await orders.GetItemDetailsAsync(order.CartItems, ct);
        return Ok(new { success = true, order = AdminDetailResponse(order, details) });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        SalesOrder? order = await orders.GetAsync(id, ct);
        if (order is null) return NotFound(new { message = "Order not found" });
        if (!IsAdmin() && !string.Equals(order.UserPhone, User.FindFirstValue("phone"), StringComparison.Ordinal)) return StatusCode(403, new { message = "Bạn không có quyền xem đơn hàng này." });
        IReadOnlyList<SalesOrderItemDetail> details = await orders.GetItemDetailsAsync(order.CartItems, ct);
        return Ok(new
        {
            userPhone = order.UserPhone,
            total = order.Total,
            cartItems = details.Select(x => new { x.Name, x.Brand, variant = new { color = x.Color, shape = x.Shape, price = x.Price, imgUrl = x.ImgUrl }, x.Quantity }),
            status = order.Status,
            payment = order.Payment,
        });
    }

    [HttpPut("update-order/{id}")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> UpdateField(string id, UpdateOrderFieldRequest request, CancellationToken ct) =>
        Ok(new { success = true, message = "Order updated successfully", order = await orders.UpdateFieldAsync(id, request.Field, request.Value, Identity()?.Name, ct) });

    [HttpPut("{id}")]
    [Authorize]
    public async Task<IActionResult> Cancel(string id, CancellationToken ct) =>
        Ok(new { message = "Order cancelled successfully.", order = await orders.CancelAsync(id, RequireUserId(), User.FindFirstValue("phone"), IsAdmin(), ct) });

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await orders.DeleteAsync(id, RequireUserId(), User.FindFirstValue("phone"), IsAdmin(), ct);
        return Ok(new { message = "Order deleted and quantities restored if necessary." });
    }

    [HttpPost("{id}/items")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> AddItem(string id, OrderItemRequest request, CancellationToken ct) => await AdminOrderResult(orders.AddItemAsync(id, ToItem(request), ct), ct);

    [HttpPut("{id}/items/{index:int}")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> UpdateItem(string id, int index, UpdateOrderItemRequest request, CancellationToken ct) => await AdminOrderResult(orders.UpdateItemAsync(id, index, request.Quantity, ct), ct);

    [HttpDelete("{id}/items/{index:int}")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> DeleteItem(string id, int index, CancellationToken ct) => await AdminOrderResult(orders.DeleteItemAsync(id, index, ct), ct);

    [HttpPut("{id}/reorder")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> Reorder(string id, ReorderOrderItemsRequest request, CancellationToken ct) => await AdminOrderResult(orders.ReorderItemsAsync(id, request.CartItems.Select(ToItem).ToArray(), ct), ct);

    [HttpPut("{id}/customer")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> Customer(string id, UpdateOrderCustomerRequest request, CancellationToken ct) => await AdminOrderResult(orders.UpdateCustomerAsync(id, request.UserName, request.UserPhone, ct), ct);

    [HttpPut("{id}/images")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> Images(string id, UpdateOrderImagesRequest request, CancellationToken ct) => await AdminOrderResult(orders.UpdateImagesAsync(id, request.Images, ct), ct);

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    [PermissionAuthorize("order.edit")]
    public async Task<IActionResult> UploadImage(IFormFile? invoice, CancellationToken ct)
    {
        if (invoice is null)
        {
            return BadRequest(new { success = 0, message = "Không có file được tải lên" });
        }

        LocalMediaSaveResult saved;
        try
        {
            saved = await mediaFiles.SaveAsync(
                invoice,
                FileUploadKind.Invoice,
                "invoices",
                "invoice-sale-",
                "invoice-images",
                ct);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusCode(500, new { success = 0, message = "Lỗi khi tải ảnh lên" });
        }
        return saved.IsSuccess
            ? Ok(new { success = 1, imageUrl = saved.PublicUrl })
            : BadRequest(new { success = 0, message = InvoiceValidationMessage(saved.ErrorCode) });
    }

    [HttpDelete("delete-image")]
    [PermissionAuthorize("order.edit")]
    public IActionResult DeleteImage([FromQuery] string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return BadRequest(new { success = 0, message = "Thiếu thông tin imageUrl." });
        }

        LocalMediaDeleteResult result;
        try
        {
            result = mediaFiles.Delete(imageUrl, "invoice-images", "invoices");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusCode(500, new { success = 0, message = "Lỗi khi xóa ảnh" });
        }
        return result.IsValid
            ? Ok(new { success = 1, message = "Đã xóa ảnh nếu file tồn tại." })
            : BadRequest(new { success = 0, message = "imageUrl không hợp lệ." });
    }

    private async Task<IActionResult> AdminOrderResult(Task<SalesOrder> task, CancellationToken ct)
    {
        SalesOrder order = await task;
        IReadOnlyList<SalesOrderItemDetail> details = await orders.GetItemDetailsAsync(order.CartItems, ct);
        return Ok(new { success = true, order = AdminDetailResponse(order, details) });
    }

    private static object AdminDetailResponse(SalesOrder order, IReadOnlyList<SalesOrderItemDetail> items) => new
    {
        _id = order.Id, order.OrderCode, order.UserName, order.UserPhone, order.Status, order.Payment, order.State, order.Total,
        order.CompletedAt, order.Images, cartItems = items,
    };
    private static SalesOrderItem ToItem(OrderItemRequest x) => new(x.ProductId, x.VariantIndex, x.Quantity);
    private static string InvoiceValidationMessage(string? errorCode) => errorCode switch
    {
        "TTS-UPLOAD-0003" => "File too large",
        "TTS-UPLOAD-0004" or "TTS-UPLOAD-0005" or "TTS-UPLOAD-0006" => "Chỉ chấp nhận file ảnh (jpg, png, webp).",
        _ => "File ảnh không hợp lệ",
    };
    private string RequireUserId() => User.FindFirstValue("userId") ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    private bool IsAdmin() => User.IsInRole("superadmin") || User.IsInRole("admin") || User.IsInRole("staff");
    private UserIdentitySnapshot? Identity() =>
        HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
}
