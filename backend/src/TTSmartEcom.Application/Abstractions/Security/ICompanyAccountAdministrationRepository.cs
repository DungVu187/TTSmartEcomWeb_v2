using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Abstractions.Security;

public interface ICompanyAccountAdministrationRepository
{
    Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
        Guid companyId,
        CancellationToken cancellationToken);

    Task<CompanyMembershipMutationResult> UpsertMembershipAsync(
        CompanyMembershipUpsertCommand command,
        CancellationToken cancellationToken);

    Task<CompanyMembershipMutationResult> RevokeMembershipAsync(
        CompanyMembershipRevokeCommand command,
        CancellationToken cancellationToken);
}
