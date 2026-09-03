using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Users;

public sealed record FeatureSettingRequest(bool IsEnabled);

[ApiController]
[Authorize]
[Route("control-plane/companies/{companyId:guid}/features")]
public sealed class PlatformFeatureAdministrationController(CompanyAccountAdministrationService accounts) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid companyId, [FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { features = await accounts.ListFeatureSettingsAsync(companyId, branchId, context, cancellationToken) });
    }

    [HttpPut("{featureId:guid}")]
    public async Task<IActionResult> SetCompany(
        Guid companyId, Guid featureId, FeatureSettingRequest request, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        bool changed = await accounts.SetFeatureAsync(companyId, null, featureId, request.IsEnabled, context, CorrelationId(), cancellationToken);
        return Ok(new { message = "Đã cập nhật chức năng của công ty", changed });
    }

    [HttpPut("{featureId:guid}/branches/{branchId:guid}")]
    public async Task<IActionResult> SetBranch(
        Guid companyId, Guid featureId, Guid branchId, FeatureSettingRequest request, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        bool changed = await accounts.SetFeatureAsync(companyId, branchId, featureId, request.IsEnabled, context, CorrelationId(), cancellationToken);
        return Ok(new { message = "Đã cập nhật giới hạn chức năng của chi nhánh", changed });
    }

    private ICurrentUserContext? CurrentContext() => HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
    private Guid CorrelationId() => HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string value && Guid.TryParse(value, out Guid id) ? id : Guid.NewGuid();
}
