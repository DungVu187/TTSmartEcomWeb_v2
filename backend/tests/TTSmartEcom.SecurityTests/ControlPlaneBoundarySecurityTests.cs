using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.SecurityTests;

public sealed class ControlPlaneBoundarySecurityTests
{
    private readonly AccessScopeService _scopeService = new();
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();
    private static readonly Guid BranchA_HN = Guid.NewGuid();
    private static readonly Guid BranchA_SG = Guid.NewGuid();
    private static readonly Guid BranchB_HN = Guid.NewGuid();

    [Fact]
    public void SEC_SCOPE_001_SuperAdmin_HasPlatformAccessToAllCompaniesAndBranches()
    {
        CurrentUserContext superAdmin = new(
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

        // SuperAdmin can access Company A and Company B
        Assert.True(_scopeService.CanAccessCompany(superAdmin, CompanyA));
        Assert.True(_scopeService.CanAccessCompany(superAdmin, CompanyB));

        // SuperAdmin can access all branches
        Assert.True(_scopeService.CanAccessBranch(superAdmin, BranchA_HN));
        Assert.True(_scopeService.CanAccessBranch(superAdmin, BranchA_SG));
        Assert.True(_scopeService.CanAccessBranch(superAdmin, BranchB_HN));

        // SuperAdmin has all permissions globally
        Assert.True(_scopeService.HasCompanyPermission(superAdmin, CompanyA, "employee.manage"));
        Assert.True(_scopeService.HasCompanyPermission(superAdmin, CompanyB, "employee.manage"));
        Assert.True(_scopeService.HasBranchPermission(superAdmin, BranchA_HN, "order.scan_ai"));
        Assert.True(_scopeService.HasBranchPermission(superAdmin, BranchB_HN, "order.scan_ai"));
    }

    [Fact]
    public void SEC_SCOPE_002_CompanyAdmin_IsolatedToOwnCompany_CannotAccessOtherCompany()
    {
        CompanyMembershipContext membershipA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Admin,
            Roles: ["company_admin"],
            Permissions: new HashSet<string>(["product.view", "product.create", "employee.manage"], StringComparer.Ordinal));

        CurrentUserContext adminA = new(
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

        // Admin ABC -> access ABC: ALLOW
        Assert.True(_scopeService.CanAccessCompany(adminA, CompanyA));
        Assert.True(_scopeService.HasCompanyPermission(adminA, CompanyA, "employee.manage"));

        // Admin ABC -> access XYZ (Company B): STRICTLY DENIED (403)
        Assert.False(_scopeService.CanAccessCompany(adminA, CompanyB));
        Assert.False(_scopeService.HasCompanyPermission(adminA, CompanyB, "employee.manage"));
        Assert.False(_scopeService.IsInScope(adminA, CompanyB, null));
    }

    [Fact]
    public void SEC_SCOPE_003_BranchEmployee_IsolatedToAssignedBranch_CannotCrossBranches()
    {
        CompanyMembershipContext compA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Member,
            Roles: ["employee"],
            Permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        BranchMembershipContext branchHN = new(
            CompanyId: CompanyA,
            BranchId: BranchA_HN,
            BranchCode: "HN",
            BranchName: "Chi nhanh Ha Noi",
            BranchUserId: Guid.NewGuid(),
            IsPrimaryBranch: true,
            Roles: ["branch_staff"],
            Permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        CurrentUserContext employeeHN = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "Employee ABC-HN",
            email: "hn@abc.com",
            phone: "0900000003",
            companyMemberships: [compA],
            activeCompanyId: CompanyA,
            branchMemberships: [branchHN],
            activeBranchId: BranchA_HN,
            roles: ["employee", "branch_staff"],
            permissions: new HashSet<string>(["order.view", "order.create"], StringComparer.Ordinal));

        // Employee ABC-HN -> access ABC-HN: ALLOW
        Assert.True(_scopeService.CanAccessBranch(employeeHN, BranchA_HN));
        Assert.True(_scopeService.HasBranchPermission(employeeHN, BranchA_HN, "order.create"));
        Assert.True(_scopeService.IsInScope(employeeHN, CompanyA, BranchA_HN));

        // Employee ABC-HN -> access ABC-SG (same company, unassigned branch): STRICTLY DENIED
        Assert.False(_scopeService.CanAccessBranch(employeeHN, BranchA_SG));
        Assert.False(_scopeService.HasBranchPermission(employeeHN, BranchA_SG, "order.create"));
        Assert.False(_scopeService.IsInScope(employeeHN, CompanyA, BranchA_SG));

        // Employee ABC-HN -> access XYZ-HN (foreign company branch): STRICTLY DENIED
        Assert.False(_scopeService.CanAccessBranch(employeeHN, BranchB_HN));
        Assert.False(_scopeService.HasBranchPermission(employeeHN, BranchB_HN, "order.create"));
        Assert.False(_scopeService.IsInScope(employeeHN, CompanyB, BranchB_HN));
    }

    [Fact]
    public void SEC_SCOPE_004_PermissionEnforcement_LackingPermissionInScope_Denied()
    {
        CompanyMembershipContext compA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Member,
            Roles: ["employee"],
            Permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        BranchMembershipContext branchHN = new(
            CompanyId: CompanyA,
            BranchId: BranchA_HN,
            BranchCode: "HN",
            BranchName: "Chi nhanh Ha Noi",
            BranchUserId: Guid.NewGuid(),
            IsPrimaryBranch: true,
            Roles: ["branch_staff"],
            Permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        CurrentUserContext viewerHN = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "Viewer ABC-HN",
            email: "viewer@abc.com",
            phone: "0900000004",
            companyMemberships: [compA],
            activeCompanyId: CompanyA,
            branchMemberships: [branchHN],
            activeBranchId: BranchA_HN,
            roles: ["employee", "branch_staff"],
            permissions: new HashSet<string>(["order.view"], StringComparer.Ordinal));

        // User has order.view -> ALLOW
        Assert.True(_scopeService.HasBranchPermission(viewerHN, BranchA_HN, "order.view"));

        // User lacks order.delete -> DENIED
        Assert.False(_scopeService.HasBranchPermission(viewerHN, BranchA_HN, "order.delete"));
        Assert.False(_scopeService.HasCompanyPermission(viewerHN, CompanyA, "order.delete"));
    }

    [Fact]
    public void SEC_SCOPE_005_TamperingWithForeignScope_DetectedAndBlocked()
    {
        CompanyMembershipContext compA = new(
            CompanyId: CompanyA,
            CompanyCode: "ABC",
            CompanyDisplayName: "Cong ty ABC",
            CompanyUserId: Guid.NewGuid(),
            UserType: (byte)ControlPlaneUserType.Admin,
            Roles: ["company_admin"],
            Permissions: new HashSet<string>(["product.view", "order.create"], StringComparer.Ordinal));

        CurrentUserContext user = new(
            userId: Guid.NewGuid(),
            isAuthenticated: true,
            isPlatformSuperAdmin: false,
            displayName: "User",
            email: "user@abc.com",
            phone: "0900000005",
            companyMemberships: [compA],
            activeCompanyId: CompanyA,
            branchMemberships: [],
            activeBranchId: null,
            roles: ["company_admin"],
            permissions: new HashSet<string>(["product.view", "order.create"], StringComparer.Ordinal));

        // Client injects forged CompanyId B into request body or URL
        Assert.False(_scopeService.IsInScope(user, CompanyB, null));
        Assert.False(_scopeService.CanAccessCompany(user, CompanyB));

        // Client injects forged BranchId B_HN into request body
        Assert.False(_scopeService.IsInScope(user, null, BranchB_HN));
        Assert.False(_scopeService.CanAccessBranch(user, BranchB_HN));
    }
}
