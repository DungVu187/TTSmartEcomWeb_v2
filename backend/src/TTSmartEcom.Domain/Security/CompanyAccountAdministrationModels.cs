namespace TTSmartEcom.Domain.Security;

public sealed record CompanyRoleDefinition(
    Guid RoleId,
    Guid? CompanyId,
    string RoleCode,
    string Name,
    ControlPlaneScopeType ScopeType,
    bool IsSystemTemplate,
    IReadOnlySet<string> Permissions,
    string? Description = null);

public sealed record ControlPlaneCompanySummary(Guid CompanyId, string CompanyCode, string Name);

public sealed record ControlPlaneUserSummary(
    Guid UserId,
    string DisplayName,
    string? Email,
    string? Phone,
    ControlPlaneAccountType AccountType,
    ControlPlaneUserStatus Status);

public sealed record EffectivePermissionDefinition(
    Guid PermissionId,
    string PermissionCode,
    string Name,
    string ModuleCode,
    string FeatureName,
    string? Description);

public sealed record FeatureAccessSetting(
    Guid FeatureId,
    string FeatureCode,
    string Name,
    string ModuleCode,
    bool CompanyEnabled,
    bool? BranchEnabled);

public sealed record CompanyBranchAccess(
    Guid BranchId,
    string BranchCode,
    string Name,
    byte Status,
    bool IsAssigned,
    IReadOnlyList<CompanyRoleDefinition> Roles);

public sealed record BranchAccountMembership(
    Guid BranchUserId,
    Guid CompanyId,
    Guid BranchId,
    Guid UserId,
    string DisplayName,
    string? Email,
    string? Phone,
    byte Status,
    IReadOnlyList<CompanyRoleDefinition> Roles);

public sealed record CompanyRoleSaveCommand(
    Guid CompanyId,
    Guid? RoleId,
    string Name,
    string Description,
    ControlPlaneScopeType ScopeType,
    IReadOnlyCollection<Guid> PermissionIds,
    Guid ActorUserId,
    Guid CorrelationId);

public sealed record BranchMembershipSaveCommand(
    Guid CompanyId,
    Guid BranchId,
    Guid TargetUserId,
    Guid RoleId,
    Guid ActorUserId,
    bool ActorIsPlatformSuperAdmin,
    ControlPlaneUserType ActorUserType,
    IReadOnlySet<string> ActorCompanyPermissions,
    bool IsPrimary,
    Guid CorrelationId);

public sealed record CompanyAccountMembership(
    Guid CompanyUserId,
    Guid CompanyId,
    Guid UserId,
    string DisplayName,
    string? Email,
    string? Phone,
    ControlPlaneAccountType AccountType,
    ControlPlaneUserType UserType,
    byte Status,
    IReadOnlyList<CompanyRoleDefinition> Roles);

public sealed record CompanyMembershipUpsertCommand(
    Guid CompanyId,
    Guid TargetUserId,
    ControlPlaneUserType UserType,
    Guid RoleId,
    Guid ActorUserId,
    bool ActorIsPlatformSuperAdmin,
    ControlPlaneUserType ActorUserType,
    IReadOnlySet<string> ActorCompanyPermissions,
    Guid CorrelationId);

public sealed record CompanyMembershipRevokeCommand(
    Guid CompanyId,
    Guid TargetUserId,
    Guid ActorUserId,
    bool ActorIsPlatformSuperAdmin,
    ControlPlaneUserType ActorUserType,
    Guid CorrelationId);

public enum CompanyMembershipMutationStatus
{
    Success,
    CompanyNotFound,
    ControlPlaneIdentityNotFound,
    MembershipNotFound,
    TargetIsPlatformIdentity,
    RoleNotFound,
    RoleHasWrongScope,
    RoleBelongsToAnotherCompany,
    MembershipTypeExceedsActor,
    RoleExceedsActorPermissions,
    OwnerRequiresPlatformSuperAdmin,
    OwnerProtected,
    SelfElevation,
    PermissionOutsideEnabledFeature,
    SystemTemplateReadOnly,
    InvalidBranch,
    LastOwner,
    Conflict,
}

public sealed record CompanyMembershipMutationResult(
    CompanyMembershipMutationStatus Status,
    bool Changed,
    CompanyAccountMembership? Membership = null);
