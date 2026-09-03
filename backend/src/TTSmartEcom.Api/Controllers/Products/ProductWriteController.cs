using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products")]
public sealed class ProductWriteController(ProductCatalogWriteService products) : ControllerBase
{
    [HttpPost("create")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> Create(ProductMutationRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.CreateAsync(
            request.ToMutation(), ActorName(), CurrentContext(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? StatusCode(201, new { message = "Product created successfully", product = ProductResponse.From(result.Product!) })
            : MutationError(result);
    }

    [HttpPut("{id}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Update(string id, ProductMutationRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.UpdateAsync(id, request.ToMutation(), ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success ? Ok(ProductResponse.From(result.Product!)) : MutationError(result);
    }

    [HttpDelete("{id}")]
    [PermissionAuthorize("product.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.DeleteAsync(id, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Product deleted successfully" }) : MutationError(result);
    }

    [HttpPut("{id}/toggle-display")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> ToggleDisplay(string id, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.ToggleDisplayAsync(id, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Thay đổi hiển thị thành công", product = ProductResponse.From(result.Product!) })
            : MutationError(result);
    }

    [HttpPost("bulk-delete")]
    [PermissionAuthorize("product.delete")]
    public async Task<IActionResult> BulkDelete(ProductBulkDeleteRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.BulkDeleteAsync(request.Ids, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = $"Đã xóa thành công {result.AffectedCount} sản phẩm." }) : MutationError(result);
    }

    [HttpPost("{id}/variant")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> AddVariant(string id, ProductVariantMutationRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.AddVariantAsync(id, request.ToMutation(), ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? StatusCode(201, new { message = "Variant added successfully", product = ProductResponse.From(result.Product!) })
            : MutationError(result);
    }

    [HttpPut("{id}/{variantIndex:int}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> UpdateVariant(
        string id, int variantIndex, ProductVariantMutationRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.UpdateVariantAsync(
            id, variantIndex, request.ToMutation(), ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Variant updated successfully", product = ProductResponse.From(result.Product!) })
            : MutationError(result);
    }

    [HttpDelete("{id}/{variantIndex:int}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> DeleteVariant(string id, int variantIndex, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.DeleteVariantAsync(id, variantIndex, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Variant deleted successfully", product = ProductResponse.From(result.Product!) })
            : MutationError(result);
    }

    [HttpPut("{id}/{variantIndex:int}/update-earn")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> UpdateEarn(
        string id, int variantIndex, ProductEarnRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.UpdateEarnAsync(
            id, variantIndex, request.Earn, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Earn and price updated successfully", variant = result.Variant }) : MutationError(result);
    }

    [HttpPut("{id}/{variantIndex:int}/update-import-price")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> UpdateImportPrice(
        string id, int variantIndex, ProductImportPriceRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.UpdateImportPriceAsync(
            id, variantIndex, request.ImportPrice, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Import price and price updated successfully", variant = result.Variant }) : MutationError(result);
    }

    [HttpPut("purchase/{id}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Purchase(string id, ProductPurchaseRequest request, CancellationToken cancellationToken)
    {
        ProductMutationResult result = await products.AdjustPurchaseCountAsync(id, request.Action, request.Amount, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Cập nhật thành công", purchaseCount = result.Product!.PurchaseCount }) : MutationError(result);
    }

    [HttpPut("update-display-field")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> BackfillDisplay(CancellationToken cancellationToken)
    {
        long count = await products.BackfillDisplayAsync(cancellationToken);
        return Ok(count == 0
            ? new { message = "Không có sản phẩm nào cần cập nhật hoặc tất cả sản phẩm đã có trường display", updatedCount = count }
            : new { message = "Cập nhật trường display thành công", updatedCount = count });
    }

    [HttpPost("by-codes")]
    [AllowAnonymous]
    public async Task<IActionResult> ByCodes(ProductCodesRequest request, CancellationToken cancellationToken)
    {
        var result = await products.FindByCodesAsync(request.Codes, Viewer(), cancellationToken);
        if (!result.Valid) return BadRequest(new { message = "Vui lòng cung cấp một mảng codes hợp lệ" });
        if (result.Products.Count == 0) return NotFound(new { message = "Không tìm thấy sản phẩm nào với các code được cung cấp" });
        return Ok(new
        {
            success = 1, total = result.Products.Count,
            products = result.Products.Select(value => new { code = value.Code, _id = value.Id }).ToArray(),
        });
    }

    private ProductViewer? Viewer()
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        ICurrentUserContext? scope = HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
        return identity is null && scope is null
            ? null
            : new ProductViewer(identity?.Role, identity?.StationIds, scope?.ActiveCompanyId, scope?.ActiveBranchId);
    }

    private string? ActorName() =>
        (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;

    private ICurrentUserContext? CurrentContext() =>
        HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;

    private ObjectResult MutationError(ProductMutationResult result)
    {
        int status = result.Status switch
        {
            ProductMutationStatus.NotFound => 404,
            ProductMutationStatus.Conflict => 409,
            ProductMutationStatus.Invalid => 400,
            ProductMutationStatus.Forbidden => 403,
            _ => 500,
        };
        return StatusCode(status, new { message = result.Message ?? "Invalid request" });
    }
}

[ApiController]
[Route("products/types")]
public sealed class ProductTypeWriteController(ProductCatalogWriteService products) : ControllerBase
{
    [HttpPost]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> Create(ProductTypeMutationRequest request, CancellationToken cancellationToken) =>
        TypeResult(await products.CreateTypeAsync(
            request.Type, request.Icon, ActorName(), includeIconInAudit: true, cancellationToken), 201);

    [HttpPut("{id}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Update(string id, ProductTypeMutationRequest request, CancellationToken cancellationToken) =>
        TypeResult(await products.UpdateTypeAsync(
            id, request.Type, request.Icon, ActorName(), cancellationToken), 200);

    [HttpDelete("{id}")]
    [PermissionAuthorize("product.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        ProductTypeMutationResult result = await products.DeleteTypeAsync(id, true, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success ? Ok(new { message = "Đã xóa loại sản phẩm" }) : TypeError(result);
    }

    private ObjectResult TypeResult(ProductTypeMutationResult result, int successStatus) => result.Status == ProductMutationStatus.Success
        ? StatusCode(successStatus, new
        {
            _id = result.ProductType!.Id, Type = result.ProductType.Type, icon = result.ProductType.Icon,
            updatedProducts = result.UpdatedProducts, updatedHomeCategories = result.UpdatedHomeCategories,
        }) : TypeError(result);

    private ObjectResult TypeError(ProductTypeMutationResult result) => StatusCode(result.Status switch
    {
        ProductMutationStatus.NotFound => 404, ProductMutationStatus.Conflict => 409,
        ProductMutationStatus.Invalid => 400, _ => 500,
    }, new { message = result.Message ?? "Invalid request" });

    private string? ActorName() =>
        (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
}
