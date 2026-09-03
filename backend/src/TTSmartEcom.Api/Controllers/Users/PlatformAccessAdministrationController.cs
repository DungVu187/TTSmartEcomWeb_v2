using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Users;

[ApiController]
[Authorize]
[Route("control-plane")]
public sealed class PlatformAccessAdministrationController(CompanyAccountAdministrationService accounts) : ControllerBase
{
    [HttpGet("companies")]
    public async Task<IActionResult> Companies(CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { companies = await accounts.ListCompaniesAsync(context, cancellationToken) });
    }

    [HttpGet("users/search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string? query, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { users = await accounts.SearchUsersAsync(query, false, context, cancellationToken) });
    }

    [HttpGet("users/lookup")]
    public async Task<IActionResult> LookupUser([FromQuery] string? identifier, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { users = await accounts.SearchUsersAsync(identifier, true, context, cancellationToken) });
    }

    [HttpGet("companies/{companyId:guid}/branches")]
    public async Task<IActionResult> Branches(Guid companyId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { branches = await accounts.ListPlatformBranchesAsync(companyId, context, cancellationToken) });
    }

    private ICurrentUserContext? CurrentContext() =>
        HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;
}
