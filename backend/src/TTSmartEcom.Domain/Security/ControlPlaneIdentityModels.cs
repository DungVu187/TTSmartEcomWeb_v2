namespace TTSmartEcom.Domain.Security;

public enum ControlPlaneUserStatus : byte
{
    Inactive = 0,
    Active = 1,
    Suspended = 2,
    Locked = 3,
}

public enum ControlPlaneAccountType : byte
{
    Platform = 1,
    Company = 2,
    Branch = 3,
}

public enum ControlPlaneIdentifierType : byte
{
    Phone = 1,
    Email = 2,
    Username = 3,
}

public enum ControlPlaneUserType : byte
{
    Owner = 1,
    Admin = 2,
    Member = 3,
}

public enum ControlPlaneScopeType : byte
{
    Company = 1,
    Branch = 2,
}

public sealed record ControlPlaneUserRecord(
    Guid UserId,
    string DisplayName,
    ControlPlaneAccountType AccountType,
    ControlPlaneUserStatus Status,
    Guid SecurityStamp,
    string? PrimaryEmail,
    string? PrimaryPhone,
    string PasswordHash,
    string HashAlgorithm,
    int HashVersion,
    bool MustChangePassword,
    int FailedAttemptCount,
    DateTimeOffset? LockedUntilUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record ControlPlaneCompanyMembership(
    Guid CompanyUserId,
    Guid CompanyId,
    string CompanyCode,
    string DisplayName,
    ControlPlaneUserType UserType,
    byte Status,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    IReadOnlyList<ControlPlaneRoleAssignment> Roles);

public sealed record ControlPlaneBranchMembership(
    Guid BranchUserId,
    Guid CompanyId,
    Guid BranchId,
    string BranchCode,
    string BranchName,
    Guid CompanyUserId,
    bool IsPrimaryBranch,
    byte Status,
    DateTimeOffset? StartsAtUtc,
    DateTimeOffset? EndsAtUtc,
    IReadOnlyList<ControlPlaneRoleAssignment> Roles);

public sealed record ControlPlaneRoleAssignment(
    Guid UserRoleId,
    Guid RoleId,
    string RoleCode,
    string RoleName,
    byte ScopeType, // 1 = Company, 2 = Branch
    Guid? CompanyUserId,
    Guid? BranchUserId,
    IReadOnlyList<string> Permissions);
