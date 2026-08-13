using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Users;

public enum UserAdministrationAction
{
    Edit,
    Delete,
    AssignStation,
    RotateAutologinToken,
}

public sealed record PermissionValidationResult(bool IsValid, IReadOnlyList<string> Permissions, string? Message)
{
    public static PermissionValidationResult Failure(string message) => new(false, [], message);
    public static PermissionValidationResult Success(IReadOnlyList<string> permissions) => new(true, permissions, null);
}

public static class UserAdministrationPolicy
{
    public static IReadOnlySet<string> GrantablePermissions { get; } = new HashSet<string>(SystemPermissions.All
        .Except(["account.manage", "zalo.manage"], StringComparer.Ordinal), StringComparer.Ordinal);

    private static Dictionary<string, string> Dependencies { get; } = new(StringComparer.Ordinal)
    {
        ["order.excel"] = "order.edit",
        ["order.scan_ai"] = "order.edit",
        ["iporder.excel"] = "iporder.edit",
        ["iporder.scan_ai"] = "iporder.edit",
        ["eporder.excel"] = "eporder.edit",
        ["eporder.scan_ai"] = "eporder.edit",
    };

    public static bool IsAdministrativeActor(UserIdentitySnapshot actor) =>
        actor.Role is SystemRoles.SuperAdmin or SystemRoles.Admin;

    public static bool CanCreateRole(UserIdentitySnapshot actor, string role) => actor.Role switch
    {
        SystemRoles.SuperAdmin => SystemRoles.All.Contains(role),
        SystemRoles.Admin => role is SystemRoles.Staff or SystemRoles.Customer,
        SystemRoles.Staff => role == SystemRoles.Customer && actor.Permissions.Contains("customer.create", StringComparer.Ordinal),
        _ => false,
    };

    public static bool CanManageTarget(UserIdentitySnapshot actor, string targetRole, UserAdministrationAction action) => actor.Role switch
    {
        SystemRoles.SuperAdmin => true,
        SystemRoles.Admin when action == UserAdministrationAction.Delete => targetRole == SystemRoles.Customer,
        SystemRoles.Admin => targetRole is SystemRoles.Staff or SystemRoles.Customer,
        SystemRoles.Staff when action == UserAdministrationAction.RotateAutologinToken => targetRole == SystemRoles.Customer,
        _ => false,
    };

    public static PermissionValidationResult ValidateGrantablePermissions(IReadOnlyList<string>? permissions)
    {
        if (permissions is null) return PermissionValidationResult.Failure("Danh sách quyền phải là một mảng");

        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string? rawPermission in permissions)
        {
            string permission = rawPermission?.Trim() ?? string.Empty;
            if (permission.Length == 0)
                return PermissionValidationResult.Failure("Quyền không hợp lệ hoặc không được phép cấp: giá trị rỗng");
            if (!GrantablePermissions.Contains(permission))
                return PermissionValidationResult.Failure($"Quyền không hợp lệ hoặc không được phép cấp: {permission}");
            if (seen.Add(permission)) normalized.Add(permission);
        }

        foreach (string permission in normalized)
        {
            if (Dependencies.TryGetValue(permission, out string? dependency) && !seen.Contains(dependency))
                return PermissionValidationResult.Failure($"Quyền {permission} yêu cầu quyền {dependency}");
        }

        return PermissionValidationResult.Success(normalized);
    }

    public static PermissionValidationResult ResolvePermissions(
        string currentRole,
        IReadOnlyList<string> currentPermissions,
        string? requestedRole,
        IReadOnlyList<string>? requestedPermissions)
    {
        string finalRole = requestedRole ?? currentRole;
        if (!SystemRoles.All.Contains(finalRole)) return PermissionValidationResult.Failure("Vai trò không hợp lệ");
        if (finalRole is not (SystemRoles.Admin or SystemRoles.Staff)) return PermissionValidationResult.Success([]);
        if (requestedPermissions is not null) return ValidateGrantablePermissions(requestedPermissions);
        return requestedRole is null
            ? PermissionValidationResult.Success(currentPermissions)
            : PermissionValidationResult.Success([]);
    }
}
