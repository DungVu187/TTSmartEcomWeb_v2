using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Contracts.Stations;
using TTSmartEcom.Api.Files;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Files;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Stations;
using TTSmartEcom.Domain.Stations;

namespace TTSmartEcom.Api.Controllers.Stations;

[ApiController]
[Route("stations")]
public sealed partial class StationController(
    IStationRepository stations,
    LocalMediaFileService mediaFiles,
    IOptions<ExternalServicesOptions> externalServices,
    ActivityLogWriteService activityLogs,
    ILogger<StationController> logger) : ControllerBase
{
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string? name, [FromQuery] string? code, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Thiếu tên hoặc mã trạm để tìm kiếm" });
        IReadOnlyList<Station> matches = await stations.SearchExactAsync(name, code, ct);
        return Ok(new { stations = matches.Select(ToPublic).ToArray() });
    }

    [HttpGet("public/{inviteCode}")]
    [AllowAnonymous]
    public async Task<IActionResult> Public(string inviteCode, CancellationToken ct) =>
        await FindByCode(inviteCode, true, ct);

    [HttpGet("code/{code}")]
    [AllowAnonymous]
    public async Task<IActionResult> Code(string code, CancellationToken ct) => await FindByCode(code, false, ct);

    [HttpGet("by-codes")]
    [AllowAnonymous]
    public async Task<IActionResult> ByCodes([FromQuery] string? codes, CancellationToken ct)
    {
        string[] values = (codes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static value => value[..Math.Min(value.Length, 100)])
            .Take(50)
            .ToArray();
        if (values.Length == 0)
        {
            return BadRequest(new { error = "Thiếu danh sách mã trạm" });
        }

        IReadOnlyList<Station> matches = await stations.FindByCodesAsync(values, ct);
        return Ok(matches.Select(ToLegacyStation).ToArray());
    }

    [HttpPost("by-ids")]
    [AllowAnonymous]
    public async Task<IActionResult> ByIds([FromBody] StationIdsRequest? request, CancellationToken ct)
    {
        IReadOnlyList<string>? ids = request?.Ids;
        if (ids is null || ids.Count == 0 || ids.Count > 100 ||
            ids.Any(static id => string.IsNullOrWhiteSpace(id) || id.Length > 100))
        {
            return BadRequest(new { error = "Thiếu hoặc sai định dạng danh sách _id" });
        }

        IReadOnlyList<Station> matches = await stations.FindByIdsAsync(ids, true, ct);
        return Ok(matches.Select(ToLegacyStation).ToArray());
    }

    [HttpGet]
    [PermissionAuthorize("station.view")]
    public async Task<IActionResult> List(CancellationToken ct = default)
    {
        // Legacy GET /stations trả về mảng raw từ Station.find({}), không có
        // envelope phân trang. Repository hiện tại dùng API phân trang dùng
        // chung, nên đọc một trang lớn rồi giữ nguyên hình dạng response cũ.
        StationPage page = await stations.ListAsync(1, 10000, null, ct);
        return Ok(page.Stations.Select(ToLegacyStation).ToArray());
    }

    [HttpPost]
    [PermissionAuthorize("station.create")]
    public async Task<IActionResult> Create(StationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StationName) || string.IsNullOrWhiteSpace(request.StationCode)) return BadRequest(new { error = "Tên và mã station là bắt buộc" });
        Station? station = await stations.CreateAsync(new NewStationData(request.StationName.Trim(), request.StationCode.Trim(), request.Location, request.AllowPublicSignup ?? true), ct);
        if (station is not null && ActorName() is { } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateStation(actorName, station), ct);
        return station is null ? StatusCode(500, new { error = "Không thể tạo station" }) : StatusCode(201, ToLegacyStation(station));
    }

    [HttpPut("{id}")]
    [PermissionAuthorize("station.edit")]
    public async Task<IActionResult> Update(string id, StationRequest request, CancellationToken ct)
    {
        Station? before = await stations.FindByIdAsync(id, ct);
        Station? updated = await stations.UpdateAsync(id,
            new UpdateStationData(request.StationName, request.StationCode, request.Location, request.AllowPublicSignup), ct);
        if (before is not null && updated is not null && ActorName() is { } actorName &&
            ActivityLogEntries.UpdateStation(actorName, before, updated) is { } entry)
            await activityLogs.TryAppendAsync(entry, ct);
        return MapResult(updated);
    }

    [HttpPut("{id}/products")]
    [PermissionAuthorize("station.edit")]
    public async Task<IActionResult> UpdateProducts(string id, StationProductsRequest request, CancellationToken ct)
    {
        if (request.ProductId is null) return BadRequest(new { error = "productId phải là một mảng" });
        Station? before = await stations.FindByIdAsync(id, ct);
        Station? updated = await stations.UpdateProductsAsync(id, request.ProductId.Take(500).ToArray(), ct);
        if (before is not null && updated is not null && ActorName() is { } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.UpdateStationProducts(actorName, before, updated), ct);
        return MapResult(updated);
    }

    [HttpDelete("{id}")]
    [PermissionAuthorize("station.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        Station? before = await stations.FindByIdAsync(id, ct);
        bool deleted = await stations.DeleteAsync(id, ct);
        if (deleted && before is not null && ActorName() is { } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteStation(actorName, before), ct);
        return deleted ? Ok(new { message = "Xóa station thành công" }) : NotFound(new { error = "Không tìm thấy station" });
    }

    [HttpPost("{id}/upload-image")]
    [Consumes("multipart/form-data")]
    [PermissionAuthorize("station.edit")]
    public async Task<IActionResult> UploadImage(string id, IFormFile? station, CancellationToken ct)
    {
        if (station is null)
        {
            return BadRequest(new { message = "Không có file được upload" });
        }

        LocalMediaSaveResult saved;
        try
        {
            saved = await mediaFiles.SaveAsync(
                station,
                FileUploadKind.StationImage,
                "stations",
                "station_",
                "station",
                ct);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStationImageStoreFailure(logger, exception);
            return StatusCode(500, new { message = "Không thể upload ảnh" });
        }
        if (!saved.IsSuccess)
        {
            return BadRequest(new
            {
                message = StationValidationMessage(saved.ErrorCode),
                code = saved.ErrorCode,
            });
        }

        string imageUrl = BuildStationImageUrl(saved.PublicUrl!);
        Station? updated = await stations.UpdateImageAsync(id, imageUrl, ct);
        if (updated is null)
        {
            mediaFiles.Delete(saved.PublicUrl!, "station", "stations");
            return NotFound(new { message = "Không tìm thấy station" });
        }

        return Ok(new { message = "Upload ảnh thành công", imgUrl = imageUrl, station = ToLegacyStation(updated) });
    }

    [HttpDelete("{id}/remove-image")]
    [PermissionAuthorize("station.edit")]
    public async Task<IActionResult> RemoveImage(string id, CancellationToken ct)
    {
        Station? current = await stations.FindByIdAsync(id, ct);
        if (current is null || string.IsNullOrWhiteSpace(current.ImageUrl))
        {
            return NotFound(new { message = "Không tìm thấy ảnh hoặc station" });
        }

        LocalMediaDeleteResult deleted;
        try
        {
            deleted = mediaFiles.Delete(current.ImageUrl, "station", "stations");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogStationImageDeleteFailure(logger, exception, id);
            deleted = LocalMediaDeleteResult.Missing(string.Empty);
        }
        if (!deleted.IsValid)
        {
            return BadRequest(new { message = "Đường dẫn ảnh station không hợp lệ" });
        }

        Station? updated = await stations.RemoveImageAsync(id, ct);
        return updated is null
            ? NotFound(new { message = "Không tìm thấy ảnh hoặc station" })
            : Ok(new { message = "Xoá ảnh thành công", station = ToLegacyStation(updated) });
    }

    private async Task<IActionResult> FindByCode(string code, bool publicOnly, CancellationToken ct)
    {
        Station? station = await stations.FindByCodeAsync(code[..Math.Min(code.Length, 100)], publicOnly, ct);
        return station is null
            ? NotFound(new { error = "Không tìm thấy station với mã này" })
            : Ok(publicOnly ? ToPublic(station) : ToLegacyStation(station));
    }

    private static IActionResult MapResult(Station? station) =>
        station is null ? new NotFoundObjectResult(new { error = "Không tìm thấy station" }) : new OkObjectResult(ToLegacyStation(station));

    private string? ActorName() =>
        (HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot)?.Name;

    private string BuildStationImageUrl(string relativeUrl)
    {
        string? address = externalServices.Value.PublicAddress?.TrimEnd('/');
        string origin = string.IsNullOrWhiteSpace(address) ? $"{Request.Scheme}://{Request.Host}" : address;
        return $"{origin}{relativeUrl}";
    }

    private static string StationValidationMessage(string? errorCode) => errorCode switch
    {
        "TTS-UPLOAD-0003" => "File too large",
        "TTS-UPLOAD-0004" or "TTS-UPLOAD-0005" or "TTS-UPLOAD-0006" => "Chỉ cho phép upload file ảnh!",
        _ => "File ảnh không hợp lệ",
    };

    private static object ToLegacyStation(Station station) => new
    {
        _id = station.Id,
        station.StationName,
        imgUrl = station.ImageUrl,
        station.StationCode,
        station.AllowPublicSignup,
        station.Location,
        productId = station.ProductIds,
        id = station.Id,
        inviteCode = station.StationCode ?? string.Empty,
    };

    [LoggerMessage(EventId = 4401, Level = LogLevel.Error, Message = "Could not store a station image")]
    private static partial void LogStationImageStoreFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4402, Level = LogLevel.Warning, Message = "Could not delete the physical image for station {StationId}")]
    private static partial void LogStationImageDeleteFailure(ILogger logger, Exception exception, string stationId);

    private static object ToPublic(Station station) => new
    {
        _id = station.Id,
        id = station.Id,
        stationName = station.StationName,
        imgUrl = station.ImageUrl,
        stationCode = station.StationCode,
        allowPublicSignup = station.AllowPublicSignup,
        location = station.Location,
        productId = station.ProductIds,
    };
}
