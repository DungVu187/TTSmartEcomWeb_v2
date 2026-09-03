using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlCompanyAccountAdministrationRepository(IControlDbConnectionFactory factory)
    : ICompanyAccountAdministrationRepository
{
    public async Task<IReadOnlyList<CompanyAccountMembership>> ListMembershipsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        return await ReadMembershipsAsync(connection, null, companyId, null, cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyRoleDefinition>> ListCompanyRolesAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        return await ReadRolesAsync(connection, null, companyId, null, cancellationToken);
    }

    public async Task<CompanyMembershipMutationResult> UpsertMembershipAsync(
        CompanyMembershipUpsertCommand command,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            CompanyMembershipMutationStatus? validation = await ValidateCompanyAndTargetAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (validation.HasValue)
                return await RollbackAsync(transaction, validation.Value, cancellationToken);

            CompanyRoleDefinition? role = await ReadRoleAsync(
                connection, transaction, command.CompanyId, command.RoleId, cancellationToken);
            if (role is null)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.RoleNotFound, cancellationToken);
            if (role.ScopeType != ControlPlaneScopeType.Company)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.RoleHasWrongScope, cancellationToken);
            if (role.CompanyId.HasValue && role.CompanyId.Value != command.CompanyId)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.RoleBelongsToAnotherCompany, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.UserType < command.ActorUserType)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && role.Permissions.Any(permission =>
                    !command.ActorCompanyPermissions.Contains(permission)))
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.RoleExceedsActorPermissions, cancellationToken);

            MembershipRow? membership = await ReadMembershipRowAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && membership is { IsActive: true }
                && membership.UserType < command.ActorUserType)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            if (membership is { IsActive: true, UserType: ControlPlaneUserType.Owner }
                && command.UserType != ControlPlaneUserType.Owner
                && await CountActiveOwnersAsync(connection, transaction, command.CompanyId, cancellationToken) <= 1)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.LastOwner, cancellationToken);

            Guid companyUserId = membership?.CompanyUserId ?? Guid.NewGuid();
            Guid[] currentRoles = membership is null
                ? []
                : await ReadActiveRoleIdsAsync(connection, transaction, companyUserId, cancellationToken);
            bool changed = membership is null
                || !membership.IsActive
                || membership.UserType != command.UserType
                || currentRoles.Length != 1
                || currentRoles[0] != command.RoleId;

            if (changed)
            {
                if (membership is null)
                {
                    await using SqlCommand insertMembership = new("""
                        INSERT dbo.CompanyUsers
                            (CompanyUserId,CompanyId,UserId,UserType,Status,StartsAtUtc,EndsAtUtc,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                        VALUES
                            (@companyUserId,@companyId,@userId,@userType,1,SYSUTCDATETIME(),NULL,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                        """, connection, transaction);
                    AddMembershipParameters(insertMembership, companyUserId, command.CompanyId, command.TargetUserId, command.UserType);
                    await insertMembership.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    await using SqlCommand updateMembership = new("""
                        UPDATE dbo.CompanyUsers
                        SET UserType=@userType,Status=1,StartsAtUtc=COALESCE(StartsAtUtc,SYSUTCDATETIME()),EndsAtUtc=NULL,
                            IsDeleted=0,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                        WHERE CompanyUserId=@companyUserId;
                        """, connection, transaction);
                    updateMembership.Parameters.AddWithValue("@companyUserId", companyUserId);
                    updateMembership.Parameters.AddWithValue("@userType", (byte)command.UserType);
                    await updateMembership.ExecuteNonQueryAsync(cancellationToken);
                }

                await ReplaceRoleAsync(
                    connection, transaction, companyUserId, command.RoleId, command.ActorUserId, cancellationToken);
                await AppendAuditAsync(
                    connection,
                    transaction,
                    command.ActorUserId,
                    command.CompanyId,
                    companyUserId,
                    membership is null || !membership.IsActive ? "company.account.assign" : "company.account.update",
                    command.CorrelationId,
                    new
                    {
                        targetUserId = command.TargetUserId,
                        before = membership is null ? null : new { userType = (byte)membership.UserType, roleIds = currentRoles },
                        after = new { userType = (byte)command.UserType, roleIds = new[] { command.RoleId } },
                    },
                    cancellationToken);
            }

            CompanyAccountMembership? result = (await ReadMembershipsAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken)).SingleOrDefault();
            await transaction.CommitAsync(cancellationToken);
            return new(CompanyMembershipMutationStatus.Success, changed, result);
        }
        catch (SqlException exception) when (exception.Number is 1205 or 2601 or 2627 or 3960)
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            return new(CompanyMembershipMutationStatus.Conflict, false);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<CompanyMembershipMutationResult> RevokeMembershipAsync(
        CompanyMembershipRevokeCommand command,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            CompanyMembershipMutationStatus? validation = await ValidateCompanyAndTargetAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (validation.HasValue)
                return await RollbackAsync(transaction, validation.Value, cancellationToken);

            MembershipRow? membership = await ReadMembershipRowAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (membership is null)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipNotFound, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && membership.IsActive
                && membership.UserType < command.ActorUserType)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            if (!membership.IsActive)
            {
                await transaction.CommitAsync(cancellationToken);
                return new(CompanyMembershipMutationStatus.Success, false);
            }
            if (membership.UserType == ControlPlaneUserType.Owner
                && await CountActiveOwnersAsync(connection, transaction, command.CompanyId, cancellationToken) <= 1)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.LastOwner, cancellationToken);

            Guid[] roleIds = await ReadActiveRoleIdsAsync(
                connection, transaction, membership.CompanyUserId, cancellationToken);
            await using (SqlCommand revokeRoles = new("""
                UPDATE dbo.UserRoles
                SET IsDeleted=1,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE CompanyUserId=@companyUserId AND IsDeleted=0;
                """, connection, transaction))
            {
                revokeRoles.Parameters.AddWithValue("@companyUserId", membership.CompanyUserId);
                await revokeRoles.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (SqlCommand revokeMembership = new("""
                UPDATE dbo.CompanyUsers
                SET Status=0,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),IsDeleted=1,
                    Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE CompanyUserId=@companyUserId AND Status=1 AND IsDeleted=0;
                """, connection, transaction))
            {
                revokeMembership.Parameters.AddWithValue("@companyUserId", membership.CompanyUserId);
                await revokeMembership.ExecuteNonQueryAsync(cancellationToken);
            }

            await AppendAuditAsync(
                connection,
                transaction,
                command.ActorUserId,
                command.CompanyId,
                membership.CompanyUserId,
                "company.account.revoke",
                command.CorrelationId,
                new
                {
                    targetUserId = command.TargetUserId,
                    before = new { userType = (byte)membership.UserType, roleIds },
                    after = (object?)null,
                },
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new(CompanyMembershipMutationStatus.Success, true);
        }
        catch (SqlException exception) when (exception.Number is 1205 or 2601 or 2627 or 3960)
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            return new(CompanyMembershipMutationStatus.Conflict, false);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<CompanyMembershipMutationStatus?> ValidateCompanyAndTargetAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        await using (SqlCommand company = new("""
            SELECT CompanyId FROM dbo.Companies WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@companyId AND Status=1 AND IsDeleted=0;
            """, connection, transaction))
        {
            company.Parameters.AddWithValue("@companyId", companyId);
            if (await company.ExecuteScalarAsync(cancellationToken) is not Guid)
                return CompanyMembershipMutationStatus.CompanyNotFound;
        }

        await using SqlCommand user = new("""
            SELECT AccountType FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
            WHERE UserId=@userId AND Status=1 AND IsDeleted=0;
            """, connection, transaction);
        user.Parameters.AddWithValue("@userId", targetUserId);
        object? accountType = await user.ExecuteScalarAsync(cancellationToken);
        if (accountType is null or DBNull) return CompanyMembershipMutationStatus.ControlPlaneIdentityNotFound;
        return Convert.ToByte(accountType, System.Globalization.CultureInfo.InvariantCulture) == (byte)ControlPlaneAccountType.Platform
            ? CompanyMembershipMutationStatus.TargetIsPlatformIdentity
            : null;
    }

    private static async Task<CompanyRoleDefinition?> ReadRoleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CompanyRoleDefinition> roles = await ReadRolesAsync(
            connection, transaction, companyId, roleId, cancellationToken, includeForeignCompanyRole: true);
        return roles.SingleOrDefault();
    }

    private static async Task<IReadOnlyList<CompanyRoleDefinition>> ReadRolesAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid companyId,
        Guid? roleId,
        CancellationToken cancellationToken,
        bool includeForeignCompanyRole = false)
    {
        await using SqlCommand command = new() { Connection = connection, Transaction = transaction };
        command.CommandText = $"""
            SELECT r.RoleId,r.CompanyId,r.RoleCode,r.Name,r.ScopeType,r.IsSystemTemplate,p.PermissionCode
            FROM dbo.Roles r{(transaction is null ? string.Empty : " WITH (UPDLOCK,HOLDLOCK)")}
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId AND rp.IsDeleted=0
            LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.Status=1 AND p.IsDeleted=0
            WHERE r.Status=1 AND r.IsDeleted=0
              AND (@roleId IS NULL OR r.RoleId=@roleId)
              AND (@includeForeign=1 OR (r.ScopeType=1 AND (r.CompanyId IS NULL OR r.CompanyId=@companyId)))
            ORDER BY r.Name,r.RoleId,p.PermissionCode;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.Add("@roleId", SqlDbType.UniqueIdentifier).Value = (object?)roleId ?? DBNull.Value;
        command.Parameters.AddWithValue("@includeForeign", includeForeignCompanyRole);

        Dictionary<Guid, RoleBuilder> builders = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid id = reader.GetGuid(0);
            if (!builders.TryGetValue(id, out RoleBuilder? builder))
            {
                builder = new(
                    id,
                    reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    (ControlPlaneScopeType)reader.GetByte(4),
                    reader.GetBoolean(5));
                builders.Add(id, builder);
            }
            if (!reader.IsDBNull(6)) builder.Permissions.Add(reader.GetString(6));
        }
        return builders.Values.Select(static builder => builder.Build()).ToArray();
    }

    private static async Task<IReadOnlyList<CompanyAccountMembership>> ReadMembershipsAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid companyId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new() { Connection = connection, Transaction = transaction };
        command.CommandText = """
            SELECT cu.CompanyUserId,cu.CompanyId,cu.UserId,u.DisplayName,
                   (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=2 AND IsDeleted=0 ORDER BY IsPrimary DESC) Email,
                   (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=1 AND IsDeleted=0 ORDER BY IsPrimary DESC) Phone,
                   u.AccountType,cu.UserType,cu.Status,
                   r.RoleId,r.CompanyId,r.RoleCode,r.Name,r.ScopeType,r.IsSystemTemplate,p.PermissionCode
            FROM dbo.CompanyUsers cu
            INNER JOIN dbo.Users u ON u.UserId=cu.UserId AND u.IsDeleted=0
            LEFT JOIN dbo.UserRoles ur ON ur.CompanyUserId=cu.CompanyUserId AND ur.IsDeleted=0
                AND (ur.StartsAtUtc IS NULL OR ur.StartsAtUtc<=SYSUTCDATETIME())
                AND (ur.EndsAtUtc IS NULL OR ur.EndsAtUtc>=SYSUTCDATETIME())
            LEFT JOIN dbo.Roles r ON r.RoleId=ur.RoleId AND r.ScopeType=1 AND r.Status=1 AND r.IsDeleted=0
                AND (r.CompanyId IS NULL OR r.CompanyId=cu.CompanyId)
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId AND rp.IsDeleted=0
            LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.Status=1 AND p.IsDeleted=0
            WHERE cu.CompanyId=@companyId AND cu.Status=1 AND cu.IsDeleted=0
              AND (@userId IS NULL OR cu.UserId=@userId)
            ORDER BY u.DisplayName,cu.UserId,r.Name,p.PermissionCode;
            """;
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.Add("@userId", SqlDbType.UniqueIdentifier).Value = (object?)userId ?? DBNull.Value;

        Dictionary<Guid, MembershipBuilder> builders = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid companyUserId = reader.GetGuid(0);
            if (!builders.TryGetValue(companyUserId, out MembershipBuilder? membership))
            {
                membership = new(
                    companyUserId,
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    (ControlPlaneAccountType)reader.GetByte(6),
                    (ControlPlaneUserType)reader.GetByte(7),
                    reader.GetByte(8));
                builders.Add(companyUserId, membership);
            }
            if (!reader.IsDBNull(9))
            {
                Guid roleId = reader.GetGuid(9);
                if (!membership.Roles.TryGetValue(roleId, out RoleBuilder? role))
                {
                    role = new(
                        roleId,
                        reader.IsDBNull(10) ? null : reader.GetGuid(10),
                        reader.GetString(11),
                        reader.GetString(12),
                        (ControlPlaneScopeType)reader.GetByte(13),
                        reader.GetBoolean(14));
                    membership.Roles.Add(roleId, role);
                }
                if (!reader.IsDBNull(15)) role.Permissions.Add(reader.GetString(15));
            }
        }
        return builders.Values.Select(static builder => builder.Build()).ToArray();
    }

    private static async Task<MembershipRow?> ReadMembershipRowAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            SELECT TOP(1) CompanyUserId,UserType,Status,IsDeleted
            FROM dbo.CompanyUsers WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@companyId AND UserId=@userId
            ORDER BY CASE WHEN Status=1 AND IsDeleted=0 THEN 0 ELSE 1 END,UpdatedAtUtc DESC;
            """, connection, transaction);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@userId", userId);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MembershipRow(
                reader.GetGuid(0),
                (ControlPlaneUserType)reader.GetByte(1),
                reader.GetByte(2) == 1 && !reader.GetBoolean(3))
            : null;
    }

    private static async Task<int> CountActiveOwnersAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            SELECT COUNT(*) FROM dbo.CompanyUsers WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyId=@companyId AND UserType=1 AND Status=1 AND IsDeleted=0;
            """, connection, transaction);
        command.Parameters.AddWithValue("@companyId", companyId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<Guid[]> ReadActiveRoleIdsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyUserId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            SELECT RoleId FROM dbo.UserRoles WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyUserId=@companyUserId AND IsDeleted=0
            ORDER BY RoleId;
            """, connection, transaction);
        command.Parameters.AddWithValue("@companyUserId", companyUserId);
        List<Guid> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0));
        return result.ToArray();
    }

    private static async Task ReplaceRoleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyUserId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        await using (SqlCommand retire = new("""
            UPDATE dbo.UserRoles
            SET IsDeleted=1,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
            WHERE CompanyUserId=@companyUserId AND RoleId<>@roleId AND IsDeleted=0;
            """, connection, transaction))
        {
            retire.Parameters.AddWithValue("@companyUserId", companyUserId);
            retire.Parameters.AddWithValue("@roleId", roleId);
            await retire.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid? existingId;
        await using (SqlCommand find = new("""
            SELECT TOP(1) UserRoleId FROM dbo.UserRoles WITH (UPDLOCK,HOLDLOCK)
            WHERE CompanyUserId=@companyUserId AND RoleId=@roleId
            ORDER BY IsDeleted,UpdatedAtUtc DESC;
            """, connection, transaction))
        {
            find.Parameters.AddWithValue("@companyUserId", companyUserId);
            find.Parameters.AddWithValue("@roleId", roleId);
            object? value = await find.ExecuteScalarAsync(cancellationToken);
            existingId = value is Guid id ? id : null;
        }

        if (existingId.HasValue)
        {
            await using SqlCommand restore = new("""
                UPDATE dbo.UserRoles
                SET IsDeleted=0,StartsAtUtc=COALESCE(StartsAtUtc,SYSUTCDATETIME()),EndsAtUtc=NULL,
                    Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE UserRoleId=@userRoleId;
                """, connection, transaction);
            restore.Parameters.AddWithValue("@userRoleId", existingId.Value);
            await restore.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using SqlCommand insert = new("""
                INSERT dbo.UserRoles
                    (UserRoleId,RoleId,CompanyUserId,BranchUserId,StartsAtUtc,EndsAtUtc,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                VALUES
                    (NEWID(),@roleId,@companyUserId,NULL,SYSUTCDATETIME(),NULL,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                """, connection, transaction);
            insert.Parameters.AddWithValue("@roleId", roleId);
            insert.Parameters.AddWithValue("@companyUserId", companyUserId);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task AppendAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid actorUserId,
        Guid companyId,
        Guid companyUserId,
        string action,
        Guid correlationId,
        object safeDetail,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            INSERT dbo.AuditLogs
                (AuditLogId,OccurredAtUtc,ActorUserId,CompanyId,BranchId,ActionCode,EntityType,EntityId,Outcome,CorrelationId,SafeDetailJson,CreatedAtUtc)
            VALUES
                (NEWID(),SYSUTCDATETIME(),@actorUserId,@companyId,NULL,@action,N'CompanyUser',@entityId,1,@correlationId,@detail,SYSUTCDATETIME());
            """, connection, transaction);
        command.Parameters.AddWithValue("@actorUserId", actorUserId);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue("@entityId", companyUserId);
        command.Parameters.AddWithValue("@correlationId", correlationId);
        command.Parameters.AddWithValue("@detail", JsonSerializer.Serialize(safeDetail));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddMembershipParameters(
        SqlCommand command,
        Guid companyUserId,
        Guid companyId,
        Guid userId,
        ControlPlaneUserType userType)
    {
        command.Parameters.AddWithValue("@companyUserId", companyUserId);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@userType", (byte)userType);
    }

    private static async Task<CompanyMembershipMutationResult> RollbackAsync(
        SqlTransaction transaction,
        CompanyMembershipMutationStatus status,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return new(status, false);
    }

    private sealed record MembershipRow(Guid CompanyUserId, ControlPlaneUserType UserType, bool IsActive);

    private sealed class RoleBuilder(
        Guid roleId,
        Guid? companyId,
        string roleCode,
        string name,
        ControlPlaneScopeType scopeType,
        bool isSystemTemplate)
    {
        public HashSet<string> Permissions { get; } = new(StringComparer.Ordinal);

        public CompanyRoleDefinition Build() => new(
            roleId,
            companyId,
            roleCode,
            name,
            scopeType,
            isSystemTemplate,
            Permissions);
    }

    private sealed class MembershipBuilder(
        Guid companyUserId,
        Guid companyId,
        Guid userId,
        string displayName,
        string? email,
        string? phone,
        ControlPlaneAccountType accountType,
        ControlPlaneUserType userType,
        byte status)
    {
        public Dictionary<Guid, RoleBuilder> Roles { get; } = [];

        public CompanyAccountMembership Build() => new(
            companyUserId,
            companyId,
            userId,
            displayName,
            email,
            phone,
            accountType,
            userType,
            status,
            Roles.Values.Select(static role => role.Build()).ToArray());
    }
}
