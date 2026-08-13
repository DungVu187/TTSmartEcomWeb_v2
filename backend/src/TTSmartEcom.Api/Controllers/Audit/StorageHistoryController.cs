using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using TTSmartEcom.Api.Configuration;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Api.Controllers.Audit;

[ApiController]
[Route("histories")]
[Authorize]
public sealed class StorageHistoryController(IStorageHistoryRepository history, IOptions<LegacyCompatibilityOptions> compatibility) : ControllerBase
{
    [HttpGet("filter-options")]
    public async Task<IActionResult> FilterOptions(CancellationToken ct) => CanEither() ? Ok(await history.GetFilterOptionsAsync(ct)) : Forbid();

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1, [FromQuery] int limit = 20,
        [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] string? orderName = null, [FromQuery] string? userName = null,
        [FromQuery] string? noteType = null, [FromQuery] string? direction = null,
        [FromQuery] bool exportAll = false, CancellationToken ct = default)
    {
        if (!Can(direction == "export" ? "history_export.view" : "history_import.view")) return Forbid();
        if (page < 1 || limit is not (20 or 50 or 100) || endDate < startDate || endDate - startDate > TimeSpan.FromDays(366)) return BadRequest(new { message = "Invalid history query" });
        return Ok(await history.QueryAsync(new StorageHistoryQuery(page, limit, startDate, endDate, orderName, userName, noteType, direction, exportAll), ct));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> ByProduct(string id, [FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] DateTimeOffset? startDate = null, [FromQuery] DateTimeOffset? endDate = null, CancellationToken ct = default)
    {
        if (!CanEither()) return Forbid();
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || page < 1 || limit is not (20 or 50 or 100) || endDate < startDate || endDate - startDate > TimeSpan.FromDays(366)) return BadRequest(new { message = "Invalid history query" });
        return Ok(await history.QueryProductAsync(id, page, limit, startDate, endDate, ct));
    }

    [HttpPut("update-ordername")]
    public async Task<IActionResult> UpdateOrderName(UpdateOrderNameRequest request, CancellationToken ct)
    {
        if (!CanEither()) return Forbid();
        if (string.IsNullOrWhiteSpace(request.OrderId) || request.OrderId.Length > 100 || string.IsNullOrWhiteSpace(request.NewOrderName) || request.NewOrderName.Length > 200) return BadRequest(new { success = false, message = "Thiếu orderId hoặc newOrderName" });
        long count = await history.UpdateOrderNameAsync(request.OrderId.Trim(), request.NewOrderName.Trim(), ct);
        return Ok(new { success = true, message = $"Đã cập nhật {count} lịch sử với orderId = {request.OrderId.Trim()}" });
    }

    private bool CanEither() => Can("history_import.view") || Can("history_export.view");
    private bool Can(string permission)
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        return identity is not null && (identity.Role == "superadmin" || (identity.Role == "admin" && compatibility.Value.AdminFullAccess) || identity.Permissions.Contains(permission, StringComparer.Ordinal));
    }
}

public sealed record UpdateOrderNameRequest(string? OrderId, string? NewOrderName);
