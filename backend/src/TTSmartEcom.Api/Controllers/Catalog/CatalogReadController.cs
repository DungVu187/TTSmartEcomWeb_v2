using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Catalog;
using TTSmartEcom.Application.Catalog;

namespace TTSmartEcom.Api.Controllers.Catalog;

[ApiController]
[Route("chips")]
[AllowAnonymous]
public sealed class CatalogReadController(CatalogReadService catalog) : ControllerBase
{
    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<BrandResponse>>> Brands(CancellationToken cancellationToken) =>
        Ok((await catalog.ListBrandsAsync(cancellationToken)).Select(CatalogContractMapper.Map).ToArray());

    [HttpGet("section")]
    public async Task<ActionResult<IReadOnlyList<string>>> Sections(CancellationToken cancellationToken) =>
        Ok(await catalog.ListSectionNamesAsync(cancellationToken));

    [HttpGet("section-doc")]
    public async Task<IActionResult> SectionDocument(CancellationToken cancellationToken)
    {
        var value = await catalog.GetSectionDocumentAsync(cancellationToken);
        return Ok(value is null ? new { } : CatalogContractMapper.Map(value));
    }

    [HttpGet("{name}/value")]
    public async Task<IActionResult> SectionValues(string name, CancellationToken cancellationToken)
    {
        var value = await catalog.GetSectionValuesAsync(name, cancellationToken);
        return value is null ? NotFound(new { message = "Không tìm thấy section" }) : Ok(value);
    }

    [HttpPost("sections/images")]
    public async Task<IActionResult> SectionImages(SectionImagesRequest request, CancellationToken cancellationToken)
    {
        if (request.Names is null || request.Names.Length > 100)
        {
            return BadRequest(new { message = "names must contain at most 100 values" });
        }

        return Ok(await catalog.GetSectionImagesAsync(request.Names, cancellationToken));
    }
}
