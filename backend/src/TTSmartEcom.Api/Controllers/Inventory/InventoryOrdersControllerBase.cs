using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Inventory;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Inventory;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Domain.Inventory;

namespace TTSmartEcom.Api.Controllers.Inventory;

[ApiController]
[Authorize]
public abstract class InventoryOrdersControllerBase(
    IInventoryOrderService orders,
    IOptions<LegacyCompatibilityOptions> compatibility,
    LocalMediaFileService mediaFiles) : ControllerBase
{
    protected abstract InventoryOrderKind Kind { get; }
    protected abstract string PermissionPrefix { get; }

    [HttpGet("orders")]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] string? orderName = null, [FromQuery] string? userName = null,
        [FromQuery] bool? status = null, [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] bool byCompletedDate = false, CancellationToken ct = default)
    {
        IActionResult? denied = Permission("view"); if (denied is not null) return denied;
        InventoryOrderListResult result = await orders.ListAsync(Kind, new InventoryOrderListQuery(page, orderName, userName, status, startDate, endDate, byCompletedDate), ct);
        return Ok(new { orders = result.Orders.Select(ToResponse).ToArray(), pagination = new { currentPage = result.CurrentPage, totalPages = result.TotalPages, totalItems = result.TotalItems } });
    }

    [HttpGet("orders/{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken ct)
    {
        IActionResult? denied = Permission("view"); if (denied is not null) return denied;
        InventoryOrder? order = await orders.GetAsync(Kind, id, ct);
        return order is null ? NotFound(new { message = "Order not found" }) : Ok(ToResponse(order));
    }

    [HttpPost("orders")]
    public async Task<IActionResult> Create(CreateInventoryOrderRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("create"); if (denied is not null) return denied;
        IReadOnlyList<InventoryOrderLineInput> lines = (request.ProductList ?? []).Select(ToInput).ToArray();
        return StatusCode(201, ToResponse(await orders.CreateAsync(Kind, Identity()?.Name ?? "Hệ thống", request.OrderName, request.Note, lines, ct)));
    }

    [HttpPut("orders/{id}")]
    public async Task<IActionResult> Metadata(string id, UpdateInventoryOrderRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.UpdateMetadataAsync(Kind, id, request.OrderName, request.Note, request.Images, ct)));
    }

    [HttpPut("orders/{id}/name")]
    public async Task<IActionResult> Name(string id, UpdateInventoryOrderRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.UpdateNameAsync(Kind, id, request.OrderName, request.Note, ct)));
    }

    [HttpPut("orders/{id}/status")]
    public async Task<IActionResult> Status(string id, InventoryOrderStatusRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        if (!request.Status.HasValue) return InvalidStatus();
        return Ok(ToResponse(await orders.SetStatusAsync(Kind, id, request.Status.Value, ct)));
    }

    [HttpPost("orders/{id}/products")]
    public async Task<IActionResult> AddLine(string id, InventoryOrderLineRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.AddLineAsync(Kind, id, ToInput(request), Identity()?.Name ?? "Hệ thống", ct)));
    }

    [HttpPut("orders/{id}/products/{productIndex:int}")]
    public async Task<IActionResult> UpdateLine(string id, int productIndex, UpdateInventoryOrderLineRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.UpdateLineAsync(Kind, id, productIndex, ToUpdateInput(request), Identity()?.Name ?? "Hệ thống", ct)));
    }

    [HttpDelete("orders/{id}/products/{productIndex:int}")]
    public async Task<IActionResult> DeleteLine(string id, int productIndex, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.DeleteLineAsync(Kind, id, productIndex, ct)));
    }

    [HttpPut("orders/{id}/products/{productIndex:int}/status")]
    public async Task<IActionResult> LineStatus(string id, int productIndex, InventoryOrderStatusRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        if (!request.Status.HasValue) return InvalidStatus();
        return Ok(ToResponse(await orders.SetLineStatusAsync(Kind, id, productIndex, request.Status.Value, ct)));
    }

    [HttpPut("orders/{id}/reorder")]
    public async Task<IActionResult> Reorder(string id, ReorderInventoryOrderRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        return Ok(ToResponse(await orders.ReorderLinesAsync(Kind, id, request.ProductList.Select(ToInput).ToArray(), ct)));
    }

    [HttpDelete("orders/{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        IActionResult? denied = Permission("delete"); if (denied is not null) return denied;
        await orders.DeleteAsync(Kind, id, ct);
        return Ok(new { message = "Order deleted successfully" });
    }

    [HttpPut("orders/{id}/setStatusAndQuantity")]
    public async Task<IActionResult> StockCompletion(string id, InventoryOrderStatusRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        if (!request.Status.HasValue) return InvalidStatus();
        return Ok(ToResponse(await orders.CompleteAsync(Kind, id, request.Status.Value, Identity()?.Name ?? "Hệ thống", ct)));
    }

    [HttpPut("orders/{id}/products/{productIndex:int}/setStatusAndQuantity")]
    public async Task<IActionResult> LineStockCompletion(string id, int productIndex, InventoryOrderStatusRequest request, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        if (!request.Status.HasValue) return InvalidStatus();
        return Ok(ToResponse(await orders.CompleteLineAsync(Kind, id, productIndex, request.Status.Value, Identity()?.Name ?? "Hệ thống", ct)));
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] int page = 1, CancellationToken ct = default)
    {
        IActionResult? denied = Permission("view"); if (denied is not null) return denied;
        InventoryOrderProductSummaryResult result = await orders.ListProductsAsync(Kind, page, ct);
        return Ok(new
        {
            products = result.Products.Select(ToProductSummaryResponse).ToArray(),
            pagination = new
            {
                currentPage = result.CurrentPage,
                totalPages = result.TotalPages,
                totalItems = result.TotalItems,
            },
        });
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(IFormFile? invoice, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
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
                "invoice-manual-",
                "invoice-images",
                ct);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusCode(500, new { message = "Lỗi upload ảnh" });
        }
        return saved.IsSuccess
            ? Ok(new { success = 1, imageUrl = saved.PublicUrl })
            : BadRequest(new { success = 0, message = InvoiceValidationMessage(saved.ErrorCode) });
    }

    [HttpDelete("delete-image")]
    public async Task<IActionResult> DeleteImage([FromQuery] string? imageUrl, CancellationToken ct)
    {
        IActionResult? denied = Permission("edit"); if (denied is not null) return denied;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return BadRequest(new { success = 0, message = "Thiếu thông tin imageUrl." });
        }

        LocalMediaDeleteResult result;
        try
        {
            result = await mediaFiles.DeleteAsync(imageUrl, "invoice-images", "invoices", ct);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusCode(500, new { success = 0, message = "Lỗi server khi xóa ảnh vật lý" });
        }
        if (!result.IsValid)
        {
            return BadRequest(new { success = 0, message = "imageUrl không hợp lệ." });
        }

        return result.Deleted
            ? Ok(new { success = 1, message = "Đã xóa ảnh vật lý thành công." })
            : Ok(new { success = 1, message = "File không tồn tại trên ổ cứng hoặc đã được xóa." });
    }

    private IActionResult? Permission(string action)
    {
        UserIdentitySnapshot? identity = Identity();
        if (identity is null) return Unauthorized(new { message = "Access denied, no token provided" });
        string required = $"{PermissionPrefix}.{action}";
        bool allowed = identity.Role == "superadmin" ||
            (identity.Role == "admin" && compatibility.Value.AdminFullAccess) ||
            identity.Permissions.Contains(required, StringComparer.Ordinal);
        return allowed ? null : StatusCode(StatusCodes.Status403Forbidden, new { message = $"Access denied, missing permission: {required}" });
    }

    private UserIdentitySnapshot? Identity() => HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
    private BadRequestObjectResult InvalidStatus() => BadRequest(new { message = Kind == InventoryOrderKind.Import ? "status phải là boolean." : "status phải là boolean" });
    private static string InvoiceValidationMessage(string? errorCode) => errorCode switch
    {
        "TTS-UPLOAD-0003" => "File too large",
        "TTS-UPLOAD-0004" or "TTS-UPLOAD-0005" or "TTS-UPLOAD-0006" => "Chỉ chấp nhận file ảnh (jpg, png, webp).",
        _ => "File ảnh không hợp lệ",
    };
    private InventoryOrderLineInput ToInput(InventoryOrderLineRequest request) => new(request.ProductId, request.Price, request.ImportPriceSnapshot, request.ProfitPercent, request.Unit, request.Quantity,
        Kind == InventoryOrderKind.Import ? request.QuantityRe ?? 0 : request.ExportedQuantity ?? 0, request.Note, request.Vat, request.SkipStockUpdate, request.IsAIScan, request.Status);

    private InventoryOrderLineUpdateInput ToUpdateInput(UpdateInventoryOrderLineRequest request) => new(request.ProductId, request.Price, request.ImportPriceSnapshot, request.ProfitPercent, request.Unit, request.Quantity,
        Kind == InventoryOrderKind.Import ? request.QuantityRe : request.ExportedQuantity, request.Note, request.Vat, request.SkipStockUpdate, request.IsAIScan, request.Status);

    private static object ToProductSummaryResponse(InventoryOrderProductSummary product) => new
    {
        _id = product.Id,
        name = product.Name,
        brand = product.Brand,
        variant = product.Variants.Select(value => new
        {
            _id = value.Id,
            price = value.Price,
            importPrice = value.ImportPrice,
            earn = value.Earn,
            imgUrl = value.ImageUrl,
            color = value.Color,
            shape = value.Shape,
            buttonCount = value.ButtonCount,
            frame = value.Frame,
            quantityForSale = value.QuantityForSale,
            quantityInStorage = value.QuantityInStorage,
            note = value.Note,
        }).ToArray(),
        totalOrdered = product.TotalOrdered,
    };

    private object ToResponse(InventoryOrder order) => new Dictionary<string, object?>
    {
        ["_id"] = order.Id,
        ["orderName"] = order.OrderName,
        ["note"] = order.Note,
        ["userName"] = order.UserName,
        ["productList"] = order.ProductList.Select(ToLineResponse).ToArray(),
        ["images"] = order.Images,
        ["total"] = order.Total,
        ["status"] = order.Status,
        ["completedAt"] = order.CompletedAt,
        ["createdAt"] = order.CreatedAt,
        ["updatedAt"] = order.UpdatedAt,
        ["__v"] = order.Version,
    };

    private object ToLineResponse(InventoryOrderLine line)
    {
        Dictionary<string, object?> response = new()
        {
            ["_id"] = line.SubdocumentId,
            ["status"] = line.Status,
            ["productId"] = line.ProductId,
            ["price"] = line.Price,
            ["unit"] = line.Unit,
            ["quantity"] = line.Quantity,
            [Kind == InventoryOrderKind.Import ? "quantityRe" : "quantityEx"] = line.ProgressQuantity,
            ["stockAppliedQuantity"] = line.StockAppliedQuantity,
            ["note"] = line.Note,
            ["vat"] = line.Vat,
            ["name"] = line.Name,
            ["brand"] = line.Brand,
            ["image"] = line.Image,
        };
        if (Kind == InventoryOrderKind.Export)
        {
            response["importPriceSnapshot"] = line.ImportPriceSnapshot;
            response["profitPercent"] = line.ProfitPercent;
            response["stockUpdateSkipped"] = line.StockUpdateSkipped;
        }
        return response;
    }
}
