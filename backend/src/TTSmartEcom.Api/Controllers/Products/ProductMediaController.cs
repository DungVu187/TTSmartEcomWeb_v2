using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Abstractions.Products;
using TTSmartEcom.Application.Products;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products")]
public sealed class ProductMediaController(
    ProductMediaFileService files,
    ProductMediaService products,
    IFileValidationService validation,
    IOptions<ExternalServicesOptions> external,
    IOptions<LegacyCompatibilityOptions> compatibility) : ControllerBase
{
    [HttpPost("upload/image")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage([FromForm] ProductImageUploadRequest request, CancellationToken cancellationToken)
    {
        if (!CanUpload()) return UploadForbidden();
        if (request.Product is null) return BadRequest(new { success = 0, message = "Không có file được upload" });
        using Stream content = request.Product.OpenReadStream();
        FileValidationResult result = validation.Validate(request.Product.FileName, request.Product.ContentType,
            request.Product.Length, content, FileUploadKind.ProductImage);
        if (!result.IsValid) return UploadError(result, FileUploadKind.ProductImage);

        StoredMediaFile stored = await files.SaveAsync(request.Product, ProductMediaFileKind.ProductImage, cancellationToken);
        return Ok(new { success = 1, imgUrl = PublicUrl("images", stored.FileName) });
    }

    [HttpPost("upload/document")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument([FromForm] ProductDocumentUploadRequest request, CancellationToken cancellationToken)
    {
        if (!CanUpload()) return UploadForbidden();
        if (request.Document is null) return BadRequest(new { success = 0, message = "Không có file được upload" });
        using Stream content = request.Document.OpenReadStream();
        FileValidationResult result = validation.Validate(request.Document.FileName, request.Document.ContentType,
            request.Document.Length, content, FileUploadKind.ProductDocument);
        if (!result.IsValid) return UploadError(result, FileUploadKind.ProductDocument);

        StoredMediaFile stored = await files.SaveAsync(request.Document, ProductMediaFileKind.ProductDocument, cancellationToken);
        return Ok(new { success = 1, url = PublicUrl("documents", stored.FileName), fileName = request.Document.FileName });
    }

    [HttpDelete("{id}/{variantIndex}/image")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> DeleteVariantImage(string id, string variantIndex, CancellationToken cancellationToken)
    {
        int parsedVariantIndex = ParseLegacyInteger(variantIndex);
        ProductMediaMutationResult preparation = await products.PrepareVariantImageDeletionAsync(id, parsedVariantIndex, cancellationToken);
        if (preparation.Status != ProductMutationStatus.Success) return MutationError(preparation);

        ResolvedMediaFile resolved;
        try { resolved = files.ResolveProductImage(preparation.ImageUrl!); }
        catch (ProductMediaFileException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }

        if (await products.IsProductImageReferencedElsewhereAsync(id, parsedVariantIndex, resolved.FileName, cancellationToken))
            return Conflict(new { message = "Image is referenced by another product variant" });

        try { await files.DeleteRegularFileIfExistsAsync(resolved, cancellationToken); }
        catch (ProductMediaFileException exception) { return StatusCode(exception.StatusCode, new { message = exception.Message }); }

        ProductMediaMutationResult cleared = await products.ClearVariantImageAsync(id, parsedVariantIndex, preparation.ImageUrl!, cancellationToken);
        return cleared.Status == ProductMutationStatus.Success
            ? Ok(new { message = "Image deleted successfully", product = ProductResponse.From(cleared.Product!) })
            : MutationError(cleared);
    }

    [HttpDelete("clean-temp-image")]
    [PermissionAuthorize("product.edit")]
    public async Task<IActionResult> CleanTemporaryImage([FromQuery] string? imageUrl, CancellationToken cancellationToken)
    {
        if (Request.Query.TryGetValue("imageUrl", out Microsoft.Extensions.Primitives.StringValues values) && values.Count != 1)
            return BadRequest(new { success = 0, message = "Đường dẫn ảnh tạm không hợp lệ." });
        if (string.IsNullOrWhiteSpace(imageUrl))
            return BadRequest(new { success = 0, message = "Thiếu thông tin imageUrl." });

        ResolvedMediaFile resolved;
        try { resolved = files.ResolveTemporaryInvoiceImage(imageUrl); }
        catch (ProductMediaFileException exception)
        {
            return StatusCode(exception.StatusCode, new { success = 0, message = exception.Message });
        }

        if (await products.IsInvoiceImageReferencedAsync(resolved.FileName, cancellationToken))
            return Conflict(new { success = 0, message = "Ảnh tạm đã được sử dụng và không thể xóa." });

        try
        {
            bool deleted = await files.DeleteRegularFileIfExistsAsync(resolved, cancellationToken);
            return Ok(new { success = 1, message = deleted ? "Đã xóa ảnh tạm thành công." : "File không tồn tại hoặc đã được xóa." });
        }
        catch (ProductMediaFileException exception)
        {
            return StatusCode(exception.StatusCode, new { success = 0, message = exception.Message });
        }
    }

    private string PublicUrl(string route, string filename)
    {
        string? configured = external.Value.PublicAddress?.TrimEnd('/');
        string origin = string.IsNullOrWhiteSpace(configured) ? $"{Request.Scheme}://{Request.Host}" : configured;
        return $"{origin}/{route}/{filename}";
    }

    private bool CanUpload()
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        return identity is not null && identity.Role is SystemRoles.SuperAdmin or SystemRoles.Admin or SystemRoles.Staff &&
            (identity.Role == SystemRoles.SuperAdmin ||
             identity.Role == SystemRoles.Admin && compatibility.Value.AdminFullAccess ||
             identity.Permissions.Contains("product.create", StringComparer.Ordinal) ||
             identity.Permissions.Contains("product.edit", StringComparer.Ordinal));
    }

    private ObjectResult UploadForbidden() => StatusCode(StatusCodes.Status403Forbidden, new
    {
        message = "Access denied, missing one of permissions: product.create, product.edit",
    });

    private BadRequestObjectResult UploadError(FileValidationResult validationResult, FileUploadKind kind)
    {
        string message = validationResult.ErrorCode switch
        {
            "TTS-UPLOAD-0003" when kind == FileUploadKind.ProductImage => "Dung lượng ảnh tối đa 4MB",
            "TTS-UPLOAD-0003" => "Dung lượng file tối đa 20MB",
            _ when kind == FileUploadKind.ProductImage => "Chỉ cho phép upload ảnh: .jpg, .jpeg, .png, .webp",
            _ => "Chỉ cho phép upload file PDF",
        };
        return BadRequest(new { success = 0, message });
    }

    private ObjectResult MutationError(ProductMediaMutationResult result) => StatusCode(result.Status switch
    {
        ProductMutationStatus.NotFound => 404,
        ProductMutationStatus.Conflict => 409,
        ProductMutationStatus.Invalid => 400,
        _ => 500,
    }, new { message = result.Message ?? "Invalid request" });

    private static int ParseLegacyInteger(string value)
    {
        int sign = value.StartsWith('-') ? -1 : 1;
        int offset = sign < 0 ? 1 : 0;
        int result = 0;
        bool found = false;
        while (offset < value.Length && char.IsAsciiDigit(value[offset]))
        {
            found = true;
            int digit = value[offset++] - '0';
            if (result > (int.MaxValue - digit) / 10) return -1;
            result = result * 10 + digit;
        }
        return found ? result * sign : -1;
    }
}
