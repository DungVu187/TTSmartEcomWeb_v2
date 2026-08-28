using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.UnitTests.Security;

public sealed class CurrentUserContextTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid BranchA1 = Guid.NewGuid();
    private static readonly Guid BranchA2 = Guid.NewGuid();
    private static readonly Guid BranchB1 = Guid.NewGuid();

    [Fact]
    public void SuperAdmin_HasUniversalAccess()
    {
        CurrentUserContext context = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: true,
            displayName: "Platform SuperAdmin",
            email: "superadmin@ttsmart.com.vn",
            phone: "0900000001",
            companyMemberships: [],
            activeCompanyId: null,
            branchMemberships: [],
            activeBranchId: null,
            roles: [SystemRoles.SuperAdmin],
            permissions: new HashSet<string>(SystemPermissions.All, StringComparer.Ordinal));

        Assert.True(context.IsPlatformSuperAdmin);
        Assert.True(context.CanAccessCompany(CompanyA));
        Assert.True(context.CanAccessCompany(CompanyB));
        Assert.True(context.CanAccessBranch(BranchA1));
        Assert.True(context.CanAccessBranch(BranchB1));
        Assert.True(context.HasPermission("product.view"));
        Assert.True(context.HasPermission("order.edit"));
        Assert.True(context.HasCompanyPermission(CompanyA, "product.view"));
        Assert.True(context.HasBranchPermission(BranchA1, "order.edit"));
    }

    [Fact]
    public void CompanyAdmin_CanAccessOwnCompany_AndIsDeniedOtherCompany()
    {
        CompanyMembershipContext membershipA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Admin,
            Roles: ["company_admin"],
            Permissions: new HashSet<string>(["product.view", "product.create", "employee.manage"], StringComparer.Ordinal));

        CurrentUserContext context = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "Admin ABC",
            email: "admin@abc.com",
            phone: "0900000002",
            companyMemberships: [membershipA],
            activeCompanyId: CompanyA,
            branchMemberships: [],
            activeBranchId: null,
            roles: ["company_admin"],
            permissions: new HashSet<string>(["product.view", "product.create", "employee.manage"], StringComparer.Ordinal));

        Assert.False(context.IsPlatformSuperAdmin);
        Assert.True(context.CanAccessCompany(CompanyA));
        Assert.False(context.CanAccessCompany(CompanyB));
        Assert.True(context.HasCompanyPermission(CompanyA, "product.create"));
        Assert.False(context.HasCompanyPermission(CompanyA, "order.delete"));
        Assert.False(context.HasCompanyPermission(CompanyB, "product.create"));
    }

    [Fact]
    public void BranchEmployee_CanAccessAssignedBranch_AndIsDeniedOtherBranches()
    {
        CompanyMembershipContext companyMem = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Member,
            Roles: ["employee"],
            Permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        BranchMembershipContext branchMemA1 = new(
            CompanyId: CompanyA,
            BranchId: BranchA1,
            BranchCode: "HN",
            BranchName: "Chi nhanh Ha Noi",
            BranchUserId: Guid.NewGuid(),
            IsPrimaryBranch: true,
            Roles: ["branch_staff"],
            Permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        CurrentUserContext context = new(
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

        Assert.True(context.CanAccessCompany(CompanyA));
        Assert.False(context.CanAccessCompany(CompanyB));
        Assert.True(context.CanAccessBranch(BranchA1));
        Assert.False(context.CanAccessBranch(BranchA2));
        Assert.False(context.CanAccessBranch(BranchB1));
        Assert.True(context.HasBranchPermission(BranchA1, "order.create"));
        Assert.False(context.HasBranchPermission(BranchA1, "order.delete"));
        Assert.False(context.HasBranchPermission(BranchA2, "order.create"));
        Assert.False(context.HasBranchPermission(BranchB1, "order.create"));
    }

    [Fact]
    public void AnonymousContext_IsDeniedEverything()
    {
        CurrentUserContext anon = CurrentUserContext.Anonymous;

        Assert.False(anon.IsAuthenticated);
        Assert.False(anon.IsPlatformSuperAdmin);
        Assert.Null(anon.UserId);
        Assert.False(anon.CanAccessCompany(CompanyA));
        Assert.False(anon.CanAccessBranch(BranchA1));
        Assert.False(anon.HasPermission("product.view"));
        Assert.False(anon.HasCompanyPermission(CompanyA, "product.view"));
        Assert.False(anon.HasBranchPermission(BranchA1, "order.view"));
    }
}
