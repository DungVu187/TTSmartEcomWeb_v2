using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Users.Requests;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Security;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.Api.Controllers.Users;

[ApiController]
[Route("users")]
public sealed class UserProfileController(IUserProfileRepository repository) : ControllerBase
{
    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        if (HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] is ICurrentUserContext context
            && context.IsControlPlaneIdentity)
        {
            return Ok(ToControlPlaneProfile(context));
        }

        return await ExecuteForCurrentAsync(async id => (await repository.FindProfileAsync(id, ct)) is { } profile
            ? Ok(profile)
            : NotFound(new { message = "Không tìm thấy người dùng" }), ct);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => (await repository.UpdateProfileAsync(id, request.Name, request.Email, ct)) is { } profile ? Ok(new { message = "Cập nhật thông tin cá nhân thành công", user = profile }) : NotFound(new { message = "Không tìm thấy người dùng" }), ct);

    [HttpPost("profile/addresses")]
    [Authorize]
    public async Task<IActionResult> AddAddress(AddressRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => (await repository.AddAddressAsync(id, new UserAddress(string.Empty, request.Label, request.ReceiverName, request.ReceiverPhone, request.AddressDetail, false), ct)) is { } addresses ? StatusCode(201, new { message = "Thêm địa chỉ thành công", addresses }) : NotFound(new { message = "Không tìm thấy người dùng" }), ct);

    [HttpPut("profile/addresses/{addressId}")]
    [Authorize]
    public async Task<IActionResult> UpdateAddress(string addressId, AddressRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => await AddressResultAsync(repository.UpdateAddressAsync(id, addressId, new UserAddressPatch(request.Label, request.ReceiverName, request.ReceiverPhone, request.AddressDetail), ct), "Cập nhật địa chỉ thành công", ct), ct);

    [HttpDelete("profile/addresses/{addressId}")]
    [Authorize]
    public async Task<IActionResult> DeleteAddress(string addressId, CancellationToken ct) => await ExecuteForCurrentAsync(async id => await AddressResultAsync(repository.DeleteAddressAsync(id, addressId, ct), "Xóa địa chỉ thành công", ct), ct);

    [HttpPut("profile/addresses/{addressId}/default")]
    [Authorize]
    public async Task<IActionResult> SetDefault(string addressId, CancellationToken ct) => await ExecuteForCurrentAsync(async id => await AddressResultAsync(repository.SetDefaultAddressAsync(id, addressId, ct), "Đã đặt địa chỉ làm mặc định", ct), ct);

    [HttpGet("order-templates")]
    [Authorize]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        // A platform/control-plane identity is not required to have a row in
        // the Operational Users table.  The admin UI still calls this legacy
        // endpoint while opening inventory pages, so an empty template list is
        // the compatible response instead of a misleading 404.
        if (HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] is ICurrentUserContext context
            && context.IsControlPlaneIdentity)
        {
            return Ok(new { orderTemplates = Array.Empty<object>() });
        }

        return await ExecuteForCurrentAsync(async id =>
            (await repository.GetOrderTemplatesAsync(id, ct)) is { } templates
                ? Ok(new { orderTemplates = templates })
                : NotFound(new { message = "User not found" }), ct);
    }

    [HttpPost("order-templates")]
    [Authorize]
    public async Task<IActionResult> AddTemplate(TemplateRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => { UserOrderTemplate? t = await repository.AddOrderTemplateAsync(id, request.DisplayName, MapProducts(request.Products), ct); return t is null ? NotFound(new { message = "User not found" }) : StatusCode(201, new { index = 0, orderTemplate = t }); }, ct);

    [HttpPut("order-template/{index:int}/display-name")]
    [Authorize]
    public async Task<IActionResult> UpdateTemplateName(int index, TemplateRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => { UserOrderTemplate? t = await repository.UpdateOrderTemplateAsync(id, index, request.DisplayName, null, ct); return t is null ? NotFound(new { message = "Order template index out of range" }) : Ok(new { message = "Display name updated successfully", orderTemplate = t }); }, ct);

    [HttpPut("order-template/{index:int}/products")]
    [Authorize]
    public async Task<IActionResult> UpdateTemplateProducts(int index, TemplateRequest request, CancellationToken ct) => await ExecuteForCurrentAsync(async id => { UserOrderTemplate? t = await repository.UpdateOrderTemplateAsync(id, index, null, MapProducts(request.Products), ct); return t is null ? NotFound(new { message = "Order template index out of range" }) : Ok(new { message = "Products updated successfully", orderTemplate = t }); }, ct);

    [HttpDelete("order-template/{index:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteTemplate(int index, CancellationToken ct) => await ExecuteForCurrentAsync(async id => await repository.DeleteOrderTemplateAsync(id, index, ct) ? Ok(new { message = "Order template deleted successfully" }) : NotFound(new { message = "Order template index out of range" }), ct);

    [HttpGet("my-stations")]
    [Authorize]
    public async Task<IActionResult> MyStations(CancellationToken ct)
    {
        if (HttpContext.Items[CurrentUserContextMiddleware.ContextItemKey] is ICurrentUserContext context
            && context.IsControlPlaneIdentity)
        {
            return Ok(new { stations = Array.Empty<string>() });
        }

        return await ExecuteForCurrentAsync(async id => (await repository.FindProfileAsync(id, ct)) is { } p ? Ok(new { stations = p.Stations }) : NotFound(new { message = "Không tìm thấy người dùng" }), ct);
    }

    private async Task<IActionResult> ExecuteForCurrentAsync(Func<string, Task<IActionResult>> operation, CancellationToken ct)
    {
        UserIdentitySnapshot? identity = HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
        if (identity is null) return Unauthorized(new { message = "Access denied, no token provided" });
        return await operation(identity.Id);
    }
    private async Task<IActionResult> AddressResultAsync(Task<IReadOnlyList<UserAddress>?> task, string message, CancellationToken ct)
    {
        IReadOnlyList<UserAddress>? addresses = await task;
        return addresses is null ? NotFound(new { message = "Không tìm thấy người dùng" }) : addresses.Count == 0 ? NotFound(new { message = "Không tìm thấy địa chỉ" }) : new OkObjectResult(new { message, addresses });
    }
    private static UserTemplateProduct[] MapProducts(IReadOnlyList<TemplateProductRequest>? values) => values?.Select(x => new UserTemplateProduct(x.ProductId, x.Quantity ?? 1)).ToArray() ?? [];

    private static object ToControlPlaneProfile(ICurrentUserContext context)
    {
        string[] permissions = ActivePermissions(context);
        return new
        {
            _id = context.UserId?.ToString(),
            email = context.Email,
            phone = context.Phone ?? string.Empty,
            name = context.DisplayName,
            role = context.IsPlatformSuperAdmin ? SystemRoles.SuperAdmin : SystemRoles.Staff,
            functions = Array.Empty<string>(),
            permissions,
            station = Array.Empty<string>(),
            addresses = Array.Empty<object>(),
            orderTemplate = Array.Empty<object>(),
            isControlPlaneIdentity = true,
            isPlatformSuperAdmin = context.IsPlatformSuperAdmin,
            activeCompanyId = context.ActiveCompanyId,
            activeBranchId = context.ActiveBranchId,
            requiresWorkspaceSelection = !context.IsPlatformSuperAdmin
                && !context.ActiveCompanyId.HasValue,
            companyMemberships = context.CompanyMemberships.Select(company => new
            {
                companyId = company.CompanyId,
                companyCode = company.CompanyCode,
                name = company.CompanyDisplayName,
                roles = company.Roles,
                permissions = company.Permissions,
                isActive = context.ActiveCompanyId == company.CompanyId,
            }).ToArray(),
            branchMemberships = context.BranchMemberships.Select(branch => new
            {
                companyId = branch.CompanyId,
                branchId = branch.BranchId,
                branchCode = branch.BranchCode,
                name = branch.BranchName,
                roles = branch.Roles,
                permissions = branch.Permissions,
                isPrimaryBranch = branch.IsPrimaryBranch,
                isActive = context.ActiveBranchId == branch.BranchId,
            }).ToArray(),
        };
    }

    private static string[] ActivePermissions(ICurrentUserContext context)
    {
        if (context.IsPlatformSuperAdmin)
        {
            return context.Permissions.ToArray();
        }

        if (context.ActiveBranchId is Guid activeBranchId)
        {
            BranchMembershipContext? branch = context.BranchMemberships.FirstOrDefault(item => item.BranchId == activeBranchId);
            if (branch is null) return [];

            IEnumerable<string> companyPermissions = context.CompanyMemberships
                .Where(item => item.CompanyId == branch.CompanyId)
                .SelectMany(item => item.Permissions);
            return branch.Permissions.Concat(companyPermissions).Distinct(StringComparer.Ordinal).ToArray();
        }

        if (context.ActiveCompanyId is Guid activeCompanyId)
        {
            return context.CompanyMemberships
                .Where(item => item.CompanyId == activeCompanyId)
                .SelectMany(item => item.Permissions)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return [];
    }
}
