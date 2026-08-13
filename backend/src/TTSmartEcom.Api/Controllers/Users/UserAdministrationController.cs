using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using TTSmartEcom.Api.Contracts.Users.Requests;
using TTSmartEcom.Api.Middleware;
using TTSmartEcom.Api.Security;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Audit;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Security;
using TTSmartEcom.Domain.Users;

namespace TTSmartEcom.Api.Controllers.Users;

[ApiController]
[Route("users")]
public sealed class UserAdministrationController(
    IUserProfileRepository repository,
    IPasswordHashWriter passwordHasher,
    ActivityLogWriteService activityLogs,
    ISuperAdminMutationGuard superAdminGuard) : ControllerBase
{
    [HttpGet("all-users")]
    [PermissionAuthorize("account.manage")]
    public async Task<IActionResult> AllUsers(CancellationToken ct)
    {
        UserIdentitySnapshot? actor = Actor();
        return actor is null || !UserAdministrationPolicy.IsAdministrativeActor(actor)
            ? StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied, admin only" })
            : Ok(await repository.ListUsersAsync(actor.Role, false, ct));
    }

    [HttpGet("customers")]
    [PermissionAuthorize("customer.view")]
    public async Task<IActionResult> Customers(CancellationToken ct) =>
        Ok(await repository.ListUsersAsync(Actor()?.Role ?? string.Empty, true, ct));

    [HttpPost("admin-create")]
    [PermissionAuthorize("customer.create")]
    public async Task<IActionResult> Create(CreateAdminUserRequest request, CancellationToken ct)
    {
        UserIdentitySnapshot? actor = Actor();
        if (actor is null) return Unauthorized(new { message = "Access denied, no token provided" });
        if (string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Số điện thoại và mật khẩu là bắt buộc" });
        if (request.Password.Length < 6 || request.Password.Length > 256)
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });

        string phone = CanonicalPhone(request.Phone);
        if (!IsValidPhone(phone)) return InvalidPhone();
        string role = actor.Role == SystemRoles.Staff ? SystemRoles.Customer : request.Role ?? SystemRoles.Customer;
        if (!SystemRoles.All.Contains(role)) return BadRequest(new { message = "Vai trò không hợp lệ" });
        if (!UserAdministrationPolicy.CanCreateRole(actor, role)) return ForbiddenCreateRole(actor.Role);

        IReadOnlyList<string> permissions = [];
        if (role is SystemRoles.Admin or SystemRoles.Staff && request.Permissions is not null)
        {
            PermissionValidationResult validation = UserAdministrationPolicy.ValidateGrantablePermissions(request.Permissions);
            if (!validation.IsValid) return BadRequest(new { message = validation.Message });
            permissions = validation.Permissions;
        }
        await using IAsyncDisposable? guard = await AcquireSuperAdminGuardAsync(role, ct);
        if (role == SystemRoles.SuperAdmin && guard is null) return SuperAdminMutationInProgress();
        if (role == SystemRoles.SuperAdmin && await repository.HasOtherUserWithRoleAsync(SystemRoles.SuperAdmin, null, ct)) return DuplicateSuperAdmin();

        UserSummary? user = await repository.CreateUserAsync(new NewUserData(
            NormalizeEmail(request.Email), phone, request.Name, passwordHasher.Hash(request.Password), role, permissions, NewToken()), ct);
        if (user is not null)
            await activityLogs.TryAppendAsync(ActivityLogEntries.CreateUser(actor.Name!, user), ct);
        return user is null
            ? StatusCode(StatusCodes.Status500InternalServerError, new { message = "Lỗi server" })
            : StatusCode(StatusCodes.Status201Created, new { message = "Tạo tài khoản thành công", user });
    }

    [HttpPut("{id}/permissions")]
    [PermissionAuthorize("account.manage")]
    public async Task<IActionResult> UpdatePermissions(string id, UpdatePermissionsRequest request, CancellationToken ct)
    {
        UserIdentitySnapshot? actor = Actor();
        if (actor is null || !UserAdministrationPolicy.IsAdministrativeActor(actor))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Bạn không có quyền thực hiện chức năng này" });

        UserSummary? current = await repository.FindUserSummaryAsync(id, ct);
        if (current is null) return UserNotFound();
        if (!UserAdministrationPolicy.CanManageTarget(actor, current.Role, UserAdministrationAction.Edit)) return ForbiddenTarget(actor.Role, "chỉnh sửa");

        string finalRole = request.Role ?? current.Role;
        if (!SystemRoles.All.Contains(finalRole)) return BadRequest(new { message = "Vai trò không hợp lệ" });
        if (!UserAdministrationPolicy.CanCreateRole(actor, finalRole)) return ForbiddenAssignRole(actor.Role);
        await using IAsyncDisposable? guard = await AcquireSuperAdminGuardAsync(finalRole, ct);
        if (finalRole == SystemRoles.SuperAdmin && guard is null) return SuperAdminMutationInProgress();
        if (finalRole == SystemRoles.SuperAdmin && await repository.HasOtherUserWithRoleAsync(SystemRoles.SuperAdmin, id, ct)) return DuplicateSuperAdmin();

        PermissionValidationResult permissionValidation = UserAdministrationPolicy.ResolvePermissions(
            current.Role, current.Permissions, request.Role, request.Permissions);
        if (!permissionValidation.IsValid) return BadRequest(new { message = permissionValidation.Message });
        if (request.Phone is not null && !IsValidPhone(CanonicalPhone(request.Phone))) return InvalidPhone();
        if (request.Password is { Length: > 0 } && request.Password.Length is < 6 or > 256)
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự" });

        string? hash = string.IsNullOrWhiteSpace(request.Password) ? null : passwordHasher.Hash(request.Password);
        IReadOnlyList<string>? permissionUpdate = request.Role is null && request.Permissions is null
            ? null
            : permissionValidation.Permissions;
        UserSummary? user = await repository.UpdatePermissionsAsync(id, current.Role, new UserPermissionUpdate(
            request.Role,
            permissionUpdate,
            request.Name,
            NormalizeEmail(request.Email),
            request.Phone is null ? null : CanonicalPhone(request.Phone),
            hash,
            hash is null ? null : NewToken(),
            hash is null ? null : DateTimeOffset.UtcNow), ct);
        if (user is not null && ActivityLogEntries.UpdateUserPermissions(actor.Name!, current, user) is { } entry)
            await activityLogs.TryAppendAsync(entry, ct);
        return user is null ? UserNotFound() : Ok(new { message = "Cập nhật tài khoản thành công", user });
    }

    [HttpPut("{id}")]
    [PermissionAuthorize("customer.edit")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request, CancellationToken ct)
    {
        UserSummary? target = await FindAuthorizedTargetAsync(id, UserAdministrationAction.Edit, ct);
        if (target is null) return await TargetFailureAsync(id, UserAdministrationAction.Edit, "cập nhật", ct);
        if (request.Phone is not null && !IsValidPhone(CanonicalPhone(request.Phone))) return InvalidPhone();
        UserSummary? user = await repository.UpdateUserAsync(id, target.Role, new UserUpdateData(request.Name, NormalizeEmail(request.Email), request.Phone is null ? null : CanonicalPhone(request.Phone)), ct);
        if (user is not null && Actor()?.Name is { Length: > 0 } actorName &&
            ActivityLogEntries.UpdateUser(actorName, target, user) is { } entry)
            await activityLogs.TryAppendAsync(entry, ct);
        return user is null ? UserNotFound() : Ok(new { message = "Cập nhật thông tin người dùng thành công", user });
    }

    [HttpDelete("{id}")]
    [PermissionAuthorize("customer.delete")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        UserSummary? target = await FindAuthorizedTargetAsync(id, UserAdministrationAction.Delete, ct);
        if (target is null) return await TargetFailureAsync(id, UserAdministrationAction.Delete, "xóa", ct);
        bool deleted = await repository.DeleteUserAsync(id, target.Role, ct);
        if (deleted && Actor()?.Name is { Length: > 0 } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.DeleteUser(actorName, target), ct);
        return deleted ? Ok(new { message = "Xóa người dùng thành công" }) : TargetChanged();
    }

    [HttpPost("{id}/rotate-autologin-token")]
    [PermissionAuthorize("customer.edit")]
    public async Task<IActionResult> RotateToken(string id, CancellationToken ct)
    {
        UserSummary? target = await FindAuthorizedTargetAsync(id, UserAdministrationAction.RotateAutologinToken, ct);
        if (target is null) return await TargetFailureAsync(id, UserAdministrationAction.RotateAutologinToken, "thao tác", ct);
        string? token = await repository.RotateAutologinTokenAsync(id, target.Role, ct);
        if (token is not null && Actor()?.Name is { Length: > 0 } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.RotateAutologinToken(actorName, target), ct);
        return token is not null
            ? Ok(new { message = "Xoay mã đăng nhập tự động thành công", logInString = token })
            : UserNotFound();
    }

    [HttpPut("stations")]
    [PermissionAuthorize("customer.assign_station")]
    public async Task<IActionResult> ReplaceStations(ReplaceStationsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Phone) || request.Stations is null)
            return BadRequest(new { message = "Thiếu số điện thoại hoặc danh sách trạm không hợp lệ" });
        string phone = CanonicalPhone(request.Phone);
        UserSummary? target = await repository.FindUserSummaryByPhoneAsync(phone, ct);
        if (target is null) return NotFound(new { message = "Không tìm thấy người dùng với số điện thoại đã cung cấp" });
        IActionResult? denied = AuthorizeTarget(target, UserAdministrationAction.AssignStation, "gán trạm");
        if (denied is not null) return denied;
        IReadOnlyList<string>? stations = await repository.ReplaceStationsByPhoneAsync(phone, target.Role, request.Stations, ct);
        if (stations is not null && Actor()?.Name is { Length: > 0 } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.ReplaceUserStations(
                actorName, target, target.Stations, stations), ct);
        return stations is null ? TargetChanged() : Ok(new { message = "Cập nhật danh sách trạm thành công", stations });
    }

    [HttpPost("{id}/stations")]
    [PermissionAuthorize("customer.assign_station")]
    public async Task<IActionResult> AddStation(string id, AddStationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StationId)) return BadRequest(new { message = "Thiếu stationId" });
        UserSummary? target = await FindAuthorizedTargetAsync(id, UserAdministrationAction.AssignStation, ct);
        if (target is null) return await TargetFailureAsync(id, UserAdministrationAction.AssignStation, "gán trạm", ct);
        UserSummary? user = await repository.AddStationAsync(id, target.Role, request.StationId, ct);
        bool added = user is not null && !target.Stations.Contains(request.StationId, StringComparer.Ordinal);
        if (added && Actor()?.Name is { Length: > 0 } actorName)
            await activityLogs.TryAppendAsync(ActivityLogEntries.AddUserStation(actorName, user!, request.StationId), ct);
        return user is not null ? Ok(new { message = "Đã thêm trạm", user }) : NotFound(new { message = "Không tìm thấy user" });
    }

    private async Task<UserSummary?> FindAuthorizedTargetAsync(string id, UserAdministrationAction action, CancellationToken ct)
    {
        UserSummary? target = await repository.FindUserSummaryAsync(id, ct);
        UserIdentitySnapshot? actor = Actor();
        return target is not null && actor is not null && UserAdministrationPolicy.CanManageTarget(actor, target.Role, action) ? target : null;
    }

    private async Task<IActionResult> TargetFailureAsync(string id, UserAdministrationAction action, string verb, CancellationToken ct)
    {
        UserSummary? target = await repository.FindUserSummaryAsync(id, ct);
        return target is null ? UserNotFound() : AuthorizeTarget(target, action, verb) ?? TargetChanged();
    }

    private ObjectResult? AuthorizeTarget(UserSummary target, UserAdministrationAction action, string verb)
    {
        UserIdentitySnapshot? actor = Actor();
        return actor is not null && UserAdministrationPolicy.CanManageTarget(actor, target.Role, action)
            ? null
            : ForbiddenTarget(actor?.Role, verb);
    }

    private UserIdentitySnapshot? Actor() => HttpContext.Items[LegacyPrincipalMiddleware.IdentityItemKey] as UserIdentitySnapshot;
    private NotFoundObjectResult UserNotFound() => NotFound(new { message = "Không tìm thấy người dùng" });
    private BadRequestObjectResult InvalidPhone() => BadRequest(new { message = "Số điện thoại không hợp lệ. Vui lòng nhập số điện thoại Việt Nam gồm 10-11 chữ số, bắt đầu bằng 0." });
    private BadRequestObjectResult DuplicateSuperAdmin() => BadRequest(new { message = "Hệ thống chỉ được phép có duy nhất 1 tài khoản Super Admin" });
    private ConflictObjectResult SuperAdminMutationInProgress() => Conflict(new { message = "Một thao tác Super Admin khác đang được xử lý, vui lòng thử lại sau." });
    private ConflictObjectResult TargetChanged() => Conflict(new { message = "Tài khoản vừa thay đổi, vui lòng tải lại và thử lại." });
    private ObjectResult ForbiddenCreateRole(string role) => StatusCode(StatusCodes.Status403Forbidden, new { message = role == SystemRoles.Admin ? "Admin chỉ được phép tạo tài khoản Staff hoặc Customer" : "Nhân viên chỉ được tạo tài khoản khách hàng" });
    private ObjectResult ForbiddenAssignRole(string role) => StatusCode(StatusCodes.Status403Forbidden, new { message = role == SystemRoles.Admin ? "Admin không có quyền chỉ định vai trò Admin hoặc Super Admin" : "Bạn không có quyền chỉ định vai trò này" });
    private ObjectResult ForbiddenTarget(string? role, string verb) => StatusCode(StatusCodes.Status403Forbidden, new { message = role == SystemRoles.Admin ? $"Admin không có quyền {verb} tài khoản này" : "Không có quyền thao tác trên tài khoản này." });
    private static bool IsValidPhone(string phone) => System.Text.RegularExpressions.Regex.IsMatch(phone, "^0\\d{9,10}$");
    private static string CanonicalPhone(string value)
    {
        string normalized = new(value.Where(static character => !char.IsWhiteSpace(character) && character is not '.' and not '-' and not '(' and not ')').ToArray());
        if (normalized.StartsWith("+84", StringComparison.Ordinal)) return "0" + normalized[3..];
        if (normalized.StartsWith("84", StringComparison.Ordinal) && normalized.Length is >= 11 and <= 12) return "0" + normalized[2..];
        return normalized;
    }
    private static string? NormalizeEmail(string? email) => email is null ? null : email.Trim().ToLowerInvariant();
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private Task<IAsyncDisposable?> AcquireSuperAdminGuardAsync(
        string role,
        CancellationToken cancellationToken) =>
        role == SystemRoles.SuperAdmin
            ? superAdminGuard.TryAcquireAsync(cancellationToken)
            : Task.FromResult<IAsyncDisposable?>(null);
}
