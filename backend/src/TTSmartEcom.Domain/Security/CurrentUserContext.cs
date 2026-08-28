namespace TTSmartEcom.Domain.Security;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsControlPlaneIdentity { get; }
    bool IsPlatformSuperAdmin { get; }
    string? DisplayName { get; }
    string? Email { get; }
    string? Phone { get; }
    IReadOnlyList<CompanyMembershipContext> CompanyMemberships { get; }
    Guid? ActiveCompanyId { get; }
    IReadOnlyList<BranchMembershipContext> BranchMemberships { get; }
    Guid? ActiveBranchId { get; }
    IReadOnlyList<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }

    bool HasPermission(string permission);
    bool CanAccessCompany(Guid companyId);
    bool CanAccessBranch(Guid branchId);
    bool HasCompanyPermission(Guid companyId, string permission);
    bool HasBranchPermission(Guid branchId, string permission);
}

public sealed record CompanyMembershipContext(
    Guid CompanyId,
    string CompanyCode,
    string CompanyDisplayName,
    Guid CompanyUserId,
    byte UserType,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions);

public sealed record BranchMembershipContext(
    Guid CompanyId,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid BranchUserId,
    bool IsPrimaryBranch,
    IReadOnlyList<string> Roles,
    IReadOnlySet<string> Permissions);

public sealed class CurrentUserContext : ICurrentUserContext
{
    public static readonly CurrentUserContext Anonymous = new(
        userId: null,
        isAuthenticated: false,
        isPlatformSuperAdmin: false,
        displayName: null,
        email: null,
        phone: null,
        companyMemberships: [],
        activeCompanyId: null,
        branchMemberships: [],
        activeBranchId: null,
        roles: [],
        permissions: new HashSet<string>(StringComparer.Ordinal));

    public Guid? UserId { get; }
    public bool IsAuthenticated { get; }
    public bool IsControlPlaneIdentity { get; }
    public bool IsPlatformSuperAdmin { get; }
    public string? DisplayName { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public IReadOnlyList<CompanyMembershipContext> CompanyMemberships { get; }
    public Guid? ActiveCompanyId { get; }
    public IReadOnlyList<BranchMembershipContext> BranchMemberships { get; }
    public Guid? ActiveBranchId { get; }
    public IReadOnlyList<string> Roles { get; }
    public IReadOnlySet<string> Permissions { get; }

    public CurrentUserContext(
        Guid? userId,
        bool isAuthenticated,
        bool isPlatformSuperAdmin,
        string? displayName,
        string? email,
        string? phone,
        IReadOnlyList<CompanyMembershipContext>? companyMemberships,
        Guid? activeCompanyId,
        IReadOnlyList<BranchMembershipContext>? branchMemberships,
        Guid? activeBranchId,
        IReadOnlyList<string>? roles,
        IReadOnlySet<string>? permissions,
        bool isControlPlaneIdentity = false)
    {
        UserId = userId;
        IsAuthenticated = isAuthenticated;
        IsControlPlaneIdentity = isControlPlaneIdentity;
        IsPlatformSuperAdmin = isPlatformSuperAdmin;
        DisplayName = displayName;
        Email = email;
        Phone = phone;
        CompanyMemberships = companyMemberships ?? [];
        ActiveCompanyId = activeCompanyId ?? (CompanyMemberships.Count == 1 ? CompanyMemberships[0].CompanyId : null);
        BranchMemberships = branchMemberships ?? [];
        if (activeBranchId.HasValue)
        {
            ActiveBranchId = activeBranchId;
        }
        else if (ActiveCompanyId.HasValue)
        {
            ActiveBranchId = BranchMemberships.FirstOrDefault(b => b.CompanyId == ActiveCompanyId.Value && b.IsPrimaryBranch)?.BranchId
                ?? (BranchMemberships.Count(b => b.CompanyId == ActiveCompanyId.Value) == 1 ? BranchMemberships.First(b => b.CompanyId == ActiveCompanyId.Value).BranchId : null);
        }
        else
        {
            ActiveBranchId = BranchMemberships.FirstOrDefault(b => b.IsPrimaryBranch)?.BranchId
                ?? (BranchMemberships.Count == 1 ? BranchMemberships[0].BranchId : null);
        }
        Roles = roles ?? [];
        Permissions = permissions ?? new HashSet<string>(StringComparer.Ordinal);
    }

    public bool HasPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission)) return false;
        if (IsPlatformSuperAdmin) return true;
        return Permissions.Contains(permission);
    }

    public bool CanAccessCompany(Guid companyId)
    {
        if (companyId == Guid.Empty) return false;
        if (IsPlatformSuperAdmin) return true;
        return CompanyMemberships.Any(c => c.CompanyId == companyId);
    }

    public bool CanAccessBranch(Guid branchId)
    {
        if (branchId == Guid.Empty) return false;
        if (IsPlatformSuperAdmin) return true;
        return BranchMemberships.Any(b => b.BranchId == branchId);
    }

    public bool HasCompanyPermission(Guid companyId, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || companyId == Guid.Empty) return false;
        if (IsPlatformSuperAdmin) return true;
        CompanyMembershipContext? membership = CompanyMemberships.FirstOrDefault(c => c.CompanyId == companyId);
        if (membership is null) return false;
        return membership.Permissions.Contains(permission);
    }

    public bool HasBranchPermission(Guid branchId, string permission)
    {
        if (string.IsNullOrWhiteSpace(permission) || branchId == Guid.Empty) return false;
        if (IsPlatformSuperAdmin) return true;
        BranchMembershipContext? membership = BranchMemberships.FirstOrDefault(b => b.BranchId == branchId);
        if (membership is null) return false;
        if (membership.Permissions.Contains(permission)) return true;
        // Branch user may also inherit permissions from their company membership
        CompanyMembershipContext? companyMembership = CompanyMemberships.FirstOrDefault(c => c.CompanyId == membership.CompanyId);
        return companyMembership is not null && companyMembership.Permissions.Contains(permission);
    }
}
