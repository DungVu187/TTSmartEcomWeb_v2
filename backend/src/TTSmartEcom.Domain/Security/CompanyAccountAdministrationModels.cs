namespace TTSmartEcom.Domain.Security;

public sealed record CompanyRoleDefinition(
    Guid RoleId,
    Guid? CompanyId,
    string RoleCode,
    string Name,
    ControlPlaneScopeType ScopeType,
    bool IsSystemTemplate,
    IReadOnlySet<string> Permissions);

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
    LastOwner,
    Conflict,
}

public sealed record CompanyMembershipMutationResult(
    CompanyMembershipMutationStatus Status,
    bool Changed,
    CompanyAccountMembership? Membership = null);
