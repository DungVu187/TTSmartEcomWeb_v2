using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Abstractions.Security;

public interface IAccessScopeService
{
    bool CanAccessCompany(ICurrentUserContext context, Guid companyId);

    bool CanAccessBranch(ICurrentUserContext context, Guid branchId);

    bool HasPermission(ICurrentUserContext context, string permission);

    bool HasActiveScopePermission(ICurrentUserContext context, string permission);

    bool HasCompanyPermission(ICurrentUserContext context, Guid companyId, string permission);

    bool HasBranchPermission(ICurrentUserContext context, Guid branchId, string permission);

    bool IsInScope(ICurrentUserContext context, Guid? targetCompanyId, Guid? targetBranchId);
}
