using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Catalog;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Catalog;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Catalog;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Catalog;

namespace TTSmartEcom.Api.Controllers.Catalog;

[ApiController]
[Route("chips")]
public sealed class CatalogWriteController(CatalogWriteService catalog) : ControllerBase
{
    [HttpGet("getValues")]
    [AllowAnonymous]
    public async Task<IActionResult> GetChipValues(CancellationToken cancellationToken)
    {
        ChipValuesRecord? value = await catalog.GetChipValuesAsync(cancellationToken);
        return value is null ? BadRequest(new { message = "No chip found" }) : Ok(value);
    }

    [HttpPost("addValue")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> AddChipValue(ChipValueMutationRequest request, CancellationToken cancellationToken)
    {
        CatalogMutationResult<ChipValuesRecord> result = await catalog.AddChipValueAsync(request.Type, request.Value, cancellationToken);
        return result.Status == CatalogMutationStatus.Success
            ? Ok(new { message = $"{request.Type} added successfully" }) : ResultError(result);
    }

    [HttpPost("removeValue")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> RemoveChipValue(ChipValueMutationRequest request, CancellationToken cancellationToken)
    {
        CatalogMutationResult<ChipValuesRecord> result = await catalog.RemoveChipValueAsync(request.Type, request.Value, cancellationToken);
        return result.Status == CatalogMutationStatus.Success ? Ok(new { message = "Xóa thành công" }) : ResultError(result);
    }

    [HttpPost("brands")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> CreateBrand(BrandMutationRequest request, CancellationToken cancellationToken)
    {
        CatalogMutationResult<BrandRecord> result = await catalog.CreateBrandAsync(
            request.Brand, ActorName(), cancellationToken);
        return result.Status == CatalogMutationStatus.Success
            ? StatusCode(201, CatalogContractMapper.Map(result.Value!)) : ResultError(result);
    }

    [HttpDelete("brands/{id}")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> DeleteBrand(string id, CancellationToken cancellationToken)
    {
        CatalogMutationResult<BrandRecord> result = await catalog.DeleteBrandAsync(id, ActorName(), cancellationToken);
        return result.Status == CatalogMutationStatus.Success ? Ok(new { message = "Brand deleted" }) : ResultError(result);
    }

    [HttpPost("section")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> CreateSection(SectionMutationRequest request, CancellationToken cancellationToken) =>
        SectionResult(await catalog.CreateSectionAsync(request.Name, ActorName(), cancellationToken), 201);

    [HttpPut("section/{oldName}")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> RenameSection(string oldName, SectionMutationRequest request, CancellationToken cancellationToken) =>
        SectionResult(await catalog.RenameSectionAsync(oldName, request.Name, ActorName(), cancellationToken), 200);

    [HttpDelete("section/{name}")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> DeleteSection(string name, CancellationToken cancellationToken) =>
        SectionResult(await catalog.DeleteSectionAsync(name, ActorName(), cancellationToken), 200);

    [HttpPost("{name}/value")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> AddSectionValue(string name, SectionValueMutationRequest request, CancellationToken cancellationToken) =>
        SectionResult(await catalog.AddSectionValueAsync(name, request.Value, ActorName(), cancellationToken), 200);

    [HttpPut("{name}/value")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> UpdateSectionValue(string name, SectionValueMutationRequest request, CancellationToken cancellationToken) =>
        SectionResult(await catalog.UpdateSectionValueAsync(
            name, request.OldValue, request.NewValue, request.ImgUrl, ActorName(), cancellationToken), 200);

    [HttpDelete("{name}/value")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> DeleteSectionValue(string name, SectionValueMutationRequest request, CancellationToken cancellationToken) =>
        SectionResult(await catalog.DeleteSectionValueAsync(name, request.Value, ActorName(), cancellationToken), 200);

    private ObjectResult SectionResult(CatalogMutationResult<SectionDocumentRecord> result, int status) =>
        result.Status == CatalogMutationStatus.Success
            ? StatusCode(status, CatalogContractMapper.Map(result.Value!)) : ResultError(result);

    private ObjectResult ResultError<T>(CatalogMutationResult<T> result) => StatusCode(result.Status switch
    {
        CatalogMutationStatus.NotFound => 404, CatalogMutationStatus.Conflict => 409,
        CatalogMutationStatus.Invalid => 400, _ => 500,
    }, new { message = result.Message ?? "Invalid request" });

    private string? ActorName() =>
        (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
}

[ApiController]
[Route("chips/types")]
public sealed class LegacyChipTypeController(
    ProductCatalogReadService reads,
    ProductCatalogWriteService writes) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await reads.ListTypesAsync(cancellationToken));

    [HttpPost]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> Create(ProductTypeMutationRequest request, CancellationToken cancellationToken)
    {
        var result = await writes.CreateTypeAsync(
            request.Type, null, ActorName(), includeIconInAudit: false, cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? StatusCode(201, result.ProductType) : StatusCode(result.Status == ProductMutationStatus.Conflict ? 409 : 400,
                new { message = result.Message });
    }

    [HttpDelete("{id}")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        var result = await writes.DeleteTypeAsync(id, false, ActorName(), cancellationToken);
        return result.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Type deleted" }) : StatusCode(result.Status == ProductMutationStatus.NotFound ? 404 : 400,
                new { message = result.Message });
    }

    private string? ActorName() =>
        (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
}
