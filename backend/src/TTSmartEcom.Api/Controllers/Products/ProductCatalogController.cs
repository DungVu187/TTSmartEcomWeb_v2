using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products")]
public sealed class ProductCatalogController(ProductCatalogReadService products) : ControllerBase
{
    [HttpGet("types")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductTypeResponse>>> Types(CancellationToken cancellationToken)
    {
        var values = await products.ListTypesAsync(cancellationToken);
        return Ok(values.Select(value => new ProductTypeResponse(value.Id, value.Type, value.Icon, value.CreatedAt, value.UpdatedAt)).ToArray());
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductListResponse>> List(CancellationToken cancellationToken)
    {
        var query = Request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault(), StringComparer.OrdinalIgnoreCase);
        var result = await products.ListAsync(query, Viewer(), cancellationToken);
        return Ok(new ProductListResponse(result.Total, result.Page, result.Limit,
            result.Products.Select(ProductResponse.FromListing).ToArray()));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> Get(string id, CancellationToken cancellationToken)
    {
        var value = await products.GetByIdAsync(id, Viewer(), cancellationToken);
        return value is null ? NotFound(new { message = "Product not found" }) : Ok(ProductResponse.From(value));
    }

    [HttpPost("fetch-by-ids")]
    [AllowAnonymous]
    public async Task<IActionResult> FetchByIds(FetchProductsByIdsRequest request, CancellationToken cancellationToken)
    {
        if (request.Ids is null || request.Ids.Length > 200)
        {
            return BadRequest(new { success = 0, message = "ids must contain at most 200 values" });
        }

        var result = await products.FetchByIdsAsync(request.Ids, Viewer(), cancellationToken);
        if (!result.Valid)
        {
            return BadRequest(new { success = 0, message = "Không có id nào hợp lệ trong mảng" });
        }

        return Ok(new
        {
            success = 1,
            total = result.Products.Count,
            products = result.Products.Select(ProductResponse.From).ToArray(),
        });
    }

    [HttpGet("top-purchased")]
    [AllowAnonymous]
    public async Task<IActionResult> TopPurchased(CancellationToken cancellationToken)
    {
        var result = await products.TopPurchasedAsync(Viewer(), cancellationToken);
        return Ok(result.Select(ProductResponse.From).ToArray());
    }

    [HttpGet("{id}/admin-detail")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> AdminDetail(string id, CancellationToken cancellationToken)
    {
        ProductViewer viewer = Viewer() is { } current
            ? current with { Role = "admin" }
            : new ProductViewer("admin");
        var value = await products.GetByIdAsync(
            id, viewer, cancellationToken, includePrivate: true);
        return value is null ? NotFound(new { message = "Product not found" }) : Ok(ProductResponse.From(value));
    }

    private ProductViewer? Viewer()
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        ICurrentUserContext? scope = HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
        return identity is null && scope is null
            ? null
            : new ProductViewer(
                identity?.Role,
                identity?.StationIds,
                scope?.ActiveCompanyId,
                scope?.ActiveBranchId);
    }
}
