using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Security;

public sealed class CompanyAccountAdministrationService(
    ICompanyAccountAdministrationRepository repository,
    IAccessScopeService accessScope)
{
    private const string RequiredPermission = "account.manage";

    public Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
        Guid companyId,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        return repository.ListMembershipsAsync(companyId, cancellationToken);
    }

    public Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
        Guid companyId,
        ICurrentUserContext context,
        CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        return repository.ListCompanyRolesAsync(companyId, cancellationToken);
    }

    public async Task<CompanyAccountMembership> UpsertMembershipAsync(
        Guid companyId,
        string targetUserId,
        byte userType,
        Guid roleId,
        ICurrentUserContext context,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        Guid targetId = RequireControlPlaneUserId(targetUserId);
        if (!Enum.IsDefined(typeof(ControlPlaneUserType), userType))
            throw Error(400, "Loại thành viên Company không hợp lệ.");
        if (roleId == Guid.Empty) throw Error(400, "Role cấp Company không hợp lệ.");

        CompanyMembershipMutationResult result = await repository.UpsertMembershipAsync(
            new CompanyMembershipUpsertCommand(
                companyId,
                targetId,
                (ControlPlaneUserType)userType,
                roleId,
                context.UserId!.Value,
                context.IsPlatformSuperAdmin,
                ActorUserType(companyId, context),
                ActorCompanyPermissions(companyId, context),
                correlationId == Guid.Empty ? Guid.NewGuid() : correlationId),
            cancellationToken);

        return result.Status == CompanyMembershipMutationStatus.Success && result.Membership is not null
            ? result.Membership
            : throw MutationError(result.Status);
    }

    public async Task<bool> RevokeMembershipAsync(
        Guid companyId,
        string targetUserId,
        ICurrentUserContext context,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        Guid targetId = RequireControlPlaneUserId(targetUserId);
        CompanyMembershipMutationResult result = await repository.RevokeMembershipAsync(
            new CompanyMembershipRevokeCommand(
                companyId,
                targetId,
                context.UserId!.Value,
                context.IsPlatformSuperAdmin,
                ActorUserType(companyId, context),
                correlationId == Guid.Empty ? Guid.NewGuid() : correlationId),
            cancellationToken);

        return result.Status == CompanyMembershipMutationStatus.Success
            ? result.Changed
            : throw MutationError(result.Status);
    }

    private void RequireCompanyAdministration(Guid companyId, ICurrentUserContext context)
    {
        if (context is null || !context.IsAuthenticated) throw Error(401, "Yêu cầu xác thực.");
        if (!context.IsControlPlaneIdentity || !context.UserId.HasValue || context.UserId.Value == Guid.Empty)
            throw Error(403, "Tài khoản thao tác chưa phải Control Plane identity.");
        if (companyId == Guid.Empty) throw Error(400, "CompanyId không hợp lệ.");
        if (context.IsPlatformSuperAdmin) return;
        if (!context.ActiveCompanyId.HasValue || context.ActiveCompanyId.Value != companyId)
            throw Error(403, "Không được quản lý tài khoản ngoài Company scope đang hoạt động.");
        if (!accessScope.CanAccessCompany(context, companyId))
            throw Error(403, "Tài khoản không thuộc Company được yêu cầu.");
        if (!accessScope.HasCompanyPermission(context, companyId, RequiredPermission))
            throw Error(403, "Thiếu quyền account.manage tại Company scope.");
    }

    private static Guid RequireControlPlaneUserId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (Guid.TryParse(normalized, out Guid userId) && userId != Guid.Empty) return userId;
        if (normalized.Length == 24 && normalized.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            throw Error(409, "Tài khoản legacy Operational chưa phải Control Plane identity.");
        throw Error(400, "Control Plane userId không hợp lệ.");
    }

    private static IReadOnlySet<string> ActorCompanyPermissions(Guid companyId, ICurrentUserContext context) =>
        context.IsPlatformSuperAdmin
            ? new HashSet<string>(SystemPermissions.All, StringComparer.Ordinal)
            : context.CompanyMemberships.First(membership => membership.CompanyId == companyId).Permissions;

    private static ControlPlaneUserType ActorUserType(Guid companyId, ICurrentUserContext context) =>
        context.IsPlatformSuperAdmin
            ? ControlPlaneUserType.Owner
            : (ControlPlaneUserType)context.CompanyMemberships.First(membership => membership.CompanyId == companyId).UserType;

    private static TTSmartEcom.Application.Common.Errors.ApplicationException MutationError(
        CompanyMembershipMutationStatus status) => status switch
        {
            CompanyMembershipMutationStatus.CompanyNotFound => Error(404, "Không tìm thấy Company đang hoạt động."),
            CompanyMembershipMutationStatus.ControlPlaneIdentityNotFound => Error(404, "Không tìm thấy Control Plane identity."),
            CompanyMembershipMutationStatus.MembershipNotFound => Error(404, "Tài khoản chưa có membership tại Company này."),
            CompanyMembershipMutationStatus.TargetIsPlatformIdentity => Error(403, "Không được sửa Platform SuperAdmin."),
            CompanyMembershipMutationStatus.RoleNotFound => Error(404, "Không tìm thấy role được yêu cầu."),
            CompanyMembershipMutationStatus.RoleHasWrongScope => Error(403, "Không được gán role Branch vào Company membership."),
            CompanyMembershipMutationStatus.RoleBelongsToAnotherCompany => Error(403, "Role không thuộc Company đang quản lý."),
            CompanyMembershipMutationStatus.MembershipTypeExceedsActor => Error(403, "Không được tự nâng cấp loại thành viên hoặc cấp loại thành viên cao hơn người thao tác."),
            CompanyMembershipMutationStatus.RoleExceedsActorPermissions => Error(403, "Không được tự nâng quyền hoặc gán role có quyền vượt quá người thao tác."),
            CompanyMembershipMutationStatus.LastOwner => Error(409, "Không thể thu hồi hoặc hạ cấp chủ sở hữu cuối cùng của Company."),
            _ => Error(409, "Company membership vừa thay đổi; vui lòng tải lại và thử lại."),
        };

    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) =>
        new(new ApplicationError($"TTS-COMPANY-ACCOUNT-{status}", 5600 + status, status, message));
}
