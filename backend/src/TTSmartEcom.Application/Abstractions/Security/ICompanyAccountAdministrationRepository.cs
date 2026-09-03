using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Abstractions.Security;

public interface ICompanyAccountAdministrationRepository
{
    Task<IReadOnlyList<ControlPlaneCompanySummary>> ListCompaniesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ControlPlaneCompanySummary>>([]);

    Task<IReadOnlyList<ControlPlaneUserSummary>> SearchUsersAsync(
        string query,
        bool exact,
        int limit,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ControlPlaneUserSummary>>([]);

    Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EffectivePermissionDefinition>> ListEffectivePermissionsAsync(
        Guid companyId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EffectivePermissionDefinition>>([]);

    Task<IReadOnlyList<FeatureAccessSetting>> ListFeatureSettingsAsync(
        Guid companyId,
        Guid? branchId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<FeatureAccessSetting>>([]);

    Task<bool> SetFeatureAsync(
        Guid companyId,
        Guid? branchId,
        Guid featureId,
        bool enabled,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<IReadOnlyList<CompanyBranchAccess>> ListBranchesForUserAsync(
        Guid companyId,
        Guid userId,
        Guid? activeBranchId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CompanyBranchAccess>>([]);

    Task<IReadOnlyList<BranchAccountMembership>> ListBranchMembershipsAsync(
        Guid companyId,
        Guid branchId,
        CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<BranchAccountMembership>>([]);

    Task<CompanyRoleDefinition> SaveRoleAsync(
        CompanyRoleSaveCommand command,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<bool> SaveBranchMembershipAsync(
        BranchMembershipSaveCommand command,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<bool> RevokeBranchMembershipAsync(
        Guid companyId,
        Guid branchId,
        Guid targetUserId,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken) => throw new NotSupportedException();

    Task<CompanyMembershipMutationResult> UpsertMembershipAsync(
        CompanyMembershipUpsertCommand command,
        CancellationToken cancellationToken);

    Task<CompanyMembershipMutationResult> RevokeMembershipAsync(
        CompanyMembershipRevokeCommand command,
        CancellationToken cancellationToken);

    Task<CompanyMembershipMutationResult> SetMembershipStatusAsync(
        CompanyMembershipRevokeCommand command,
        bool isActive,
        CancellationToken cancellationToken) => throw new NotSupportedException();
}
