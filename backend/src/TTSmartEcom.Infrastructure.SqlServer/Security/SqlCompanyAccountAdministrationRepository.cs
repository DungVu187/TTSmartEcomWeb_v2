using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlCompanyAccountAdministrationRepository(IControlDbConnectionFactory factory)
    : ICompanyAccountAdministrationRepository
{
    public async Task<IReadOnlyList<ControlPlaneCompanySummary>> ListCompaniesAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT CompanyId,CompanyCode,DisplayName
            FROM dbo.Companies
            WHERE Status=1 AND IsDeleted=0
            ORDER BY DisplayName,CompanyCode;
            """, connection);
        List<ControlPlaneCompanySummary> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2)));
        return result;
    }

    public async Task<IReadOnlyList<ControlPlaneUserSummary>> SearchUsersAsync(
        string query, bool exact, int limit, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT TOP (@limit) u.UserId,u.DisplayName,u.AccountType,u.Status,
              (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=2 AND IsDeleted=0 ORDER BY IsPrimary DESC) Email,
              (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=1 AND IsDeleted=0 ORDER BY IsPrimary DESC) Phone
            FROM dbo.Users u
            WHERE u.IsDeleted=0 AND u.AccountType<>1
              AND (@exact=0 AND (u.DisplayName LIKE @contains OR EXISTS(
                    SELECT 1 FROM dbo.UserLogins ul WHERE ul.UserId=u.UserId AND ul.IsDeleted=0 AND ul.DisplayValue LIKE @contains))
                OR @exact=1 AND EXISTS(
                    SELECT 1 FROM dbo.UserLogins ul WHERE ul.UserId=u.UserId AND ul.IsDeleted=0
                      AND ul.IdentifierType IN (1,2) AND ul.NormalizedValue=@normalized))
            ORDER BY u.DisplayName,u.UserId;
            """, connection);
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 50));
        command.Parameters.AddWithValue("@exact", exact);
        command.Parameters.AddWithValue("@contains", "%" + query.Trim() + "%");
        command.Parameters.AddWithValue("@normalized", query.Trim().ToUpperInvariant());
        List<ControlPlaneUserSummary> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetGuid(0), reader.GetString(1),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
                (ControlPlaneAccountType)reader.GetByte(2), (ControlPlaneUserStatus)reader.GetByte(3)));
        return result;
    }

    public async Task<IReadOnlyList<EffectivePermissionDefinition>> ListEffectivePermissionsAsync(
        Guid companyId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT DISTINCT p.PermissionId,p.PermissionCode,p.Name,p.ModuleCode,f.Name,p.Description
            FROM dbo.Permissions p
            INNER JOIN dbo.Features f ON f.ModuleCode=p.ModuleCode AND f.Status=1 AND f.IsDeleted=0
            INNER JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId AND cf.CompanyId=@companyId
              AND cf.IsEnabled=1 AND cf.IsDeleted=0
              AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
              AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME())
            WHERE p.Status=1 AND p.IsDeleted=0
            ORDER BY p.ModuleCode,p.Name;
            """, connection);
        command.Parameters.AddWithValue("@companyId", companyId);
        List<EffectivePermissionDefinition> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5)));
        return result;
    }

    public async Task<IReadOnlyList<FeatureAccessSetting>> ListFeatureSettingsAsync(
        Guid companyId, Guid? branchId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT f.FeatureId,f.FeatureCode,f.Name,f.ModuleCode,
              CONVERT(bit,CASE WHEN cf.IsEnabled=1 AND cf.IsDeleted=0
                AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
                AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME()) THEN 1 ELSE 0 END) CompanyEnabled,
              CASE WHEN @branchId IS NULL THEN NULL ELSE CONVERT(bit,CASE WHEN bf.BranchFeatureSettingId IS NULL THEN 1
                WHEN bf.IsEnabled=1 AND bf.IsDeleted=0
                  AND (bf.EffectiveFromUtc IS NULL OR bf.EffectiveFromUtc<=SYSUTCDATETIME())
                  AND (bf.EffectiveToUtc IS NULL OR bf.EffectiveToUtc>=SYSUTCDATETIME()) THEN 1 ELSE 0 END) END BranchEnabled
            FROM dbo.Features f
            LEFT JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId AND cf.CompanyId=@companyId AND cf.IsDeleted=0
            LEFT JOIN dbo.BranchFeatureSettings bf ON bf.FeatureId=f.FeatureId AND bf.BranchId=@branchId AND bf.IsDeleted=0
            WHERE f.Status=1 AND f.IsDeleted=0
            ORDER BY f.Name,f.FeatureCode;
            """, connection);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.Add("@branchId", SqlDbType.UniqueIdentifier).Value = (object?)branchId ?? DBNull.Value;
        List<FeatureAccessSetting> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(new(reader.GetGuid(0), reader.GetString(1),
            reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.IsDBNull(5) ? null : reader.GetBoolean(5)));
        return result;
    }

    public async Task<bool> SetFeatureAsync(
        Guid companyId, Guid? branchId, Guid featureId, bool enabled,
        Guid actorUserId, Guid correlationId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            bool before;
            Guid entityId;
            if (branchId.HasValue)
            {
                await using SqlCommand validate = new("""
                    SELECT CASE WHEN cf.IsEnabled=1 AND cf.IsDeleted=0 THEN 1 ELSE 0 END
                    FROM dbo.Branches b WITH(UPDLOCK,HOLDLOCK)
                    INNER JOIN dbo.Features f ON f.FeatureId=@featureId AND f.Status=1 AND f.IsDeleted=0
                    LEFT JOIN dbo.CompanyFeatureSettings cf ON cf.CompanyId=b.CompanyId AND cf.FeatureId=f.FeatureId AND cf.IsDeleted=0
                    WHERE b.BranchId=@branchId AND b.CompanyId=@companyId AND b.Status=1 AND b.IsDeleted=0;
                    """, connection, transaction);
                validate.Parameters.AddWithValue("@featureId", featureId);
                validate.Parameters.AddWithValue("@branchId", branchId.Value);
                validate.Parameters.AddWithValue("@companyId", companyId);
                object? companyEnabled = await validate.ExecuteScalarAsync(cancellationToken);
                if (companyEnabled is null || companyEnabled is DBNull || enabled &&
                    Convert.ToInt32(companyEnabled, System.Globalization.CultureInfo.InvariantCulture) != 1)
                    throw new InvalidOperationException("BranchFeatureExceedsCompany");
                entityId = Guid.NewGuid();
                await using SqlCommand upsert = new("""
                    SELECT @existingId=BranchFeatureSettingId,@before=IsEnabled
                    FROM dbo.BranchFeatureSettings WITH(UPDLOCK,HOLDLOCK)
                    WHERE BranchId=@branchId AND FeatureId=@featureId AND IsDeleted=0;
                    IF @existingId IS NULL
                      INSERT dbo.BranchFeatureSettings
                        (BranchFeatureSettingId,BranchId,FeatureId,IsEnabled,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                      VALUES (@newId,@branchId,@featureId,@enabled,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                    ELSE
                      UPDATE dbo.BranchFeatureSettings SET IsEnabled=@enabled,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                      WHERE BranchFeatureSettingId=@existingId AND IsEnabled<>@enabled;
                    SELECT COALESCE(@existingId,@newId),COALESCE(@before,CONVERT(bit,0));
                    """, connection, transaction);
                upsert.Parameters.Add("@existingId", SqlDbType.UniqueIdentifier).Direction = ParameterDirection.InputOutput;
                upsert.Parameters["@existingId"].Value = DBNull.Value;
                upsert.Parameters.Add("@before", SqlDbType.Bit).Direction = ParameterDirection.InputOutput;
                upsert.Parameters["@before"].Value = DBNull.Value;
                upsert.Parameters.AddWithValue("@newId", entityId);
                upsert.Parameters.AddWithValue("@branchId", branchId.Value);
                upsert.Parameters.AddWithValue("@featureId", featureId);
                upsert.Parameters.AddWithValue("@enabled", enabled);
                await using SqlDataReader reader = await upsert.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                entityId = reader.GetGuid(0); before = reader.GetBoolean(1);
            }
            else
            {
                entityId = Guid.NewGuid();
                await using SqlCommand upsert = new("""
                    IF NOT EXISTS(SELECT 1 FROM dbo.Companies WHERE CompanyId=@companyId AND Status=1 AND IsDeleted=0)
                      THROW 56001,N'CompanyNotFound',1;
                    IF NOT EXISTS(SELECT 1 FROM dbo.Features WHERE FeatureId=@featureId AND Status=1 AND IsDeleted=0)
                      THROW 56002,N'FeatureNotFound',1;
                    SELECT @existingId=CompanyFeatureSettingId,@before=IsEnabled
                    FROM dbo.CompanyFeatureSettings WITH(UPDLOCK,HOLDLOCK)
                    WHERE CompanyId=@companyId AND FeatureId=@featureId AND IsDeleted=0;
                    IF @existingId IS NULL
                      INSERT dbo.CompanyFeatureSettings
                        (CompanyFeatureSettingId,CompanyId,FeatureId,IsEnabled,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                      VALUES (@newId,@companyId,@featureId,@enabled,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                    ELSE
                      UPDATE dbo.CompanyFeatureSettings SET IsEnabled=@enabled,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                      WHERE CompanyFeatureSettingId=@existingId AND IsEnabled<>@enabled;
                    SELECT COALESCE(@existingId,@newId),COALESCE(@before,CONVERT(bit,0));
                    """, connection, transaction);
                upsert.Parameters.Add("@existingId", SqlDbType.UniqueIdentifier).Direction = ParameterDirection.InputOutput;
                upsert.Parameters["@existingId"].Value = DBNull.Value;
                upsert.Parameters.Add("@before", SqlDbType.Bit).Direction = ParameterDirection.InputOutput;
                upsert.Parameters["@before"].Value = DBNull.Value;
                upsert.Parameters.AddWithValue("@newId", entityId);
                upsert.Parameters.AddWithValue("@companyId", companyId);
                upsert.Parameters.AddWithValue("@featureId", featureId);
                upsert.Parameters.AddWithValue("@enabled", enabled);
                await using SqlDataReader reader = await upsert.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                entityId = reader.GetGuid(0); before = reader.GetBoolean(1);
            }
            bool changed = before != enabled;
            if (changed) await AppendAuditAsync(connection, transaction, actorUserId, companyId, entityId,
                branchId.HasValue ? "branch.feature.update" : "company.feature.update", correlationId,
                new { featureId, before, after = enabled }, cancellationToken,
                branchId.HasValue ? "BranchFeatureSetting" : "CompanyFeatureSetting", branchId);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<CompanyBranchAccess>> ListBranchesForUserAsync(
        Guid companyId, Guid userId, Guid? activeBranchId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT b.BranchId,b.BranchCode,b.Name,b.Status,bu.BranchUserId,
                   r.RoleId,r.CompanyId,r.RoleCode,r.Name,r.ScopeType,r.IsSystemTemplate,p.PermissionCode
            FROM dbo.Branches b
            LEFT JOIN dbo.BranchUsers bu ON bu.BranchId=b.BranchId AND bu.UserId=@userId AND bu.Status=1 AND bu.IsDeleted=0
            LEFT JOIN dbo.UserRoles ur ON ur.BranchUserId=bu.BranchUserId AND ur.IsDeleted=0
            LEFT JOIN dbo.Roles r ON r.RoleId=ur.RoleId AND r.ScopeType=2 AND r.Status=1 AND r.IsDeleted=0
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId AND rp.IsDeleted=0
            LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.Status=1 AND p.IsDeleted=0
              AND EXISTS (
                SELECT 1 FROM dbo.Features f
                INNER JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId
                  AND cf.CompanyId=@companyId AND cf.IsEnabled=1 AND cf.IsDeleted=0
                  AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
                  AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME())
                WHERE f.ModuleCode=p.ModuleCode AND f.Status=1 AND f.IsDeleted=0)
            WHERE b.CompanyId=@companyId AND b.Status=1 AND b.IsDeleted=0
              AND (@activeBranchId IS NULL OR b.BranchId=@activeBranchId)
            ORDER BY b.Name,b.BranchId,r.Name,p.PermissionCode;
            """, connection);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.Add("@activeBranchId", SqlDbType.UniqueIdentifier).Value = (object?)activeBranchId ?? DBNull.Value;
        Dictionary<Guid, BranchBuilder> builders = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid branchId = reader.GetGuid(0);
            if (!builders.TryGetValue(branchId, out BranchBuilder? branch))
            {
                branch = new(branchId, reader.GetString(1), reader.GetString(2), reader.GetByte(3), !reader.IsDBNull(4));
                builders.Add(branchId, branch);
            }
            if (!reader.IsDBNull(5))
            {
                Guid roleId = reader.GetGuid(5);
                if (!branch.Roles.TryGetValue(roleId, out RoleBuilder? role))
                {
                    role = new(roleId, reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetString(7),
                        reader.GetString(8), (ControlPlaneScopeType)reader.GetByte(9), reader.GetBoolean(10));
                    branch.Roles.Add(roleId, role);
                }
                if (!reader.IsDBNull(11)) role.Permissions.Add(reader.GetString(11));
            }
        }
        return builders.Values.Select(static item => item.Build()).ToArray();
    }

    public async Task<IReadOnlyList<BranchAccountMembership>> ListBranchMembershipsAsync(
        Guid companyId, Guid branchId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlCommand command = new("""
            SELECT bu.BranchUserId,b.CompanyId,bu.BranchId,bu.UserId,u.DisplayName,
              (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=2 AND IsDeleted=0 ORDER BY IsPrimary DESC) Email,
              (SELECT TOP(1) DisplayValue FROM dbo.UserLogins WHERE UserId=u.UserId AND IdentifierType=1 AND IsDeleted=0 ORDER BY IsPrimary DESC) Phone,
              bu.Status,r.RoleId,r.CompanyId,r.RoleCode,r.Name,r.ScopeType,r.IsSystemTemplate,p.PermissionCode
            FROM dbo.BranchUsers bu
            INNER JOIN dbo.Branches b ON b.BranchId=bu.BranchId AND b.CompanyId=@companyId AND b.Status=1 AND b.IsDeleted=0
            INNER JOIN dbo.Users u ON u.UserId=bu.UserId AND u.IsDeleted=0
            LEFT JOIN dbo.UserRoles ur ON ur.BranchUserId=bu.BranchUserId AND ur.IsDeleted=0
            LEFT JOIN dbo.Roles r ON r.RoleId=ur.RoleId AND r.ScopeType=2 AND r.Status=1 AND r.IsDeleted=0
              AND r.CompanyId=@companyId
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId AND rp.IsDeleted=0
            LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.Status=1 AND p.IsDeleted=0
            WHERE bu.BranchId=@branchId AND bu.Status=1 AND bu.IsDeleted=0
            ORDER BY u.DisplayName,u.UserId,r.Name,p.PermissionCode;
            """, connection);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.AddWithValue("@branchId", branchId);
        Dictionary<Guid, BranchMembershipBuilder> builders = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            Guid id = reader.GetGuid(0);
            if (!builders.TryGetValue(id, out BranchMembershipBuilder? membership))
            {
                membership = new(id, reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetByte(7));
                builders.Add(id, membership);
            }
            if (!reader.IsDBNull(8))
            {
                Guid roleId = reader.GetGuid(8);
                if (!membership.Roles.TryGetValue(roleId, out RoleBuilder? role))
                {
                    role = new(roleId, reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.GetString(10), reader.GetString(11),
                        (ControlPlaneScopeType)reader.GetByte(12), reader.GetBoolean(13));
                    membership.Roles.Add(roleId, role);
                }
                if (!reader.IsDBNull(14)) role.Permissions.Add(reader.GetString(14));
            }
        }
        return builders.Values.Select(static item => item.Build()).ToArray();
    }

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

    public async Task<CompanyRoleDefinition> SaveRoleAsync(
        CompanyRoleSaveCommand command,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            Guid[] allowed = (await ReadEffectivePermissionIdsAsync(
                connection, transaction, command.CompanyId, cancellationToken)).ToArray();
            if (command.PermissionIds.Any(id => !allowed.Contains(id)))
                throw new InvalidOperationException("PermissionOutsideEnabledFeature");

            Guid roleId = command.RoleId ?? Guid.NewGuid();
            string roleCode = "CUSTOM_" + roleId.ToString("N").ToUpperInvariant();
            if (command.RoleId.HasValue)
            {
                await using SqlCommand update = new("""
                    UPDATE dbo.Roles
                    SET Name=@name,ScopeType=@scope,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                    WHERE RoleId=@roleId AND CompanyId=@companyId AND IsSystemTemplate=0 AND IsDeleted=0
                      AND (ScopeType=@scope OR NOT EXISTS(SELECT 1 FROM dbo.UserRoles WHERE RoleId=@roleId AND IsDeleted=0));
                    """, connection, transaction);
                update.Parameters.AddWithValue("@name", command.Name);
                update.Parameters.AddWithValue("@scope", (byte)command.ScopeType);
                update.Parameters.AddWithValue("@roleId", roleId);
                update.Parameters.AddWithValue("@companyId", command.CompanyId);
                if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidOperationException("SystemTemplateReadOnly");
            }
            else
            {
                await using SqlCommand insert = new("""
                    INSERT dbo.Roles
                      (RoleId,CompanyId,RoleCode,NormalizedRoleCode,Name,ScopeType,IsSystemTemplate,Status,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                    VALUES
                      (@roleId,@companyId,@code,@code,@name,@scope,0,1,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                    """, connection, transaction);
                insert.Parameters.AddWithValue("@roleId", roleId);
                insert.Parameters.AddWithValue("@companyId", command.CompanyId);
                insert.Parameters.AddWithValue("@code", roleCode);
                insert.Parameters.AddWithValue("@name", command.Name);
                insert.Parameters.AddWithValue("@scope", (byte)command.ScopeType);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (SqlCommand retire = new("""
                UPDATE dbo.RolePermissions
                SET IsDeleted=1,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE RoleId=@roleId AND IsDeleted=0
                  AND PermissionId NOT IN (SELECT value FROM OPENJSON(@permissionIds) WITH (value uniqueidentifier '$'));
                """, connection, transaction))
            {
                retire.Parameters.AddWithValue("@roleId", roleId);
                retire.Parameters.AddWithValue("@permissionIds", JsonSerializer.Serialize(command.PermissionIds));
                await retire.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (Guid permissionId in command.PermissionIds.Distinct())
            {
                await using SqlCommand grant = new("""
                    UPDATE dbo.RolePermissions
                    SET IsDeleted=0,GrantedAtUtc=SYSUTCDATETIME(),GrantedByUserId=@actor,
                        Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                    WHERE RoleId=@roleId AND PermissionId=@permissionId AND IsDeleted=1;
                    IF @@ROWCOUNT=0 AND NOT EXISTS(
                        SELECT 1 FROM dbo.RolePermissions WHERE RoleId=@roleId AND PermissionId=@permissionId AND IsDeleted=0)
                      INSERT dbo.RolePermissions
                        (RolePermissionId,RoleId,PermissionId,GrantedAtUtc,GrantedByUserId,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                      VALUES (NEWID(),@roleId,@permissionId,SYSUTCDATETIME(),@actor,1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                    """, connection, transaction);
                grant.Parameters.AddWithValue("@roleId", roleId);
                grant.Parameters.AddWithValue("@permissionId", permissionId);
                grant.Parameters.AddWithValue("@actor", command.ActorUserId);
                await grant.ExecuteNonQueryAsync(cancellationToken);
            }

            await AppendAuditAsync(connection, transaction, command.ActorUserId, command.CompanyId, roleId,
                command.RoleId.HasValue ? "company.role.update" : "company.role.create", command.CorrelationId,
                new { roleId, command.Name, description = command.Description, scopeType = (byte)command.ScopeType, permissionIds = command.PermissionIds }, cancellationToken,
                entityType: "Role");
            CompanyRoleDefinition result = (await ReadRolesAsync(
                connection, transaction, command.CompanyId, roleId, cancellationToken)).Single();
            await transaction.CommitAsync(cancellationToken);
            return result with { Description = command.Description };
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> SaveBranchMembershipAsync(
        BranchMembershipSaveCommand command,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            MembershipRow? companyMembership = await ReadMembershipRowAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (companyMembership is null || !companyMembership.IsActive)
                throw new InvalidOperationException("MembershipNotFound");
            if (!command.ActorIsPlatformSuperAdmin && command.ActorUserType == ControlPlaneUserType.Admin
                && companyMembership.UserType != ControlPlaneUserType.Member)
                throw new InvalidOperationException("MembershipTypeExceedsActor");

            CompanyRoleDefinition? role = await ReadRoleAsync(
                connection, transaction, command.CompanyId, command.RoleId, cancellationToken);
            if (role is null || role.ScopeType != ControlPlaneScopeType.Branch || role.CompanyId != command.CompanyId)
                throw new InvalidOperationException("RoleHasWrongScope");
            if (!command.ActorIsPlatformSuperAdmin && role.Permissions.Any(p => !command.ActorCompanyPermissions.Contains(p)))
                throw new InvalidOperationException("RoleExceedsActorPermissions");

            await using (SqlCommand validateBranch = new("""
                SELECT BranchId FROM dbo.Branches WITH(UPDLOCK,HOLDLOCK)
                WHERE BranchId=@branchId AND CompanyId=@companyId AND Status=1 AND IsDeleted=0;
                """, connection, transaction))
            {
                validateBranch.Parameters.AddWithValue("@branchId", command.BranchId);
                validateBranch.Parameters.AddWithValue("@companyId", command.CompanyId);
                if (await validateBranch.ExecuteScalarAsync(cancellationToken) is not Guid)
                    throw new InvalidOperationException("InvalidBranch");
            }

            Guid branchUserId;
            await using (SqlCommand find = new("""
                SELECT TOP(1) BranchUserId FROM dbo.BranchUsers WITH(UPDLOCK,HOLDLOCK)
                WHERE BranchId=@branchId AND UserId=@userId ORDER BY IsDeleted,UpdatedAtUtc DESC;
                """, connection, transaction))
            {
                find.Parameters.AddWithValue("@branchId", command.BranchId);
                find.Parameters.AddWithValue("@userId", command.TargetUserId);
                object? value = await find.ExecuteScalarAsync(cancellationToken);
                branchUserId = value is Guid id ? id : Guid.NewGuid();
            }
            await using (SqlCommand upsert = new("""
                UPDATE dbo.BranchUsers
                SET Status=1,IsPrimaryBranch=@primary,StartsAtUtc=COALESCE(StartsAtUtc,SYSUTCDATETIME()),EndsAtUtc=NULL,
                    IsDeleted=0,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE BranchUserId=@branchUserId;
                IF @@ROWCOUNT=0
                  INSERT dbo.BranchUsers
                    (BranchUserId,BranchId,UserId,Status,IsPrimaryBranch,StartsAtUtc,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
                  VALUES (@branchUserId,@branchId,@userId,1,@primary,SYSUTCDATETIME(),1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
                """, connection, transaction))
            {
                upsert.Parameters.AddWithValue("@branchUserId", branchUserId);
                upsert.Parameters.AddWithValue("@branchId", command.BranchId);
                upsert.Parameters.AddWithValue("@userId", command.TargetUserId);
                upsert.Parameters.AddWithValue("@primary", command.IsPrimary);
                await upsert.ExecuteNonQueryAsync(cancellationToken);
            }
            await ReplaceBranchRoleAsync(connection, transaction, branchUserId, command.RoleId, cancellationToken);
            await AppendAuditAsync(connection, transaction, command.ActorUserId, command.CompanyId, branchUserId,
                "branch.account.assign", command.CorrelationId,
                new { command.TargetUserId, command.BranchId, command.RoleId }, cancellationToken, "BranchUser", command.BranchId);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RevokeBranchMembershipAsync(
        Guid companyId, Guid branchId, Guid targetUserId, Guid actorUserId, Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            Guid? branchUserId;
            await using (SqlCommand find = new("""
                SELECT bu.BranchUserId FROM dbo.BranchUsers bu WITH(UPDLOCK,HOLDLOCK)
                INNER JOIN dbo.Branches b ON b.BranchId=bu.BranchId
                WHERE bu.BranchId=@branchId AND bu.UserId=@userId AND b.CompanyId=@companyId
                  AND bu.Status=1 AND bu.IsDeleted=0;
                """, connection, transaction))
            {
                find.Parameters.AddWithValue("@branchId", branchId);
                find.Parameters.AddWithValue("@userId", targetUserId);
                find.Parameters.AddWithValue("@companyId", companyId);
                object? value = await find.ExecuteScalarAsync(cancellationToken);
                branchUserId = value is Guid id ? id : null;
            }
            if (!branchUserId.HasValue) { await transaction.CommitAsync(cancellationToken); return false; }
            await using SqlCommand revoke = new("""
                UPDATE dbo.UserRoles SET IsDeleted=1,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),
                  Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE BranchUserId=@branchUserId AND IsDeleted=0;
                UPDATE dbo.BranchUsers SET Status=0,IsDeleted=1,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),
                  Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE BranchUserId=@branchUserId;
                """, connection, transaction);
            revoke.Parameters.AddWithValue("@branchUserId", branchUserId.Value);
            await revoke.ExecuteNonQueryAsync(cancellationToken);
            await AppendAuditAsync(connection, transaction, actorUserId, companyId, branchUserId.Value,
                "branch.account.revoke", correlationId, new { targetUserId, branchId }, cancellationToken, "BranchUser", branchId);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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
            if (!command.ActorIsPlatformSuperAdmin && command.UserType == ControlPlaneUserType.Owner)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.OwnerRequiresPlatformSuperAdmin, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.ActorUserType == ControlPlaneUserType.Admin
                && command.UserType != ControlPlaneUserType.Member)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.TargetUserId == command.ActorUserId
                && command.UserType < command.ActorUserType)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.SelfElevation, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.UserType < command.ActorUserType)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && role.Permissions.Any(permission =>
                    !command.ActorCompanyPermissions.Contains(permission)))
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.RoleExceedsActorPermissions, cancellationToken);

            MembershipRow? membership = await ReadMembershipRowAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && membership is { IsActive: true, UserType: ControlPlaneUserType.Owner })
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.OwnerProtected, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.ActorUserType == ControlPlaneUserType.Admin
                && membership is { IsActive: true } && membership.UserType != ControlPlaneUserType.Member)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
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
            if (!command.ActorIsPlatformSuperAdmin && membership.UserType == ControlPlaneUserType.Owner)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.OwnerProtected, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.ActorUserType == ControlPlaneUserType.Admin
                && membership.UserType != ControlPlaneUserType.Member)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
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

    public async Task<CompanyMembershipMutationResult> SetMembershipStatusAsync(
        CompanyMembershipRevokeCommand command, bool isActive, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            CompanyMembershipMutationStatus? validation = await ValidateCompanyAndTargetAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (validation.HasValue) return await RollbackAsync(transaction, validation.Value, cancellationToken);
            MembershipRow? membership = await ReadMembershipRowAsync(
                connection, transaction, command.CompanyId, command.TargetUserId, cancellationToken);
            if (membership is null) return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipNotFound, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && membership.UserType == ControlPlaneUserType.Owner)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.OwnerProtected, cancellationToken);
            if (!command.ActorIsPlatformSuperAdmin && command.ActorUserType == ControlPlaneUserType.Admin
                && membership.UserType != ControlPlaneUserType.Member)
                return await RollbackAsync(transaction, CompanyMembershipMutationStatus.MembershipTypeExceedsActor, cancellationToken);
            byte desired = isActive ? (byte)1 : (byte)2;
            await using SqlCommand update = new("""
                UPDATE dbo.CompanyUsers
                SET Status=@status,EndsAtUtc=NULL,Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
                WHERE CompanyUserId=@companyUserId AND IsDeleted=0 AND Status<>@status;
                """, connection, transaction);
            update.Parameters.AddWithValue("@status", desired);
            update.Parameters.AddWithValue("@companyUserId", membership.CompanyUserId);
            bool changed = await update.ExecuteNonQueryAsync(cancellationToken) == 1;
            if (changed) await AppendAuditAsync(connection, transaction, command.ActorUserId, command.CompanyId,
                membership.CompanyUserId, isActive ? "company.account.activate" : "company.account.suspend",
                command.CorrelationId, new { command.TargetUserId, isActive }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(CompanyMembershipMutationStatus.Success, changed);
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
            SELECT r.RoleId,r.CompanyId,r.RoleCode,r.Name,r.ScopeType,r.IsSystemTemplate,
                   JSON_VALUE(roleAudit.SafeDetailJson,'$.description') Description,p.PermissionCode
            FROM dbo.Roles r{(transaction is null ? string.Empty : " WITH (UPDLOCK,HOLDLOCK)")}
            OUTER APPLY (SELECT TOP(1) a.SafeDetailJson FROM dbo.AuditLogs a
                         WHERE a.EntityType=N'Role' AND a.EntityId=r.RoleId AND a.Outcome=1
                         ORDER BY a.OccurredAtUtc DESC,a.AuditLogId DESC) roleAudit
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId AND rp.IsDeleted=0
            LEFT JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId AND p.Status=1 AND p.IsDeleted=0
              AND EXISTS (
                SELECT 1 FROM dbo.Features f
                INNER JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId
                  AND cf.CompanyId=cu.CompanyId AND cf.IsEnabled=1 AND cf.IsDeleted=0
                  AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
                  AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME())
                WHERE f.ModuleCode=p.ModuleCode AND f.Status=1 AND f.IsDeleted=0)
            WHERE r.Status=1 AND r.IsDeleted=0
              AND (@roleId IS NULL OR r.RoleId=@roleId)
              AND (@includeForeign=1 OR (r.CompanyId IS NULL OR r.CompanyId=@companyId))
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
                    reader.GetBoolean(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6));
                builders.Add(id, builder);
            }
            if (!reader.IsDBNull(7)) builder.Permissions.Add(reader.GetString(7));
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
            WHERE cu.CompanyId=@companyId AND cu.Status IN(1,2) AND cu.IsDeleted=0
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

    private static async Task ReplaceBranchRoleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid branchUserId,
        Guid roleId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            UPDATE dbo.UserRoles
            SET IsDeleted=1,EndsAtUtc=COALESCE(EndsAtUtc,SYSUTCDATETIME()),Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
            WHERE BranchUserId=@branchUserId AND RoleId<>@roleId AND IsDeleted=0;
            UPDATE dbo.UserRoles
            SET IsDeleted=0,StartsAtUtc=COALESCE(StartsAtUtc,SYSUTCDATETIME()),EndsAtUtc=NULL,
                Version=Version+1,UpdatedAtUtc=SYSUTCDATETIME()
            WHERE BranchUserId=@branchUserId AND RoleId=@roleId AND IsDeleted=1;
            IF NOT EXISTS(SELECT 1 FROM dbo.UserRoles WHERE BranchUserId=@branchUserId AND RoleId=@roleId AND IsDeleted=0)
              INSERT dbo.UserRoles
                (UserRoleId,RoleId,CompanyUserId,BranchUserId,StartsAtUtc,Version,CreatedAtUtc,UpdatedAtUtc,IsDeleted)
              VALUES (NEWID(),@roleId,NULL,@branchUserId,SYSUTCDATETIME(),1,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
            """, connection, transaction);
        command.Parameters.AddWithValue("@branchUserId", branchUserId);
        command.Parameters.AddWithValue("@roleId", roleId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyCollection<Guid>> ReadEffectivePermissionIdsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        await using SqlCommand command = new("""
            SELECT DISTINCT p.PermissionId
            FROM dbo.Permissions p WITH(UPDLOCK,HOLDLOCK)
            INNER JOIN dbo.Features f ON f.ModuleCode=p.ModuleCode AND f.Status=1 AND f.IsDeleted=0
            INNER JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId AND cf.CompanyId=@companyId
              AND cf.IsEnabled=1 AND cf.IsDeleted=0
              AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
              AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME())
            WHERE p.Status=1 AND p.IsDeleted=0;
            """, connection, transaction);
        command.Parameters.AddWithValue("@companyId", companyId);
        List<Guid> result = [];
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetGuid(0));
        return result;
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
        CancellationToken cancellationToken,
        string entityType = "CompanyUser",
        Guid? branchId = null)
    {
        await using SqlCommand command = new("""
            INSERT dbo.AuditLogs
                (AuditLogId,OccurredAtUtc,ActorUserId,CompanyId,BranchId,ActionCode,EntityType,EntityId,Outcome,CorrelationId,SafeDetailJson,CreatedAtUtc)
            VALUES
                (NEWID(),SYSUTCDATETIME(),@actorUserId,@companyId,@branchId,@action,@entityType,@entityId,1,@correlationId,@detail,SYSUTCDATETIME());
            """, connection, transaction);
        command.Parameters.AddWithValue("@actorUserId", actorUserId);
        command.Parameters.AddWithValue("@companyId", companyId);
        command.Parameters.Add("@branchId", SqlDbType.UniqueIdentifier).Value = (object?)branchId ?? DBNull.Value;
        command.Parameters.AddWithValue("@action", action);
        command.Parameters.AddWithValue("@entityType", entityType);
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

    private sealed class BranchBuilder(Guid branchId, string branchCode, string name, byte status, bool isAssigned)
    {
        public Dictionary<Guid, RoleBuilder> Roles { get; } = [];

        public CompanyBranchAccess Build() => new(
            branchId, branchCode, name, status, isAssigned,
            Roles.Values.Select(static role => role.Build()).ToArray());
    }

    private sealed class BranchMembershipBuilder(
        Guid branchUserId, Guid companyId, Guid branchId, Guid userId, string displayName,
        string? email, string? phone, byte status)
    {
        public Dictionary<Guid, RoleBuilder> Roles { get; } = [];
        public BranchAccountMembership Build() => new(branchUserId, companyId, branchId, userId, displayName,
            email, phone, status, Roles.Values.Select(static role => role.Build()).ToArray());
    }

    private sealed class RoleBuilder(
        Guid roleId,
        Guid? companyId,
        string roleCode,
        string name,
        ControlPlaneScopeType scopeType,
        bool isSystemTemplate,
        string? description = null)
    {
        public HashSet<string> Permissions { get; } = new(StringComparer.Ordinal);

        public CompanyRoleDefinition Build() => new(
            roleId,
            companyId,
            roleCode,
            name,
            scopeType,
            isSystemTemplate,
            Permissions,
            description);
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
