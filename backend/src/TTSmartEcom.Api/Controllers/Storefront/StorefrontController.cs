using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using TTSmartEcom.Api.Contracts.Storefront;
using TTSmartEcom.Api.Contracts.Storefront.Requests;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Storefront;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Application.Abstractions.Files;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Domain.Storefront;

namespace TTSmartEcom.Api.Controllers.Storefront;

[ApiController]
[Route("manages")]
public sealed class StorefrontController(
    IStorefrontRepository storefront,
    LocalMediaFileService mediaFiles,
    ActivityLogWriteService activityLogs,
    IOptions<ExternalServicesOptions> externalServices) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct) => await storefront.GetAsync(ct) is { } value ? Ok(new { success = 1, data = value }) : NotFound(new { success = 0, message = "Chưa có dữ liệu Manage" });

    [HttpGet("policies")]
    [AllowAnonymous]
    public async Task<IActionResult> Policies(CancellationToken ct) => Ok(new { success = 1, data = (await storefront.GetAsync(ct))?.Policies ?? [] });

    [HttpPut("update-introduction")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> Introduction(StorefrontPatchRequest request, CancellationToken ct)
    {
        StorefrontContent data = await storefront.UpsertAsync(new StorefrontPatch(
            Introduction: request.Introduction, IntroductionTranslations: request.IntroductionTranslations), ct);
        await AuditManageAsync("update_introduction", "Trang Giới thiệu", ct,
            request.Introduction is not null ? "introduction" : null,
            request.IntroductionTranslations is not null ? "introductionTranslations" : null);
        return Ok(new { success = 1, data });
    }

    [HttpPut("update-policy")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> Policy(StorefrontPatchRequest request, CancellationToken ct)
    {
        StorefrontContent data = await storefront.UpsertAsync(new StorefrontPatch(MainPolicy: request.MainPolicy), ct);
        await AuditManageAsync("update_policy", "Trang Chính sách", ct,
            request.MainPolicy is not null ? "mainPolicy" : null);
        return Ok(new { success = 1, data });
    }

    [HttpPut("update-policies")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> UpdatePolicies(PoliciesRequest request, CancellationToken ct)
    {
        if (request.Policies is null) return BadRequest(new { message = "policies is required" });
        StorefrontContent data = await storefront.UpdatePoliciesAsync(request.Policies, ct);
        await AuditManageAsync("update_policies", "Trang Chính sách", ct, "policies");
        return Ok(new { success = 1, data });
    }

    [HttpPut("update-partners-text")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> Partners(StorefrontPatchRequest request, CancellationToken ct) => Ok(new { success = 1, data = await storefront.UpsertAsync(new StorefrontPatch(DisplayPartners: request.DisplayPartners, Partners: request.Partners), ct) });

    [HttpPut("update-footer")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> Footer(StorefrontPatchRequest request, CancellationToken ct)
    {
        if (request.FooterContent is null) return BadRequest(new { message = "footerContent is required" });
        StorefrontContent data = await storefront.UpsertAsync(new StorefrontPatch(FooterContent: request.FooterContent), ct);
        await AuditManageAsync("update_homepage_section", "Nội dung footer", ct, "footerContent");
        return Ok(new { success = 1, data });
    }

    [HttpPut("update-home-categories")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> HomeCategories(UpdateHomeCategoriesRequest request, CancellationToken ct)
    {
        StorefrontContent data = await storefront.UpdateHomeCategoriesAsync(new HomeCategoryConfigPatch(
            request.Configured, request.SidebarTitle, request.SidebarTitleTranslations,
            request.ShowSidebar, request.ShowQuickCategories, request.Items), ct);
        await AuditManageAsync("update_home_categories", "Danh mục trang chủ", ct,
            request.Configured.HasValue ? "configured" : null,
            request.SidebarTitle is not null ? "sidebarTitle" : null,
            request.SidebarTitleTranslations is not null ? "sidebarTitleTranslations" : null,
            request.ShowSidebar.HasValue ? "showSidebar" : null,
            request.ShowQuickCategories.HasValue ? "showQuickCategories" : null,
            request.Items is not null ? "items" : null);
        return Ok(new { success = 1, data });
    }

    [HttpPut("update-section/{sectionId}")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> Section(string sectionId, UpdateStorefrontSectionRequest request, CancellationToken ct)
    {
        if (!TryNormalizeSection(sectionId, out string section)) return BadRequest(new { message = "sectionId must be section1 through section11" });
        return Ok(new { success = 1, data = await storefront.UpdateSectionAsync(section, new StorefrontSectionPatch(request.Name, request.NameTranslations, request.ProductId, request.Display, request.Image, request.Link), ct) });
    }

    [HttpPut("update-section{number:int:min(1):max(10)}")]
    [PermissionAuthorize("storefront.manage")]
    public Task<IActionResult> LegacySection(int number, UpdateStorefrontSectionRequest request, CancellationToken ct) => Section($"section{number}", request, ct);

    [HttpPut("update")]
    [PermissionAuthorize("storefront.manage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateImage([FromForm] StorefrontSingleImageRequest request, CancellationToken ct)
    {
        if (request.Manage is null || request.Manage.Count == 0) return BadRequest(new { success = 0, message = "Vui lòng upload một file ảnh" });
        if (request.Manage.Count != 1 || (request.TopPurchaseUrl && request.HighestRatingUrl))
            return BadRequest(new { success = 0, message = "Chỉ được cập nhật một trường: topPurchaseUrl hoặc highestRatingUrl" });
        LocalMediaSaveResult saved = await SaveStorefrontImage(request.Manage[0], ct);
        if (!saved.IsSuccess) return UploadError(saved);
        string imageUrl = PublicUrl(saved.PublicUrl!);
        if (!request.TopPurchaseUrl && !request.HighestRatingUrl)
        {
            await DeleteSavedFileAsync(saved, ct);
            return BadRequest(new { success = 0, message = "Vui lòng chọn trường ảnh cần cập nhật" });
        }
        StorefrontContent data = await storefront.UpsertAsync(new StorefrontPatch(
            TopPurchaseUrl: request.TopPurchaseUrl ? imageUrl : null,
            HighestRatingUrl: request.HighestRatingUrl ? imageUrl : null), ct);
        await AuditManageAsync("update_settings", "Cấu hình chung", ct);
        return Ok(new { success = 1, message = "Cập nhật thành công", data });
    }

    [HttpPost("update-images")]
    [PermissionAuthorize("storefront.manage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateImages([FromForm] StorefrontImagesRequest request, CancellationToken ct) =>
        await AppendImagesAsync(request.Manage, false, ct);

    [HttpPost("update-partners")]
    [PermissionAuthorize("storefront.manage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdatePartners([FromForm] StorefrontImagesRequest request, CancellationToken ct) =>
        await AppendImagesAsync(request.Manage, true, ct);

    [HttpPost("upload-section-image")]
    [PermissionAuthorize("storefront.manage")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadSectionImage([FromForm] StorefrontSectionImageRequest request, CancellationToken ct)
    {
        if (request.Image is null) return BadRequest(new { success = 0, message = "Vui lòng tải lên một file ảnh" });
        LocalMediaSaveResult saved = await SaveStorefrontImage(request.Image, ct);
        return saved.IsSuccess
            ? Ok(new { success = 1, message = "Tải ảnh lên thành công", imgUrl = PublicUrl(saved.PublicUrl!) })
            : UploadError(saved);
    }

    [HttpDelete("delete-image")]
    [PermissionAuthorize("storefront.manage")]
    public async Task<IActionResult> DeleteImage(DeleteStorefrontImageRequest request, CancellationToken ct)
    {
        string? imageUrl = request.ImgUrl ?? request.ImageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl)) return BadRequest(new { success = 0, message = "Vui lòng cung cấp imgUrl hợp lệ để xóa ảnh" });
        if (!await storefront.ContainsImageAsync(imageUrl, ct)) return NotFound(new { success = 0, message = "Ảnh không tồn tại trong dữ liệu" });
        bool removed = await storefront.RemoveImageAsync(imageUrl, ct);
        if (!removed) return Conflict(new { success = 0, message = "Dữ liệu ảnh vừa được thay đổi, vui lòng thử lại" });
        LocalMediaDeleteResult deleted = await mediaFiles.DeleteAsync(imageUrl, "images", "images", ct);
        if (!deleted.IsValid) return BadRequest(new { success = 0, message = "Đường dẫn ảnh không hợp lệ" });
        return Ok(new { success = 1, message = "Xóa ảnh thành công", data = await storefront.GetAsync(ct) });
    }

    private async Task<IActionResult> AppendImagesAsync(List<IFormFile>? files, bool partners, CancellationToken ct)
    {
        if (files is null || files.Count == 0) return BadRequest(new { success = 0, message = "Vui lòng upload ít nhất một file ảnh" });
        if (files.Count > 10) return BadRequest(new { success = 0, message = "Chỉ được upload tối đa 10 file ảnh" });
        List<string> urls = [];
        List<LocalMediaSaveResult> savedFiles = [];
        foreach (IFormFile file in files)
        {
            LocalMediaSaveResult saved = await SaveStorefrontImage(file, ct);
            if (!saved.IsSuccess)
            {
                foreach (LocalMediaSaveResult item in savedFiles) await DeleteSavedFileAsync(item, ct);
                return UploadError(saved);
            }
            savedFiles.Add(saved);
            urls.Add(PublicUrl(saved.PublicUrl!));
        }
        StorefrontContent? current = await storefront.GetAsync(ct);
        IReadOnlyList<string> merged = partners
            ? [.. current?.Partners ?? [], .. urls]
            : [.. current?.OverviewImages ?? [], .. urls];
        StorefrontContent data = await storefront.UpsertAsync(partners
            ? new StorefrontPatch(Partners: merged)
            : new StorefrontPatch(OverviewImages: merged), ct);
        return Ok(new { success = 1, message = partners ? "Cập nhật ảnh đối tác thành công" : "Cập nhật mảng ảnh thành công", data });
    }

    private Task<LocalMediaSaveResult> SaveStorefrontImage(IFormFile file, CancellationToken ct) =>
        mediaFiles.SaveAsync(file, FileUploadKind.StorefrontImage, "images", "manage_", "images", ct);

    private string PublicUrl(string relativeUrl)
    {
        string? address = externalServices.Value.PublicAddress?.TrimEnd('/');
        string origin = string.IsNullOrWhiteSpace(address) ? $"{Request.Scheme}://{Request.Host}" : address;
        return $"{origin}{relativeUrl}";
    }

    private Task<LocalMediaDeleteResult> DeleteSavedFileAsync(LocalMediaSaveResult saved, CancellationToken ct)
    {
        return saved.PublicUrl is null
            ? Task.FromResult(LocalMediaDeleteResult.Invalid())
            : mediaFiles.DeleteAsync(saved.PublicUrl, "images", "images", ct);
    }

    private BadRequestObjectResult UploadError(LocalMediaSaveResult result) => BadRequest(new
    {
        success = 0,
        message = result.ErrorCode == "TTS-UPLOAD-0003" ? "File too large" : "Chỉ cho phép upload file ảnh!",
    });

    private static bool TryNormalizeSection(string value, out string section)
    {
        section = value.Trim().ToLowerInvariant();
        if (!section.StartsWith("section", StringComparison.Ordinal) || !int.TryParse(section[7..], out int number) || number is < 1 or > 11)
        {
            section = string.Empty;
            return false;
        }
        return true;
    }

    private async Task AuditManageAsync(
        string action, string targetName, CancellationToken cancellationToken, params string?[] fields)
    {
        string? actorName =
            (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;
        if (string.IsNullOrWhiteSpace(actorName)) return;
        string[] safeFields = fields.Where(static field => !string.IsNullOrWhiteSpace(field)).Select(static field => field!).ToArray();
        await activityLogs.TryAppendAsync(ActivityLogEntries.Manage(actorName, action, targetName, safeFields), cancellationToken);
    }
}
