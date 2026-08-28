using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Security;

public sealed class AccessScopeService : IAccessScopeService
{
    public bool CanAccessCompany(ICurrentUserContext context, Guid companyId)
    {
        if (context is null || !context.IsAuthenticated || companyId == Guid.Empty)
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        return context.CanAccessCompany(companyId);
    }

    public bool CanAccessBranch(ICurrentUserContext context, Guid branchId)
    {
        if (context is null || !context.IsAuthenticated || branchId == Guid.Empty)
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        return context.CanAccessBranch(branchId);
    }

    public bool HasPermission(ICurrentUserContext context, string permission)
    {
        if (context is null || !context.IsAuthenticated || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        return context.HasPermission(permission);
    }

    public bool HasActiveScopePermission(ICurrentUserContext context, string permission)
    {
        if (context is null || !context.IsAuthenticated || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        if (context.ActiveBranchId.HasValue)
        {
            return HasBranchPermission(context, context.ActiveBranchId.Value, permission);
        }

        return context.ActiveCompanyId.HasValue
            && HasCompanyPermission(context, context.ActiveCompanyId.Value, permission);
    }

    public bool HasCompanyPermission(ICurrentUserContext context, Guid companyId, string permission)
    {
        if (context is null || !context.IsAuthenticated || companyId == Guid.Empty || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        if (!CanAccessCompany(context, companyId))
        {
            return false;
        }

        return context.HasCompanyPermission(companyId, permission);
    }

    public bool HasBranchPermission(ICurrentUserContext context, Guid branchId, string permission)
    {
        if (context is null || !context.IsAuthenticated || branchId == Guid.Empty || string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        if (!CanAccessBranch(context, branchId))
        {
            return false;
        }

        return context.HasBranchPermission(branchId, permission);
    }

    public bool IsInScope(ICurrentUserContext context, Guid? targetCompanyId, Guid? targetBranchId)
    {
        if (context is null || !context.IsAuthenticated)
        {
            return false;
        }

        if (context.IsPlatformSuperAdmin)
        {
            return true;
        }

        if (targetCompanyId.HasValue && targetCompanyId.Value != Guid.Empty)
        {
            if (!context.CanAccessCompany(targetCompanyId.Value))
            {
                return false;
            }
        }

        if (targetBranchId.HasValue && targetBranchId.Value != Guid.Empty)
        {
            if (!context.CanAccessBranch(targetBranchId.Value))
            {
                return false;
            }

            // Verify the branch belongs to an accessible company if both specified
            if (targetCompanyId.HasValue && targetCompanyId.Value != Guid.Empty)
            {
                BranchMembershipContext? branch = context.BranchMemberships.FirstOrDefault(b => b.BranchId == targetBranchId.Value);
                if (branch is not null && branch.CompanyId != targetCompanyId.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
