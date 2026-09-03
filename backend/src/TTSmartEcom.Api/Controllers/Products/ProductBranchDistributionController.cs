using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Products;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products/distribution")]
public sealed class ProductBranchDistributionController(ProductBranchDistributionService distribution)
    : ControllerBase
{
    [HttpGet("branches")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> ListActiveBranches(CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        IReadOnlyList<Application.Abstractions.Products.ActiveCompanyBranch> branches =
            await distribution.ListActiveBranchesAsync(context, cancellationToken);
        return Ok(new
        {
            branches = branches.Select(branch => new ProductDistributionBranchResponse(
                branch.BranchId,
                branch.CompanyId,
                branch.BranchCode,
                branch.Name)).ToArray(),
        });
    }

    [HttpPost("status")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Status(
        ProductBranchDistributionStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        IReadOnlyList<Application.Abstractions.Products.ProductBranchDistributionStatus> statuses =
            await distribution.GetDistributionStatusAsync(request.ProductIds, context, cancellationToken);
        return Ok(new { branches = statuses.Select(status => new ProductBranchDistributionStatusResponse(
            status.BranchId, status.AssignedCount, status.SelectedCount,
            status.AssignedCount == 0 ? "none" : status.AssignedCount == status.SelectedCount ? "all" : "partial")) });
    }

    [HttpGet("{productId}/branches")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> List(
        string productId,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        IReadOnlyList<ProductBranchAssignment> assignments = await distribution.ListAsync(
            productId, context, cancellationToken);
        return Ok(new
        {
            productId,
            branches = assignments.Select(ProductBranchAssignmentResponse.From).ToArray(),
        });
    }

    [HttpGet("{productId}/branches/{branchId:guid}")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> IsActive(
        string productId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        bool active = await distribution.IsActiveAsync(productId, branchId, context, cancellationToken);
        return Ok(new { productId, branchId, isActive = active });
    }

    [HttpPost("assign")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Assign(
        ProductBranchDistributionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        ProductBranchAssignmentChange result = await distribution.AssignAsync(
            request.ProductIds, request.BranchIds, context, cancellationToken);
        return Ok(new
        {
            message = "Phân phối sản phẩm thành công",
            productIds = result.ProductIds,
            branchIds = result.BranchIds,
            changedCount = result.ChangedCount,
        });
    }

    [HttpPost("revoke")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> Revoke(
        ProductBranchDistributionRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        ProductBranchAssignmentChange result = await distribution.RevokeAsync(
            request.ProductIds, request.BranchIds, context, cancellationToken);
        return Ok(new
        {
            message = "Thu hồi phân phối sản phẩm thành công",
            productIds = result.ProductIds,
            branchIds = result.BranchIds,
            changedCount = result.ChangedCount,
        });
    }

    private ICurrentUserContext? CurrentContext() =>
        HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
}
