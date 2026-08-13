using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Products;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Products;

namespace TTSmartEcom.Api.Controllers.Products;

[ApiController]
[Route("products")]
public sealed class ProductAiController(
    IProductAiProvider provider,
    ProductInvoiceMatchingService invoiceMatching,
    IFileValidationService validation,
    LocalMediaFileService mediaFiles,
    IOptions<LegacyCompatibilityOptions> compatibility) : ControllerBase
{
    private const string MissingGeminiMessage =
        "Vui lòng cấu hình GEMINI_API_KEY hợp lệ trước khi sử dụng tính năng này.";

    [HttpPost("scan-invoice")]
    [Authorize(Roles = "superadmin,admin,staff")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ScanInvoice(
        [FromForm] InvoiceScanRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanScanInvoice())
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied, missing scan AI permission" });
        if (!provider.IsConfigured) return MissingProvider();
        if (request.Invoice is null)
            return BadRequest(new { success = 0, message = "Không có file ảnh được tải lên." });
        FileValidationResult valid = Validate(request.Invoice, FileUploadKind.Invoice, cancellationToken);
        if (!valid.IsValid) return BadRequest(new { success = 0, message = InvoiceValidationMessage(valid.ErrorCode) });

        byte[] content = await ReadBoundedAsync(request.Invoice, 5L * 1024 * 1024, cancellationToken);
        ProductAiResult analysis = await provider.AnalyzeInvoiceAsync(
            request.Invoice.ContentType,
            content,
            cancellationToken);
        if (analysis.Status != ProductAiStatus.Success) return ProviderFailure("TTS-PRODUCT-SCAN-INVOICE-0503");
        ProductInvoiceMatchResult? matched = await invoiceMatching.MatchAsync(analysis.Payload, cancellationToken);
        if (matched is null) return ProviderFailure("TTS-PRODUCT-SCAN-INVOICE-INVALID");

        LocalMediaSaveResult saved;
        try
        {
            saved = await mediaFiles.SaveAsync(request.Invoice, FileUploadKind.Invoice, "invoices",
                "invoice-scan-", "invoice-images", cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return StatusCode(500, new { success = 0, message = "Đã xảy ra lỗi khi lưu ảnh hóa đơn." });
        }
        if (!saved.IsSuccess) return BadRequest(new { success = 0, message = InvoiceValidationMessage(saved.ErrorCode) });

        return Ok(new
        {
            success = 1,
            imageUrl = saved.PublicUrl,
            total = matched.Items.Count,
            items = matched.Items,
        });
    }

    [HttpPost("voice-query")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> VoiceQuery(
        [FromForm] VoiceAudioRequest request,
        CancellationToken cancellationToken)
    {
        if (!provider.IsConfigured) return MissingProvider();
        if (request.Audio is null)
            return BadRequest(new { success = 0, message = "Không nhận được file âm thanh nào." });
        FileValidationResult valid = Validate(request.Audio, FileUploadKind.VoiceAudio, cancellationToken);
        if (!valid.IsValid) return BadRequest(new { success = 0, message = VoiceValidationMessage(valid.ErrorCode) });
        byte[] content = await ReadBoundedAsync(request.Audio, 10L * 1024 * 1024, cancellationToken);
        string contentType = request.Audio.ContentType == "application/octet-stream" ? "audio/mp4" : request.Audio.ContentType;
        ProductAiResult analysis = await provider.AnalyzeVoiceAsync(contentType, content, cancellationToken);
        if (analysis.Status != ProductAiStatus.Success) return ProviderFailure("TTS-PRODUCT-VOICE-AUDIO-0503");
        return VoiceResult(ProductVoiceQueryService.FromProvider(analysis.Payload));
    }

    [HttpPost("voice-query-text")]
    [Authorize]
    public IActionResult VoiceQueryText(VoiceTextRequest request)
    {
        string text = request.Text?.Trim() ?? string.Empty;
        if (text.Length == 0) return BadRequest(new { success = 0, message = "Vui lòng nhập câu tìm kiếm." });
        if (text.Length > 1_000) return BadRequest(new { success = 0, message = "Câu tìm kiếm quá dài." });
        return VoiceResult(ProductVoiceQueryService.FromText(text));
    }

    private OkObjectResult VoiceResult(VoiceQueryResult value) => value.HistoryExport is null
        ? Ok(new { success = 1, transcript = value.Transcript, keyword = value.Keyword, intent = value.Intent, filters = value.Filters })
        : Ok(new { success = 1, transcript = value.Transcript, keyword = value.Keyword, intent = value.Intent, filters = value.Filters, historyExport = value.HistoryExport });

    private BadRequestObjectResult MissingProvider() => BadRequest(new { success = 0, message = MissingGeminiMessage });

    private ObjectResult ProviderFailure(string code)
    {
        Response.Headers["X-Error-Code"] = code;
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            success = 0,
            message = "Dịch vụ phân tích AI hiện không khả dụng.",
            error = "Lỗi server",
        });
    }

    private bool CanScanInvoice()
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        if (identity is null || identity.Role is not ("superadmin" or "admin" or "staff")) return false;
        return identity.Role == "superadmin"
            || identity.Role == "admin" && compatibility.Value.AdminFullAccess
            || identity.Permissions.Contains("order.scan_ai", StringComparer.Ordinal)
            || identity.Permissions.Contains("iporder.scan_ai", StringComparer.Ordinal)
            || identity.Permissions.Contains("eporder.scan_ai", StringComparer.Ordinal);
    }

    private FileValidationResult Validate(
        IFormFile file,
        FileUploadKind kind,
        CancellationToken cancellationToken)
    {
        using Stream content = file.OpenReadStream();
        cancellationToken.ThrowIfCancellationRequested();
        return validation.Validate(file.FileName, file.ContentType, file.Length, content, kind);
    }

    private static async Task<byte[]> ReadBoundedAsync(IFormFile file, long maximum, CancellationToken cancellationToken)
    {
        if (file.Length > maximum) throw new InvalidDataException("File exceeds the validated size limit.");
        await using Stream source = file.OpenReadStream();
        using MemoryStream destination = new((int)file.Length);
        await source.CopyToAsync(destination, cancellationToken);
        if (destination.Length > maximum) throw new InvalidDataException("File exceeds the validated size limit.");
        return destination.ToArray();
    }

    private static string InvoiceValidationMessage(string? errorCode) => errorCode switch
    {
        "TTS-UPLOAD-0003" => "File quá lớn.",
        "TTS-UPLOAD-0004" or "TTS-UPLOAD-0005" or "TTS-UPLOAD-0006" => "Chỉ chấp nhận file ảnh (jpg, png, webp).",
        _ => "File ảnh không hợp lệ.",
    };

    private static string VoiceValidationMessage(string? errorCode) => errorCode switch
    {
        "TTS-UPLOAD-0003" => "File âm thanh quá lớn.",
        "TTS-UPLOAD-0004" or "TTS-UPLOAD-0005" => "Chỉ chấp nhận file âm thanh.",
        _ => "File âm thanh không hợp lệ.",
    };
}
