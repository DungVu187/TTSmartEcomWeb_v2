using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlControlPlaneIdentityReader(IControlDbConnectionFactory connectionFactory) : IControlPlaneIdentityReader
{
    public async Task<ICurrentUserContext?> FindContextByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        // 1. Read User & Primary contacts
        const string userSql = @"
SELECT u.UserId, u.DisplayName, u.AccountType, u.Status, u.SecurityStamp,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 2 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryEmail,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 1 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryPhone
FROM dbo.Users u
WHERE u.UserId = @UserId AND u.IsDeleted = 0;";

        UserHeaderRow? userHeader;
        await using (var cmd = new SqlCommand(userSql, connection))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            userHeader = new UserHeaderRow(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetByte(2),
                reader.GetByte(3),
                reader.GetGuid(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6));
        }

        if (userHeader.Status != (byte)ControlPlaneUserStatus.Active)
        {
            return null;
        }

        bool isPlatformSuperAdmin = userHeader.AccountType == (byte)ControlPlaneAccountType.Platform;

        // 2. Read Active Company Memberships
        const string companySql = @"
SELECT cu.CompanyUserId, cu.CompanyId, c.CompanyCode, c.DisplayName, cu.UserType, cu.Status
FROM dbo.CompanyUsers cu
INNER JOIN dbo.Companies c ON c.CompanyId = cu.CompanyId
WHERE cu.UserId = @UserId
  AND cu.IsDeleted = 0 AND cu.Status = 1
  AND c.IsDeleted = 0 AND c.Status = 1
  AND (cu.StartsAtUtc IS NULL OR cu.StartsAtUtc <= SYSUTCDATETIME())
  AND (cu.EndsAtUtc IS NULL OR cu.EndsAtUtc >= SYSUTCDATETIME());";

        List<CompanyUserRow> companyRows = [];
        await using (var cmd = new SqlCommand(companySql, connection))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                companyRows.Add(new CompanyUserRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetByte(4),
                    reader.GetByte(5)));
            }
        }

        // 3. Read Active Branch Memberships
        const string branchSql = @"
SELECT bu.BranchUserId, b.CompanyId, bu.BranchId, b.BranchCode, b.Name, cu.CompanyUserId, bu.IsPrimaryBranch, bu.Status
FROM dbo.BranchUsers bu
INNER JOIN dbo.Branches b ON b.BranchId = bu.BranchId
INNER JOIN dbo.CompanyUsers cu ON cu.UserId = bu.UserId AND cu.CompanyId = b.CompanyId
INNER JOIN dbo.Companies c ON c.CompanyId = b.CompanyId
WHERE bu.UserId = @UserId
  AND bu.IsDeleted = 0 AND bu.Status = 1
  AND b.IsDeleted = 0 AND b.Status = 1
  AND cu.IsDeleted = 0 AND cu.Status = 1
  AND c.IsDeleted = 0 AND c.Status = 1
  AND (bu.StartsAtUtc IS NULL OR bu.StartsAtUtc <= SYSUTCDATETIME())
  AND (bu.EndsAtUtc IS NULL OR bu.EndsAtUtc >= SYSUTCDATETIME());";

        List<BranchUserRow> branchRows = [];
        await using (var cmd = new SqlCommand(branchSql, connection))
        {
            cmd.Parameters.AddWithValue("@UserId", userId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                branchRows.Add(new BranchUserRow(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetGuid(5),
                    reader.GetBoolean(6),
                    reader.GetByte(7)));
            }
        }

        // 4. Read User Roles & Permissions
        List<RolePermissionRow> rolePermRows = [];
        HashSet<Guid> companyUserIds = companyRows.Select(c => c.CompanyUserId).ToHashSet();
        HashSet<Guid> branchUserIds = branchRows.Select(b => b.BranchUserId).ToHashSet();

        if (companyUserIds.Count > 0 || branchUserIds.Count > 0)
        {
            const string rolePermSql = @"
SELECT ur.UserRoleId, ur.RoleId, ur.CompanyUserId, ur.BranchUserId, r.RoleCode, r.Name, r.ScopeType, r.CompanyId, p.PermissionCode
FROM dbo.UserRoles ur
INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
LEFT JOIN dbo.RolePermissions rp ON rp.RoleId = r.RoleId AND rp.IsDeleted = 0
LEFT JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId AND p.IsDeleted = 0 AND p.Status = 1
  AND EXISTS
  (
    SELECT 1
    FROM dbo.Features f
    INNER JOIN dbo.CompanyFeatureSettings cf ON cf.FeatureId=f.FeatureId
      AND cf.IsEnabled=1 AND cf.IsDeleted=0
      AND (cf.EffectiveFromUtc IS NULL OR cf.EffectiveFromUtc<=SYSUTCDATETIME())
      AND (cf.EffectiveToUtc IS NULL OR cf.EffectiveToUtc>=SYSUTCDATETIME())
    WHERE f.ModuleCode=p.ModuleCode AND f.Status=1 AND f.IsDeleted=0
      AND
      (
        (ur.CompanyUserId IS NOT NULL AND EXISTS
          (SELECT 1 FROM dbo.CompanyUsers ecu
           WHERE ecu.CompanyUserId=ur.CompanyUserId AND ecu.CompanyId=cf.CompanyId))
        OR
        (ur.BranchUserId IS NOT NULL AND EXISTS
          (SELECT 1 FROM dbo.BranchUsers ebu
           INNER JOIN dbo.Branches eb ON eb.BranchId=ebu.BranchId
           WHERE ebu.BranchUserId=ur.BranchUserId AND eb.CompanyId=cf.CompanyId
             AND NOT EXISTS
             (SELECT 1 FROM dbo.BranchFeatureSettings bf
              WHERE bf.BranchId=eb.BranchId AND bf.FeatureId=f.FeatureId
                AND bf.IsDeleted=0 AND
                (bf.IsEnabled=0 OR (bf.EffectiveFromUtc IS NOT NULL AND bf.EffectiveFromUtc>SYSUTCDATETIME())
                  OR (bf.EffectiveToUtc IS NOT NULL AND bf.EffectiveToUtc<SYSUTCDATETIME())))))
      )
  )
WHERE ur.IsDeleted = 0
  AND r.IsDeleted = 0 AND r.Status = 1
  AND (ur.StartsAtUtc IS NULL OR ur.StartsAtUtc <= SYSUTCDATETIME())
  AND (ur.EndsAtUtc IS NULL OR ur.EndsAtUtc >= SYSUTCDATETIME());";

            await using var cmd = new SqlCommand(rolePermSql, connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                Guid? cuId = reader.IsDBNull(2) ? null : reader.GetGuid(2);
                Guid? buId = reader.IsDBNull(3) ? null : reader.GetGuid(3);

                Guid? roleCompanyId = reader.IsDBNull(7) ? null : reader.GetGuid(7);
                CompanyUserRow? companyMembership = cuId.HasValue
                    ? companyRows.FirstOrDefault(row => row.CompanyUserId == cuId.Value)
                    : null;
                BranchUserRow? branchMembership = buId.HasValue
                    ? branchRows.FirstOrDefault(row => row.BranchUserId == buId.Value)
                    : null;

                bool validCompanyRole = companyMembership is not null
                    && reader.GetByte(6) == (byte)ControlPlaneScopeType.Company
                    && (!roleCompanyId.HasValue || roleCompanyId.Value == companyMembership.CompanyId);
                bool validBranchRole = branchMembership is not null
                    && reader.GetByte(6) == (byte)ControlPlaneScopeType.Branch
                    && (!roleCompanyId.HasValue || roleCompanyId.Value == branchMembership.CompanyId);

                if (validCompanyRole || validBranchRole)
                {
                    rolePermRows.Add(new RolePermissionRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        cuId,
                        buId,
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetByte(6),
                        roleCompanyId,
                        reader.IsDBNull(8) ? null : reader.GetString(8)));
                }
            }
        }

        // 5. Aggregate Company Memberships
        List<CompanyMembershipContext> companyMemberships = [];
        HashSet<string> allRoles = new(StringComparer.Ordinal);
        HashSet<string> allPermissions = new(StringComparer.Ordinal);

        foreach (CompanyUserRow cu in companyRows)
        {
            List<string> cRoles = rolePermRows
                .Where(r => r.CompanyUserId == cu.CompanyUserId)
                .Select(r => r.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Default role name by user type if no explicit role assigned
            if (cRoles.Count == 0)
            {
                if (cu.UserType == (byte)ControlPlaneUserType.Owner) cRoles.Add("company_owner");
                else if (cu.UserType == (byte)ControlPlaneUserType.Admin) cRoles.Add("company_admin");
                else cRoles.Add("company_member");
            }

            HashSet<string> cPerms = rolePermRows
                .Where(r => r.CompanyUserId == cu.CompanyUserId && !string.IsNullOrWhiteSpace(r.PermissionCode))
                .Select(r => r.PermissionCode!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string r in cRoles) allRoles.Add(r);
            foreach (string p in cPerms) allPermissions.Add(p);

            companyMemberships.Add(new CompanyMembershipContext(
                cu.CompanyId,
                cu.CompanyCode,
                cu.DisplayName,
                cu.CompanyUserId,
                cu.UserType,
                cRoles,
                cPerms));
        }

        // 6. Aggregate Branch Memberships
        List<BranchMembershipContext> branchMemberships = [];
        foreach (BranchUserRow bu in branchRows)
        {
            List<string> bRoles = rolePermRows
                .Where(r => r.BranchUserId == bu.BranchUserId)
                .Select(r => r.RoleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (bRoles.Count == 0)
            {
                bRoles.Add("branch_member");
            }

            HashSet<string> bPerms = rolePermRows
                .Where(r => r.BranchUserId == bu.BranchUserId && !string.IsNullOrWhiteSpace(r.PermissionCode))
                .Select(r => r.PermissionCode!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (string r in bRoles) allRoles.Add(r);
            foreach (string p in bPerms) allPermissions.Add(p);

            branchMemberships.Add(new BranchMembershipContext(
                bu.CompanyId,
                bu.BranchId,
                bu.BranchCode,
                bu.BranchName,
                bu.BranchUserId,
                bu.IsPrimaryBranch,
                bRoles,
                bPerms));
        }

        if (isPlatformSuperAdmin)
        {
            allRoles.Add(SystemRoles.SuperAdmin);
            foreach (string p in SystemPermissions.All)
            {
                allPermissions.Add(p);
            }
        }

        return new CurrentUserContext(
            userId: userHeader.UserId,
            isAuthenticated: true,
            isPlatformSuperAdmin: isPlatformSuperAdmin,
            displayName: userHeader.DisplayName,
            email: userHeader.PrimaryEmail,
            phone: userHeader.PrimaryPhone,
            companyMemberships: companyMemberships,
            activeCompanyId: null,
            branchMemberships: branchMemberships,
            activeBranchId: null,
            roles: allRoles.ToList(),
            permissions: allPermissions,
            isControlPlaneIdentity: true);
    }

    public async Task<ICurrentUserContext?> FindContextByLoginAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        string normalized = identifier.Trim().ToUpperInvariant();

        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP(1) UserId
FROM dbo.UserLogins
WHERE NormalizedValue = @Normalized
  AND IsDeleted = 0;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Normalized", normalized);
        object? result = await cmd.ExecuteScalarAsync(cancellationToken);

        if (result is null || result == DBNull.Value || result is not Guid userId)
        {
            return null;
        }

        return await FindContextByIdAsync(userId, cancellationToken);
    }

    private sealed record UserHeaderRow(
        Guid UserId,
        string DisplayName,
        byte AccountType,
        byte Status,
        Guid SecurityStamp,
        string? PrimaryEmail,
        string? PrimaryPhone);

    private sealed record CompanyUserRow(
        Guid CompanyUserId,
        Guid CompanyId,
        string CompanyCode,
        string DisplayName,
        byte UserType,
        byte Status);

    private sealed record BranchUserRow(
        Guid BranchUserId,
        Guid CompanyId,
        Guid BranchId,
        string BranchCode,
        string BranchName,
        Guid CompanyUserId,
        bool IsPrimaryBranch,
        byte Status);

    private sealed record RolePermissionRow(
        Guid UserRoleId,
        Guid RoleId,
        Guid? CompanyUserId,
        Guid? BranchUserId,
        string RoleCode,
        string RoleName,
        byte ScopeType,
        Guid? RoleCompanyId,
        string? PermissionCode);
}
