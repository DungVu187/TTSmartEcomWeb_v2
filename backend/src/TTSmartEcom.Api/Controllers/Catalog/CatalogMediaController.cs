using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Controllers.Products;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Catalog;

namespace TTSmartEcom.Api.Controllers.Catalog;

[ApiController]
[Route("chips")]
public sealed class CatalogMediaController(
    ProductMediaFileService files,
    CatalogMediaService catalog,
    IFileValidationService validation,
    IOptions<ExternalServicesOptions> external) : ControllerBase
{
    [HttpPost("upload-section-image")]
    [PermissionAuthorize("product.create")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] SectionImageUploadRequest request, CancellationToken cancellationToken)
    {
        if (request.SectionImage is null) return BadRequest(new { message = "No file uploaded" });
        using Stream content = request.SectionImage.OpenReadStream();
        FileValidationResult result = validation.Validate(request.SectionImage.FileName, request.SectionImage.ContentType,
            request.SectionImage.Length, content, FileUploadKind.StorefrontImage);
        if (!result.IsValid) return BadRequest(new { message = result.Message ?? "File ảnh không hợp lệ" });
        StoredMediaFile stored = await files.SaveAsync(request.SectionImage, ProductMediaFileKind.SectionImage, cancellationToken);
        string? configured = external.Value.PublicAddress?.TrimEnd('/');
        string origin = string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured;
        return Ok(new { imgUrl = $"{origin}/section-images/{stored.FileName}" });
    }

    [HttpDelete("delete-section-image/{filename}")]
    [PermissionAuthorize("product.create")]
    public async Task<IActionResult> Delete(string filename, CancellationToken cancellationToken)
    {
        filename = Uri.UnescapeDataString(filename);
        ResolvedMediaFile resolved;
        try { resolved = files.ResolveSectionImageFilename(filename); }
        catch (ProductMediaFileException exception) { return BadRequest(new { message = exception.Message }); }
        if (await catalog.IsSectionImageReferencedAsync(resolved.FileName, cancellationToken))
            return Conflict(new { message = "File is referenced by a section" });
        try
        {
            bool deleted = await files.DeleteRegularFileIfExistsAsync(resolved, cancellationToken);
            return deleted ? Ok(new { message = "File deleted successfully" }) : NotFound(new { message = "File not found" });
        }
        catch (ProductMediaFileException)
        {
            return StatusCode(500, new { message = "Lỗi server khi xóa ảnh phân loại" });
        }
    }
}
