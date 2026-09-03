using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Users;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Api.Controllers.Users;

[ApiController]
[Authorize]
[Route("control-plane/companies/{companyId:guid}/accounts")]
public sealed class CompanyAccountAdministrationController(CompanyAccountAdministrationService accounts)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid companyId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        IReadOnlyList<CompanyAccountMembership> result = await accounts.ListMembershipsAsync(
            companyId, context, cancellationToken);
        return Ok(new
        {
            companyId,
            accounts = result.Select(CompanyAccountResponse.From).ToArray(),
        });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListRoles(Guid companyId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        IReadOnlyList<CompanyRoleDefinition> result = await accounts.ListCompanyRolesAsync(
            companyId, context, cancellationToken);
        return Ok(new
        {
            companyId,
            roles = result.Select(CompanyRoleResponse.From).ToArray(),
        });
    }

    [HttpPut("{userId}/membership")]
    public async Task<IActionResult> Upsert(
        Guid companyId,
        string userId,
        CompanyMembershipUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        CompanyAccountMembership result = await accounts.UpsertMembershipAsync(
            companyId,
            userId,
            request.UserType,
            request.RoleId,
            context,
            CorrelationId(),
            cancellationToken);
        return Ok(new
        {
            message = "Cập nhật phạm vi truy cập Company thành công",
            account = CompanyAccountResponse.From(result),
        });
    }

    [HttpDelete("{userId}/membership")]
    public async Task<IActionResult> Revoke(
        Guid companyId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized(new { message = "Access denied, no token provided" });
        bool changed = await accounts.RevokeMembershipAsync(
            companyId,
            userId,
            context,
            CorrelationId(),
            cancellationToken);
        return Ok(new
        {
            message = changed
                ? "Thu hồi phạm vi truy cập Company thành công"
                : "Phạm vi truy cập Company đã được thu hồi trước đó",
            changed,
        });
    }

    private ICurrentUserContext? CurrentContext() =>
        HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;

    private Guid CorrelationId() =>
        HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string value && Guid.TryParse(value, out Guid correlationId)
            ? correlationId
            : Guid.NewGuid();
}
