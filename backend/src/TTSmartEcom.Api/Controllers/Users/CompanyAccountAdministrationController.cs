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

    [HttpGet("permissions")]
    public async Task<IActionResult> ListPermissions(Guid companyId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { permissions = await accounts.ListEffectivePermissionsAsync(companyId, context, cancellationToken) });
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(
        Guid companyId, CompanyRoleSaveRequest request, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        CompanyRoleDefinition role = await accounts.SaveRoleAsync(companyId, null, request.Name, request.Description,
            request.ScopeType, request.PermissionIds, context, CorrelationId(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new { message = "Tạo vai trò thành công", role = CompanyRoleResponse.From(role) });
    }

    [HttpPut("roles/{roleId:guid}")]
    public async Task<IActionResult> UpdateRole(
        Guid companyId, Guid roleId, CompanyRoleSaveRequest request, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        CompanyRoleDefinition role = await accounts.SaveRoleAsync(companyId, roleId, request.Name, request.Description,
            request.ScopeType, request.PermissionIds, context, CorrelationId(), cancellationToken);
        return Ok(new { message = "Cập nhật vai trò thành công", role = CompanyRoleResponse.From(role) });
    }

    [HttpGet("{userId}/branches")]
    public async Task<IActionResult> ListBranches(
        Guid companyId, string userId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { branches = await accounts.ListBranchesForUserAsync(companyId, userId, context, cancellationToken) });
    }

    [HttpGet("branches/{branchId:guid}/users")]
    public async Task<IActionResult> ListBranchUsers(
        Guid companyId, Guid branchId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { users = await accounts.ListBranchMembershipsAsync(companyId, branchId, context, cancellationToken) });
    }

    [HttpGet("branches/{branchId:guid}/roles")]
    public async Task<IActionResult> ListBranchRoles(
        Guid companyId, Guid branchId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        return Ok(new { roles = (await accounts.ListBranchRolesAsync(companyId, branchId, context, cancellationToken))
            .Select(CompanyRoleResponse.From).ToArray() });
    }

    [HttpPut("{userId}/branches/{branchId:guid}")]
    public async Task<IActionResult> SaveBranch(
        Guid companyId, string userId, Guid branchId, BranchMembershipSaveRequest request,
        CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        bool changed = await accounts.SaveBranchMembershipAsync(companyId, branchId, userId, request.RoleId,
            request.IsPrimary, context, CorrelationId(), cancellationToken);
        return Ok(new { message = "Cập nhật quyền truy cập chi nhánh thành công", changed });
    }

    [HttpDelete("{userId}/branches/{branchId:guid}")]
    public async Task<IActionResult> RevokeBranch(
        Guid companyId, string userId, Guid branchId, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        bool changed = await accounts.RevokeBranchMembershipAsync(
            companyId, branchId, userId, context, CorrelationId(), cancellationToken);
        return Ok(new { message = changed ? "Đã ngừng quyền truy cập chi nhánh" : "Quyền truy cập đã được ngừng trước đó", changed });
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
            message = "Cập nhật quyền truy cập công ty thành công",
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
                ? "Ngừng quyền truy cập công ty thành công"
                : "Quyền truy cập công ty đã được ngừng trước đó",
            changed,
        });
    }

    [HttpPut("{userId}/status")]
    public async Task<IActionResult> SetStatus(
        Guid companyId, string userId, MembershipStatusRequest request, CancellationToken cancellationToken)
    {
        if (CurrentContext() is not { } context) return Unauthorized();
        bool changed = await accounts.SetMembershipStatusAsync(
            companyId, userId, request.IsActive, context, CorrelationId(), cancellationToken);
        return Ok(new { message = request.IsActive ? "Đã mở lại quyền truy cập" : "Đã tạm khóa quyền truy cập", changed });
    }

    private ICurrentUserContext? CurrentContext() =>
        HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] as ICurrentUserContext;

    private Guid CorrelationId() =>
        HttpContext.Items[CorrelationIdMiddleware.ItemKey] is string value && Guid.TryParse(value, out Guid correlationId)
            ? correlationId
            : Guid.NewGuid();
}
