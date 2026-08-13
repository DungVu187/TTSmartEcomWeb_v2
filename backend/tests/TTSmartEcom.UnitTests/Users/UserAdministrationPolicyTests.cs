using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Users;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.UnitTests.Users;

public sealed class UserAdministrationPolicyTests
{
    [Fact]
    public void ValidateGrantablePermissions_NormalizesAndDeduplicates()
    {
        PermissionValidationResult result = UserAdministrationPolicy.ValidateGrantablePermissions(
            [" order.edit ", "order.excel", "order.edit", "account.manage"]);

        Assert.False(result.IsValid);
        Assert.Contains("account.manage", result.Message ?? string.Empty, StringComparison.Ordinal);

        result = UserAdministrationPolicy.ValidateGrantablePermissions([" order.edit ", "order.excel", "order.edit", "activitylog.view"]);
        Assert.True(result.IsValid);
        Assert.Equal(["order.edit", "order.excel", "activitylog.view"], result.Permissions);
    }

    [Theory]
    [InlineData("order.excel", "order.edit")]
    [InlineData("iporder.scan_ai", "iporder.edit")]
    [InlineData("eporder.excel", "eporder.edit")]
    public void ValidateGrantablePermissions_RequiresDependencies(string permission, string dependency)
    {
        PermissionValidationResult result = UserAdministrationPolicy.ValidateGrantablePermissions([permission]);

        Assert.False(result.IsValid);
        Assert.Contains(dependency, result.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePermissions_ClearsPrivilegeWhenRoleDoesNotAcceptPermissions()
    {
        PermissionValidationResult customer = UserAdministrationPolicy.ResolvePermissions(
            SystemRoles.Staff, ["product.edit"], SystemRoles.Customer, null);
        PermissionValidationResult unchangedStaff = UserAdministrationPolicy.ResolvePermissions(
            SystemRoles.Staff, ["product.edit"], null, null);
        PermissionValidationResult promotedStaff = UserAdministrationPolicy.ResolvePermissions(
            SystemRoles.Customer, [], SystemRoles.Staff, null);

        Assert.True(customer.IsValid);
        Assert.Empty(customer.Permissions);
        Assert.Equal(["product.edit"], unchangedStaff.Permissions);
        Assert.Empty(promotedStaff.Permissions);
    }

    [Fact]
    public void TargetAuthorization_EnforcesLegacyRoleHierarchy()
    {
        UserIdentitySnapshot superAdmin = Actor(SystemRoles.SuperAdmin);
        UserIdentitySnapshot admin = Actor(SystemRoles.Admin);
        UserIdentitySnapshot staff = Actor(SystemRoles.Staff, ["customer.edit", "customer.delete", "customer.assign_station"]);

        Assert.True(UserAdministrationPolicy.CanManageTarget(superAdmin, SystemRoles.SuperAdmin, UserAdministrationAction.Delete));
        Assert.True(UserAdministrationPolicy.CanManageTarget(admin, SystemRoles.Staff, UserAdministrationAction.Edit));
        Assert.False(UserAdministrationPolicy.CanManageTarget(admin, SystemRoles.Staff, UserAdministrationAction.Delete));
        Assert.True(UserAdministrationPolicy.CanManageTarget(admin, SystemRoles.Customer, UserAdministrationAction.Delete));
        Assert.False(UserAdministrationPolicy.CanManageTarget(admin, SystemRoles.Admin, UserAdministrationAction.AssignStation));
        Assert.False(UserAdministrationPolicy.CanManageTarget(staff, SystemRoles.Customer, UserAdministrationAction.Edit));
        Assert.True(UserAdministrationPolicy.CanManageTarget(staff, SystemRoles.Customer, UserAdministrationAction.RotateAutologinToken));
        Assert.False(UserAdministrationPolicy.CanManageTarget(staff, SystemRoles.Customer, UserAdministrationAction.Delete));
        Assert.False(UserAdministrationPolicy.CanManageTarget(staff, SystemRoles.Customer, UserAdministrationAction.AssignStation));
    }

    [Fact]
    public void RoleCreation_RequiresActorScopeAndCustomerCreateForStaff()
    {
        Assert.True(UserAdministrationPolicy.CanCreateRole(Actor(SystemRoles.SuperAdmin), SystemRoles.Admin));
        Assert.False(UserAdministrationPolicy.CanCreateRole(Actor(SystemRoles.Admin), SystemRoles.Admin));
        Assert.True(UserAdministrationPolicy.CanCreateRole(Actor(SystemRoles.Admin), SystemRoles.Staff));
        Assert.False(UserAdministrationPolicy.CanCreateRole(Actor(SystemRoles.Staff), SystemRoles.Customer));
        Assert.True(UserAdministrationPolicy.CanCreateRole(Actor(SystemRoles.Staff, ["customer.create"]), SystemRoles.Customer));
    }

    private static UserIdentitySnapshot Actor(string role, IReadOnlyCollection<string>? permissions = null) => new(
        "507f1f77bcf86cd799439011", null, "0900000000", "Synthetic actor", role, [], permissions ?? [], null);
}
