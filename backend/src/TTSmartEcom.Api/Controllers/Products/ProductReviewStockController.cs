using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products")]
public sealed class ProductReviewStockController(
    ProductCatalogWriteService products,
    ProductCatalogReadService productReads) : ControllerBase
{
    [HttpGet("{id}/review")]
    [AllowAnonymous]
    public async Task<IActionResult> Reviews(string id, CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<Domain.Products.ProductReview>? reviews = await products.GetReviewsAsync(id, cancellationToken);
            return reviews is null ? NotFound(new { message = "Product not found" }) : Ok(reviews);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{id}/review/create")]
    [Authorize]
    public async Task<IActionResult> CreateReview(
        string id, ProductReviewRequest request, CancellationToken cancellationToken)
    {
        UserIdentitySnapshot? actor = Actor();
        ProductReviewMutationResult result = await products.CreateReviewAsync(
            id, actor?.Email, request.Comment, request.Rating, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? StatusCode(201, new { message = "Review added successfully", review = result.Review }) : ReviewError(result);
    }

    [HttpPut("{id}/review/{reviewId}")]
    [Authorize]
    public async Task<IActionResult> UpdateReview(
        string id, string reviewId, ProductReviewRequest request, CancellationToken cancellationToken)
    {
        UserIdentitySnapshot? actor = Actor();
        ProductReviewMutationResult result = await products.UpdateReviewAsync(
            id, reviewId, request.Comment, request.Rating, actor?.Email, actor?.Role, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Review updated successfully", review = result.Review }) : ReviewError(result);
    }

    [HttpDelete("{id}/review/{reviewId}")]
    [Authorize]
    public async Task<IActionResult> DeleteReview(string id, string reviewId, CancellationToken cancellationToken)
    {
        UserIdentitySnapshot? actor = Actor();
        ProductReviewMutationResult result = await products.DeleteReviewAsync(
            id, reviewId, actor?.Email, actor?.Role, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Review deleted successfully", product = ProductResponse.From(result.Product!) }) : ReviewError(result);
    }

    [HttpPost("fetch-inventory-by-ids")]
    [Authorize]
    public async Task<IActionResult> InventoryByIds(FetchProductsByIdsRequest request, CancellationToken cancellationToken)
    {
        if (!CanReadInventory()) return Forbid();
        if (request.Ids is null || request.Ids.Length is 0 or > 200 || request.Ids.Any(id => !IsObjectId(id)))
            return BadRequest(new { success = 0, message = "Vui lòng cung cấp một mảng ids hợp lệ" });
        var result = await productReads.FetchByIdsAsync(
            request.Ids, new ProductViewer("admin"), cancellationToken, includePrivate: true);
        if (!result.Valid) return BadRequest(new { success = 0, message = "Không có id nào hợp lệ trong mảng" });
        return Ok(new
        {
            success = 1, total = result.Products.Count,
            products = result.Products.Select(product => new
            {
                _id = product.Id, name = product.Name, code = product.Code, brand = product.Brand,
                variant = product.Variants.Select(variant => new
                {
                    imgUrl = variant.ImageUrl ?? string.Empty, importPrice = variant.ImportPrice ?? string.Empty,
                    price = variant.Price ?? string.Empty, earn = variant.Earn ?? 0,
                }).ToArray(),
            }).ToArray(),
        });
    }

    [HttpPost("{id}/{variantIndex:int}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> AdjustStock(
        string id, int variantIndex, ProductStockRequest request, CancellationToken cancellationToken)
    {
        if (!TryNumber(request.Quantity, out double quantity)) return BadRequest(new { message = "Quantity must be a number" });
        if (quantity == 0) return BadRequest(new { message = "Số lượng thay đổi phải khác 0" });
        if (!TryScalar(request.OrderId, out string? orderId) || !TryScalar(request.OrderName, out string? orderName) ||
            !TryBool(request.IsAiScan, out bool isAiScan)) return BadRequest(new { message = "Stock metadata is invalid" });
        ProductStockMutationResult result = await products.AdjustStockAsync(id, variantIndex, quantity,
            Actor()?.Name, orderId, orderName, isAiScan, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new
            {
                message = "Quantity updated & history saved", product = ProductResponse.From(result.Product!),
                history = new { _id = result.HistoryId, productId = id, productName = result.Product!.Name,
                    quantity, userName = Actor()?.Name, orderId, orderName, isAIScan = isAiScan, source = "product_manual" },
            }) : StockError(result);
    }

    private UserIdentitySnapshot? Actor() => HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;

    private bool CanReadInventory()
    {
        UserIdentitySnapshot? actor = Actor();
        if (actor is null) return false;
        if (actor.Role is "superadmin" or "admin") return true;
        if (actor.Role != "staff") return false;
        string[] allowed = ["iporder.view", "iporder.create", "iporder.edit", "eporder.view", "eporder.create", "eporder.edit"];
        return allowed.Any(permission => actor.Permissions.Contains(permission, StringComparer.Ordinal));
    }

    private ObjectResult ReviewError(ProductReviewMutationResult result) => StatusCode(result.Status switch
    {
        ProductMutationStatus.NotFound => 404, ProductMutationStatus.Forbidden => 403,
        ProductMutationStatus.Invalid => 400, ProductMutationStatus.Conflict => 409, _ => 500,
    }, new { message = result.Message ?? "Invalid request" });

    private ObjectResult StockError(ProductStockMutationResult result) => StatusCode(result.Status switch
    {
        ProductMutationStatus.NotFound => 404, ProductMutationStatus.Invalid => 400,
        ProductMutationStatus.Conflict => 409, _ => 500,
    }, new { message = result.Message ?? "Invalid request" });

    private static bool IsObjectId(string? id) => id?.Length == 24 && id.All(Uri.IsHexDigit);

    private static bool TryNumber(JsonElement value, out double number)
    {
        number = 0;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDouble(out number) && double.IsFinite(number),
            JsonValueKind.String => double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number),
            _ => false,
        };
    }

    private static bool TryScalar(JsonElement value, out string? scalar)
    {
        scalar = value.ValueKind switch
        {
            JsonValueKind.Undefined or JsonValueKind.Null => null,
            JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(), JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => null,
        };
        return value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array);
    }

    private static bool TryBool(JsonElement value, out bool result)
    {
        result = false;
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return true;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { result = value.GetBoolean(); return true; }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number) && number is 0 or 1) { result = number == 1; return true; }
        return false;
    }
}
