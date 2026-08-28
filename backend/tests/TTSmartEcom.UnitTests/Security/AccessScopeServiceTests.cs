using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.UnitTests.Security;

public sealed class AccessScopeServiceTests
{
    private readonly AccessScopeService _service = new();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid BranchA1 = Guid.NewGuid();
    private static readonly Guid BranchA2 = Guid.NewGuid();
    private static readonly Guid BranchB1 = Guid.NewGuid();

    [Fact]
    public void SuperAdmin_IsInScope_ForAnyCompanyAndBranch()
    {
        CurrentUserContext superAdmin = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: true,
            displayName: "SuperAdmin",
            email: "admin@ttsmart.com.vn",
            phone: "0900000001",
            companyMemberships: [],
            activeCompanyId: null,
            branchMemberships: [],
            activeBranchId: null,
            roles: [SystemRoles.SuperAdmin],
            permissions: new HashSet<string>(SystemPermissions.All, StringComparer.Ordinal));

        Assert.True(_service.CanAccessCompany(superAdmin, CompanyA));
        Assert.True(_service.CanAccessCompany(superAdmin, CompanyB));
        Assert.True(_service.CanAccessBranch(superAdmin, BranchA1));
        Assert.True(_service.CanAccessBranch(superAdmin, BranchB1));
        Assert.True(_service.HasPermission(superAdmin, "any.permission"));
        Assert.True(_service.HasCompanyPermission(superAdmin, CompanyA, "any.permission"));
        Assert.True(_service.HasBranchPermission(superAdmin, BranchB1, "any.permission"));
        Assert.True(_service.IsInScope(superAdmin, CompanyA, BranchA1));
        Assert.True(_service.IsInScope(superAdmin, CompanyB, BranchB1));
    }

    [Fact]
    public void CompanyAdmin_IsInScope_OnlyForAssignedCompany()
    {
        CompanyMembershipContext membershipA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Admin,
            Roles: ["company_admin"],
            Permissions: new HashSet<string>(["product.view", "product.edit"], StringComparer.Ordinal));

        CurrentUserContext companyAdmin = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "Company Admin",
            email: "admin@abc.com",
            phone: "0900000002",
            companyMemberships: [membershipA],
            activeCompanyId: CompanyA,
            branchMemberships: [],
            activeBranchId: null,
            roles: ["company_admin"],
            permissions: new HashSet<string>(["product.view", "product.edit"], StringComparer.Ordinal));

        // Company A: allowed
        Assert.True(_service.CanAccessCompany(companyAdmin, CompanyA));
        Assert.True(_service.HasCompanyPermission(companyAdmin, CompanyA, "product.edit"));
        Assert.False(_service.HasCompanyPermission(companyAdmin, CompanyA, "order.delete"));
        Assert.True(_service.IsInScope(companyAdmin, CompanyA, null));

        // Company B: denied
        Assert.False(_service.CanAccessCompany(companyAdmin, CompanyB));
        Assert.False(_service.HasCompanyPermission(companyAdmin, CompanyB, "product.edit"));
        Assert.False(_service.IsInScope(companyAdmin, CompanyB, null));
    }

    [Fact]
    public void BranchEmployee_IsInScope_OnlyForAssignedBranch()
    {
        CompanyMembershipContext companyMem = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Member,
            Roles: ["employee"],
            Permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        BranchMembershipContext branchMemA1 = new(
            CompanyId: CompanyA,
            BranchId: BranchA1,
            BranchCode: "HN",
            BranchName: "Chi nhanh HN",
            BranchUserId: Guid.NewGuid(),
            IsPrimaryBranch: true,
            Roles: ["branch_staff"],
            Permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        CurrentUserContext employee = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "Employee HN",
            email: "hn@abc.com",
            phone: "0900000003",
            companyMemberships: [companyMem],
            activeCompanyId: CompanyA,
            branchMemberships: [branchMemA1],
            activeBranchId: BranchA1,
            roles: ["employee", "branch_staff"],
            permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        // Own Branch A1: allowed
        Assert.True(_service.CanAccessBranch(employee, BranchA1));
        Assert.True(_service.HasBranchPermission(employee, BranchA1, "order.create"));
        Assert.True(_service.IsInScope(employee, CompanyA, BranchA1));

        // Other branch in same company A2: denied
        Assert.False(_service.CanAccessBranch(employee, BranchA2));
        Assert.False(_service.HasBranchPermission(employee, BranchA2, "order.create"));
        Assert.False(_service.IsInScope(employee, CompanyA, BranchA2));

        // Branch in other company B1: denied
        Assert.False(_service.CanAccessBranch(employee, BranchB1));
        Assert.False(_service.HasBranchPermission(employee, BranchB1, "order.create"));
        Assert.False(_service.IsInScope(employee, CompanyB, BranchB1));
    }

    [Fact]
    public void BranchMismatchWithCompany_IsInScope_ReturnsFalse()
    {
        CompanyMembershipContext companyMem = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Member,
            Roles: ["employee"],
            Permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        BranchMembershipContext branchMemA1 = new(
            CompanyId: CompanyA,
            BranchId: BranchA1,
            BranchCode: "HN",
            BranchName: "Chi nhanh HN",
            BranchUserId: Guid.NewGuid(),
            IsPrimaryBranch: true,
            Roles: ["branch_staff"],
            Permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        CurrentUserContext user = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "User",
            email: "u@abc.com",
            phone: "0900000004",
            companyMemberships: [companyMem],
            activeCompanyId: CompanyA,
            branchMemberships: [branchMemA1],
            activeBranchId: BranchA1,
            roles: ["employee"],
            permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        // User belongs to CompanyA and BranchA1, but caller forged targetCompanyId = CompanyB with BranchA1
        Assert.False(_service.IsInScope(user, CompanyB, BranchA1));
    }

    [Fact]
    public void UnauthenticatedContext_IsInScope_ReturnsFalse()
    {
        ICurrentUserContext anon = CurrentUserContext.Anonymous;

        Assert.False(_service.CanAccessCompany(anon, CompanyA));
        Assert.False(_service.CanAccessBranch(anon, BranchA1));
        Assert.False(_service.HasPermission(anon, "product.view"));
        Assert.False(_service.IsInScope(anon, CompanyA, BranchA1));
    }

    [Fact]
    public void ActiveScopePermission_DoesNotLeakFromAnotherCompanyMembership()
    {
        CompanyMembershipContext companyA = new(
            CompanyA, "ABC", "Cong ty ABC", Guid.NewGuid(), (byte)ControlPlaneUserType.Admin,
            ["company_admin"], new HashSet<string>(["product.edit"], StringComparer.Ordinal));
        CompanyMembershipContext companyB = new(
            CompanyB, "XYZ", "Cong ty XYZ", Guid.NewGuid(), (byte)ControlPlaneUserType.Member,
            ["company_member"], new HashSet<string>(StringComparer.Ordinal));

        CurrentUserContext user = new(
            Guid.NewGuid(), true, false, "Multi-company user", null, null,
            [companyA, companyB], CompanyB, [], null,
            ["company_admin", "company_member"],
            new HashSet<string>(["product.edit"], StringComparer.Ordinal),
            isControlPlaneIdentity: true);

        Assert.True(_service.HasPermission(user, "product.edit"));
        Assert.False(_service.HasActiveScopePermission(user, "product.edit"));
        Assert.True(_service.HasCompanyPermission(user, CompanyA, "product.edit"));
        Assert.False(_service.HasCompanyPermission(user, CompanyB, "product.edit"));
    }
}
