using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Common.Errors;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Application.Security;

public sealed class CompanyAccountAdministrationService(
    ICompanyAccountAdministrationRepository repository,
    IAccessScopeService accessScope)
{
    private const string RequiredPermission = "account.manage";

    public Task<IReadOnlyList<ControlPlaneCompanySummary>> ListCompaniesAsync(
        ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequirePlatformSuperAdmin(context);
        return repository.ListCompaniesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<ControlPlaneUserSummary>> SearchUsersAsync(
        string? query, bool exact, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireControlPlaneIdentity(context);
        string value = query?.Trim() ?? string.Empty;
        if (value.Length < (exact ? 3 : 2) || value.Length > 200)
            throw Error(400, exact
                ? "Vui lòng nhập đầy đủ số điện thoại hoặc email."
                : "Từ khóa tìm kiếm phải có từ 2 đến 200 ký tự.");
        if (!exact) RequirePlatformSuperAdmin(context);
        else if (!context.IsPlatformSuperAdmin &&
                 (!context.ActiveCompanyId.HasValue || context.ActiveBranchId.HasValue ||
                  !accessScope.HasCompanyPermission(context, context.ActiveCompanyId.Value, RequiredPermission)))
            throw Error(403, "Bạn không có quyền tìm và thêm người dùng cho công ty này.");
        return repository.SearchUsersAsync(value, exact, exact ? 1 : 20, cancellationToken);
    }

    public Task<IReadOnlyList<CompanyBranchAccess>> ListPlatformBranchesAsync(
        Guid companyId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequirePlatformSuperAdmin(context);
        if (companyId == Guid.Empty) throw Error(400, "Công ty không hợp lệ.");
        return repository.ListBranchesForUserAsync(companyId, Guid.Empty, null, cancellationToken);
    }

    public Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
        Guid companyId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        return repository.ListMembershipsAsync(companyId, cancellationToken);
    }

    public Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
        Guid companyId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        return repository.ListCompanyRolesAsync(companyId, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyRoleDefinition>> ListBranchRolesAsync(
        Guid companyId, Guid branchId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireBranchOrCompanyAdministration(companyId, branchId, context);
        IReadOnlyList<CompanyRoleDefinition> roles = await repository.ListCompanyRolesAsync(companyId, cancellationToken);
        return roles.Where(role => role.ScopeType == ControlPlaneScopeType.Branch).ToArray();
    }

    public Task<IReadOnlyList<EffectivePermissionDefinition>> ListEffectivePermissionsAsync(
        Guid companyId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        return repository.ListEffectivePermissionsAsync(companyId, cancellationToken);
    }

    public Task<IReadOnlyList<FeatureAccessSetting>> ListFeatureSettingsAsync(
        Guid companyId, Guid? branchId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequirePlatformSuperAdmin(context);
        if (companyId == Guid.Empty || branchId == Guid.Empty) throw Error(400, "Công ty hoặc chi nhánh không hợp lệ.");
        return repository.ListFeatureSettingsAsync(companyId, branchId, cancellationToken);
    }

    public Task<bool> SetFeatureAsync(
        Guid companyId, Guid? branchId, Guid featureId, bool enabled,
        ICurrentUserContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        RequirePlatformSuperAdmin(context);
        if (companyId == Guid.Empty || featureId == Guid.Empty || branchId == Guid.Empty)
            throw Error(400, "Công ty, chi nhánh hoặc chức năng không hợp lệ.");
        return repository.SetFeatureAsync(companyId, branchId, featureId, enabled, context.UserId!.Value,
            correlationId == Guid.Empty ? Guid.NewGuid() : correlationId, cancellationToken);
    }

    public Task<IReadOnlyList<CompanyBranchAccess>> ListBranchesForUserAsync(
        Guid companyId, string targetUserId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        if (context.ActiveBranchId is Guid branchId) RequireBranchOrCompanyAdministration(companyId, branchId, context);
        else RequireCompanyAdministration(companyId, context);
        return repository.ListBranchesForUserAsync(
            companyId, RequireControlPlaneUserId(targetUserId), context.ActiveBranchId, cancellationToken);
    }

    public Task<IReadOnlyList<BranchAccountMembership>> ListBranchMembershipsAsync(
        Guid companyId, Guid branchId, ICurrentUserContext context, CancellationToken cancellationToken)
    {
        RequireControlPlaneIdentity(context);
        if (!context.ActiveCompanyId.HasValue || context.ActiveCompanyId != companyId ||
            !context.ActiveBranchId.HasValue || context.ActiveBranchId != branchId ||
            !accessScope.CanAccessBranch(context, branchId) ||
            !accessScope.HasBranchPermission(context, branchId, RequiredPermission))
            throw Error(403, "Bạn không có quyền quản lý người dùng tại chi nhánh này.");
        return repository.ListBranchMembershipsAsync(companyId, branchId, cancellationToken);
    }

    public async Task<CompanyRoleDefinition> SaveRoleAsync(
        Guid companyId, Guid? roleId, string? name, string? description, byte scopeType,
        IReadOnlyCollection<Guid>? permissionIds, ICurrentUserContext context,
        Guid correlationId, CancellationToken cancellationToken)
    {
        RequireCompanyOwner(companyId, context);
        string normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 2 or > 200) throw Error(400, "Tên vai trò phải có từ 2 đến 200 ký tự.");
        string normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length > 1000) throw Error(400, "Mô tả vai trò không được vượt quá 1000 ký tự.");
        if (!Enum.IsDefined(typeof(ControlPlaneScopeType), scopeType)) throw Error(400, "Phạm vi vai trò không hợp lệ.");
        Guid[] permissions = permissionIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? [];
        if (permissions.Length == 0 || permissions.Length > 200) throw Error(400, "Vui lòng chọn quyền hợp lệ cho vai trò.");
        try
        {
            return await repository.SaveRoleAsync(new CompanyRoleSaveCommand(
                companyId, roleId, normalizedName, normalizedDescription, (ControlPlaneScopeType)scopeType,
                permissions, context.UserId!.Value, correlationId == Guid.Empty ? Guid.NewGuid() : correlationId), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw PersistenceError(exception);
        }
    }

    public async Task<bool> SaveBranchMembershipAsync(
        Guid companyId, Guid branchId, string targetUserId, Guid roleId, bool isPrimary,
        ICurrentUserContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        RequireBranchOrCompanyAdministration(companyId, branchId, context);
        if (branchId == Guid.Empty || roleId == Guid.Empty) throw Error(400, "Chi nhánh hoặc vai trò không hợp lệ.");
        try
        {
            return await repository.SaveBranchMembershipAsync(new BranchMembershipSaveCommand(
                companyId, branchId, RequireControlPlaneUserId(targetUserId), roleId,
                context.UserId!.Value, context.IsPlatformSuperAdmin, ActorUserType(companyId, context),
                ActorAssignablePermissions(companyId, branchId, context), isPrimary,
                correlationId == Guid.Empty ? Guid.NewGuid() : correlationId), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw PersistenceError(exception);
        }
    }

    public Task<bool> RevokeBranchMembershipAsync(
        Guid companyId, Guid branchId, string targetUserId, ICurrentUserContext context,
        Guid correlationId, CancellationToken cancellationToken)
    {
        RequireBranchOrCompanyAdministration(companyId, branchId, context);
        if (branchId == Guid.Empty) throw Error(400, "Chi nhánh không hợp lệ.");
        return repository.RevokeBranchMembershipAsync(
            companyId, branchId, RequireControlPlaneUserId(targetUserId), context.UserId!.Value,
            correlationId == Guid.Empty ? Guid.NewGuid() : correlationId, cancellationToken);
    }

    public async Task<CompanyAccountMembership> UpsertMembershipAsync(
        Guid companyId, string targetUserId, byte userType, Guid roleId,
        ICurrentUserContext context, Guid correlationId, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        Guid targetId = RequireControlPlaneUserId(targetUserId);
        if (!Enum.IsDefined(typeof(ControlPlaneUserType), userType)) throw Error(400, "Loại tài khoản không hợp lệ.");
        if (roleId == Guid.Empty) throw Error(400, "Vai trò công ty không hợp lệ.");

        ControlPlaneUserType requestedType = (ControlPlaneUserType)userType;
        if (requestedType == ControlPlaneUserType.Owner && !context.IsPlatformSuperAdmin)
            throw Error(403, "Chỉ Quản trị nền tảng được chỉ định chủ sở hữu công ty.");
        if (!context.IsPlatformSuperAdmin && context.UserId == targetId && requestedType < ActorUserType(companyId, context))
            throw Error(403, "Bạn không thể tự nâng loại tài khoản của mình.");

        CompanyMembershipMutationResult result = await repository.UpsertMembershipAsync(
            new CompanyMembershipUpsertCommand(
                companyId, targetId, requestedType, roleId, context.UserId!.Value,
                context.IsPlatformSuperAdmin, ActorUserType(companyId, context),
                ActorCompanyPermissions(companyId, context),
                correlationId == Guid.Empty ? Guid.NewGuid() : correlationId), cancellationToken);

        return result.Status == CompanyMembershipMutationStatus.Success && result.Membership is not null
            ? result.Membership
            : throw MutationError(result.Status);
    }

    public async Task<bool> RevokeMembershipAsync(
        Guid companyId, string targetUserId, ICurrentUserContext context,
        Guid correlationId, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        Guid targetId = RequireControlPlaneUserId(targetUserId);
        CompanyMembershipMutationResult result = await repository.RevokeMembershipAsync(
            new CompanyMembershipRevokeCommand(
                companyId, targetId, context.UserId!.Value, context.IsPlatformSuperAdmin,
                ActorUserType(companyId, context), correlationId == Guid.Empty ? Guid.NewGuid() : correlationId), cancellationToken);
        return result.Status == CompanyMembershipMutationStatus.Success ? result.Changed : throw MutationError(result.Status);
    }

    public async Task<bool> SetMembershipStatusAsync(
        Guid companyId, string targetUserId, bool isActive, ICurrentUserContext context,
        Guid correlationId, CancellationToken cancellationToken)
    {
        RequireCompanyAdministration(companyId, context);
        CompanyMembershipMutationResult result = await repository.SetMembershipStatusAsync(
            new CompanyMembershipRevokeCommand(companyId, RequireControlPlaneUserId(targetUserId), context.UserId!.Value,
                context.IsPlatformSuperAdmin, ActorUserType(companyId, context),
                correlationId == Guid.Empty ? Guid.NewGuid() : correlationId), isActive, cancellationToken);
        return result.Status == CompanyMembershipMutationStatus.Success ? result.Changed : throw MutationError(result.Status);
    }

    private void RequireCompanyAdministration(Guid companyId, ICurrentUserContext context)
    {
        RequireControlPlaneIdentity(context);
        if (companyId == Guid.Empty) throw Error(400, "Công ty không hợp lệ.");
        if (context.IsPlatformSuperAdmin) return;
        if (context.ActiveBranchId.HasValue) throw Error(403, "Không thể quản lý quyền công ty trong không gian chi nhánh.");
        if (!context.ActiveCompanyId.HasValue || context.ActiveCompanyId.Value != companyId)
            throw Error(403, "Không được quản lý tài khoản ngoài công ty đang hoạt động.");
        if (!accessScope.CanAccessCompany(context, companyId)) throw Error(403, "Tài khoản không thuộc công ty được yêu cầu.");
        if (ActorUserType(companyId, context) == ControlPlaneUserType.Owner) return;
        if (!accessScope.HasCompanyPermission(context, companyId, RequiredPermission))
            throw Error(403, "Bạn không có quyền quản lý người dùng tại công ty này.");
    }

    private static void RequireControlPlaneIdentity(ICurrentUserContext context)
    {
        if (context is null || !context.IsAuthenticated) throw Error(401, "Yêu cầu xác thực.");
        if (!context.IsControlPlaneIdentity || !context.UserId.HasValue || context.UserId == Guid.Empty)
            throw Error(403, "Tài khoản này chưa sẵn sàng để quản lý quyền truy cập.");
    }

    private static void RequirePlatformSuperAdmin(ICurrentUserContext context)
    {
        RequireControlPlaneIdentity(context);
        if (!context.IsPlatformSuperAdmin) throw Error(403, "Chỉ Quản trị nền tảng được thực hiện thao tác này.");
    }

    private void RequireCompanyOwner(Guid companyId, ICurrentUserContext context)
    {
        RequireCompanyAdministration(companyId, context);
        if (!context.IsPlatformSuperAdmin && ActorUserType(companyId, context) != ControlPlaneUserType.Owner)
            throw Error(403, "Chỉ chủ sở hữu công ty được quản lý vai trò nội bộ.");
    }

    private void RequireBranchOrCompanyAdministration(Guid companyId, Guid branchId, ICurrentUserContext context)
    {
        RequireControlPlaneIdentity(context);
        if (context.ActiveBranchId.HasValue)
        {
            if (context.ActiveCompanyId != companyId || context.ActiveBranchId != branchId ||
                !accessScope.CanAccessBranch(context, branchId) ||
                !accessScope.HasBranchPermission(context, branchId, RequiredPermission))
                throw Error(403, "Bạn không có quyền quản lý người dùng tại chi nhánh này.");
            return;
        }
        RequireCompanyAdministration(companyId, context);
    }

    private static HashSet<string> ActorAssignablePermissions(Guid companyId, Guid branchId, ICurrentUserContext context)
    {
        if (context.IsPlatformSuperAdmin) return new HashSet<string>(SystemPermissions.All, StringComparer.Ordinal);
        IEnumerable<string> company = context.CompanyMemberships
            .Where(item => item.CompanyId == companyId).SelectMany(item => item.Permissions);
        IEnumerable<string> branch = context.BranchMemberships
            .Where(item => item.BranchId == branchId).SelectMany(item => item.Permissions);
        return company.Concat(branch).ToHashSet(StringComparer.Ordinal);
    }

    private static Guid RequireControlPlaneUserId(string value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (Guid.TryParse(normalized, out Guid userId) && userId != Guid.Empty) return userId;
        if (normalized.Length == 24 && normalized.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
            throw Error(409, "Tài khoản cũ này chưa sẵn sàng để cấp quyền công ty. Vui lòng tạo tài khoản quản trị trước.");
        throw Error(400, "Người dùng không hợp lệ.");
    }

    private static IReadOnlySet<string> ActorCompanyPermissions(Guid companyId, ICurrentUserContext context) =>
        context.IsPlatformSuperAdmin
            ? new HashSet<string>(SystemPermissions.All, StringComparer.Ordinal)
            : context.CompanyMemberships.First(membership => membership.CompanyId == companyId).Permissions;

    private static ControlPlaneUserType ActorUserType(Guid companyId, ICurrentUserContext context) =>
        context.IsPlatformSuperAdmin ? ControlPlaneUserType.Owner
            : (ControlPlaneUserType)context.CompanyMemberships.First(membership => membership.CompanyId == companyId).UserType;

    private static TTSmartEcom.Application.Common.Errors.ApplicationException MutationError(CompanyMembershipMutationStatus status) => status switch
    {
        CompanyMembershipMutationStatus.CompanyNotFound => Error(404, "Không tìm thấy công ty đang hoạt động."),
        CompanyMembershipMutationStatus.ControlPlaneIdentityNotFound => Error(404, "Không tìm thấy tài khoản quản trị phù hợp."),
        CompanyMembershipMutationStatus.MembershipNotFound => Error(404, "Người dùng chưa có quyền truy cập công ty này."),
        CompanyMembershipMutationStatus.TargetIsPlatformIdentity => Error(403, "Không được sửa Quản trị nền tảng."),
        CompanyMembershipMutationStatus.RoleNotFound => Error(404, "Không tìm thấy vai trò được yêu cầu."),
        CompanyMembershipMutationStatus.RoleHasWrongScope => Error(403, "Vai trò không đúng phạm vi."),
        CompanyMembershipMutationStatus.RoleBelongsToAnotherCompany => Error(403, "Vai trò không thuộc công ty đang quản lý."),
        CompanyMembershipMutationStatus.MembershipTypeExceedsActor => Error(403, "Không được quản lý loại tài khoản ngang hoặc cao hơn thẩm quyền của bạn."),
        CompanyMembershipMutationStatus.RoleExceedsActorPermissions => Error(403, "Không được gán vai trò có quyền vượt quá quyền của bạn."),
        CompanyMembershipMutationStatus.OwnerRequiresPlatformSuperAdmin => Error(403, "Chỉ Quản trị nền tảng được chỉ định chủ sở hữu công ty."),
        CompanyMembershipMutationStatus.OwnerProtected => Error(403, "Chủ sở hữu công ty chỉ có thể được thay đổi bởi Quản trị nền tảng."),
        CompanyMembershipMutationStatus.SelfElevation => Error(403, "Bạn không thể tự nâng quyền của mình."),
        CompanyMembershipMutationStatus.PermissionOutsideEnabledFeature => Error(403, "Vai trò chứa quyền thuộc chức năng chưa được bật cho công ty."),
        CompanyMembershipMutationStatus.SystemTemplateReadOnly => Error(403, "Vai trò mẫu chỉ có thể được sao chép, không thể sửa trực tiếp."),
        CompanyMembershipMutationStatus.InvalidBranch => Error(404, "Không tìm thấy chi nhánh hợp lệ trong công ty."),
        CompanyMembershipMutationStatus.LastOwner => Error(409, "Không thể thu hồi hoặc hạ cấp chủ sở hữu cuối cùng của công ty."),
        _ => Error(409, "Dữ liệu quyền truy cập vừa thay đổi; vui lòng tải lại và thử lại."),
    };

    private static TTSmartEcom.Application.Common.Errors.ApplicationException PersistenceError(InvalidOperationException exception) =>
        exception.Message switch
        {
            "PermissionOutsideEnabledFeature" => MutationError(CompanyMembershipMutationStatus.PermissionOutsideEnabledFeature),
            "SystemTemplateReadOnly" => MutationError(CompanyMembershipMutationStatus.SystemTemplateReadOnly),
            "MembershipNotFound" => MutationError(CompanyMembershipMutationStatus.MembershipNotFound),
            "MembershipTypeExceedsActor" => MutationError(CompanyMembershipMutationStatus.MembershipTypeExceedsActor),
            "RoleHasWrongScope" => MutationError(CompanyMembershipMutationStatus.RoleHasWrongScope),
            "RoleExceedsActorPermissions" => MutationError(CompanyMembershipMutationStatus.RoleExceedsActorPermissions),
            "InvalidBranch" => MutationError(CompanyMembershipMutationStatus.InvalidBranch),
            _ => Error(409, "Dữ liệu quyền truy cập vừa thay đổi; vui lòng tải lại và thử lại."),
        };

    private static TTSmartEcom.Application.Common.Errors.ApplicationException Error(int status, string message) =>
        new(new ApplicationError($"TTS-COMPANY-ACCOUNT-{status}", 5600 + status, status, message));
}
