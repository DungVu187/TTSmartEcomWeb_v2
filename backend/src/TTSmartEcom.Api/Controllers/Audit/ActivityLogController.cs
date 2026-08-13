using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Domain.Audit;

namespace TTSmartEcom.Api.Controllers.Audit;

[ApiController]
[Route("activity-logs")]
public sealed class ActivityLogController(IAuditRepository audit) : ControllerBase
{
    [HttpGet]
    [PermissionAuthorize("activitylog.view")]
    public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int limit = 20, [FromQuery] string? startDate = null, [FromQuery] string? endDate = null, [FromQuery] string? userName = null, [FromQuery] string? productName = null, [FromQuery] string? action = null, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 10000);
        limit = limit is 20 or 50 or 100 ? limit : 20;
        DateTimeOffset? start = DateTimeOffset.TryParse(startDate, out DateTimeOffset s) ? s : null;
        DateTimeOffset? end = DateTimeOffset.TryParse(endDate, out DateTimeOffset e) ? e : null;
        ActivityLogPage result = await audit.QueryAsync(new ActivityLogQuery(page, limit, start, end, userName, productName, action), ct);
        return Ok(result);
    }
}
