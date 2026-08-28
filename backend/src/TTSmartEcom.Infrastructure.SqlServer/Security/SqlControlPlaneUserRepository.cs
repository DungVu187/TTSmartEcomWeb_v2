using Microsoft.Data.SqlClient;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Domain.Security;

namespace TTSmartEcom.Infrastructure.SqlServer.Security;

public sealed class SqlControlPlaneUserRepository(IControlDbConnectionFactory connectionFactory) : IControlPlaneUserRepository
{
    public async Task<ControlPlaneUserRecord?> FindByLoginAsync(string identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        string normalized = identifier.Trim().ToUpperInvariant();

        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP(1) u.UserId, u.DisplayName, u.AccountType, u.Status, u.SecurityStamp,
       p.PasswordHash, p.HashAlgorithm, p.HashVersion, p.MustChangePassword, p.FailedAttemptCount, p.LockedUntilUtc,
       u.LastLoginAtUtc,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 2 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryEmail,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 1 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryPhone
FROM dbo.UserLogins l
INNER JOIN dbo.Users u ON u.UserId = l.UserId
INNER JOIN dbo.UserPasswords p ON p.UserId = u.UserId
WHERE l.NormalizedValue = @Normalized
  AND l.IsDeleted = 0
  AND u.IsDeleted = 0;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Normalized", normalized);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ControlPlaneUserRecord(
            UserId: reader.GetGuid(0),
            DisplayName: reader.GetString(1),
            AccountType: (ControlPlaneAccountType)reader.GetByte(2),
            Status: (ControlPlaneUserStatus)reader.GetByte(3),
            SecurityStamp: reader.GetGuid(4),
            PrimaryEmail: reader.IsDBNull(12) ? null : reader.GetString(12),
            PrimaryPhone: reader.IsDBNull(13) ? null : reader.GetString(13),
            PasswordHash: reader.GetString(5),
            HashAlgorithm: reader.GetString(6),
            HashVersion: reader.GetInt32(7),
            MustChangePassword: reader.GetBoolean(8),
            FailedAttemptCount: reader.GetInt32(9),
            LockedUntilUtc: reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero),
            LastLoginAtUtc: reader.IsDBNull(11) ? null : new DateTimeOffset(reader.GetDateTime(11), TimeSpan.Zero));
    }

    public async Task<ControlPlaneUserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT TOP(1) u.UserId, u.DisplayName, u.AccountType, u.Status, u.SecurityStamp,
       p.PasswordHash, p.HashAlgorithm, p.HashVersion, p.MustChangePassword, p.FailedAttemptCount, p.LockedUntilUtc,
       u.LastLoginAtUtc,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 2 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryEmail,
       (SELECT TOP(1) ul.DisplayValue FROM dbo.UserLogins ul WHERE ul.UserId = u.UserId AND ul.IdentifierType = 1 AND ul.IsDeleted = 0 ORDER BY ul.IsPrimary DESC) AS PrimaryPhone
FROM dbo.Users u
INNER JOIN dbo.UserPasswords p ON p.UserId = u.UserId
WHERE u.UserId = @UserId
  AND u.IsDeleted = 0;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ControlPlaneUserRecord(
            UserId: reader.GetGuid(0),
            DisplayName: reader.GetString(1),
            AccountType: (ControlPlaneAccountType)reader.GetByte(2),
            Status: (ControlPlaneUserStatus)reader.GetByte(3),
            SecurityStamp: reader.GetGuid(4),
            PrimaryEmail: reader.IsDBNull(12) ? null : reader.GetString(12),
            PrimaryPhone: reader.IsDBNull(13) ? null : reader.GetString(13),
            PasswordHash: reader.GetString(5),
            HashAlgorithm: reader.GetString(6),
            HashVersion: reader.GetInt32(7),
            MustChangePassword: reader.GetBoolean(8),
            FailedAttemptCount: reader.GetInt32(9),
            LockedUntilUtc: reader.IsDBNull(10) ? null : new DateTimeOffset(reader.GetDateTime(10), TimeSpan.Zero),
            LastLoginAtUtc: reader.IsDBNull(11) ? null : new DateTimeOffset(reader.GetDateTime(11), TimeSpan.Zero));
    }

    public async Task<bool> RecordSuccessfulLoginAsync(Guid userId, DateTimeOffset loginTime, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
BEGIN TRANSACTION;
UPDATE dbo.Users
SET LastLoginAtUtc = @LoginTime,
    UpdatedAtUtc = SYSUTCDATETIME(),
    Version = Version + 1
WHERE UserId = @UserId;

UPDATE dbo.UserPasswords
SET FailedAttemptCount = 0,
    LockedUntilUtc = NULL,
    UpdatedAtUtc = SYSUTCDATETIME(),
    Version = Version + 1
WHERE UserId = @UserId;
COMMIT TRANSACTION;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@LoginTime", loginTime.UtcDateTime);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RecordFailedLoginAsync(Guid userId, int maxFailedAttempts, TimeSpan lockoutDuration, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
UPDATE dbo.UserPasswords
SET FailedAttemptCount = FailedAttemptCount + 1,
    LockedUntilUtc = CASE WHEN FailedAttemptCount + 1 >= @MaxAttempts THEN DATEADD(second, @LockoutSeconds, SYSUTCDATETIME()) ELSE LockedUntilUtc END,
    UpdatedAtUtc = SYSUTCDATETIME(),
    Version = Version + 1
WHERE UserId = @UserId;";

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        cmd.Parameters.AddWithValue("@MaxAttempts", maxFailedAttempts);
        cmd.Parameters.AddWithValue("@LockoutSeconds", (int)lockoutDuration.TotalSeconds);
        return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<IReadOnlyList<ControlPlaneCompanyMembership>> GetCompanyMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT cu.CompanyUserId, cu.CompanyId, c.CompanyCode, c.DisplayName, cu.UserType, cu.Status, cu.StartsAtUtc, cu.EndsAtUtc
FROM dbo.CompanyUsers cu
INNER JOIN dbo.Companies c ON c.CompanyId = cu.CompanyId
WHERE cu.UserId = @UserId
  AND cu.IsDeleted = 0 AND cu.Status = 1
  AND c.IsDeleted = 0 AND c.Status = 1
  AND (cu.StartsAtUtc IS NULL OR cu.StartsAtUtc <= SYSUTCDATETIME())
  AND (cu.EndsAtUtc IS NULL OR cu.EndsAtUtc >= SYSUTCDATETIME());";

        List<ControlPlaneCompanyMembership> list = [];
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new ControlPlaneCompanyMembership(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                (ControlPlaneUserType)reader.GetByte(4),
                reader.GetByte(5),
                reader.IsDBNull(6) ? null : new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
                reader.IsDBNull(7) ? null : new DateTimeOffset(reader.GetDateTime(7), TimeSpan.Zero),
                []));
        }

        return list;
    }

    public async Task<IReadOnlyList<ControlPlaneBranchMembership>> GetBranchMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
SELECT bu.BranchUserId, b.CompanyId, bu.BranchId, b.BranchCode, b.Name, cu.CompanyUserId, bu.IsPrimaryBranch, bu.Status, bu.StartsAtUtc, bu.EndsAtUtc
FROM dbo.BranchUsers bu
INNER JOIN dbo.Branches b ON b.BranchId = bu.BranchId
INNER JOIN dbo.CompanyUsers cu ON cu.UserId = bu.UserId AND cu.CompanyId = b.CompanyId
WHERE bu.UserId = @UserId
  AND bu.IsDeleted = 0 AND bu.Status = 1
  AND b.IsDeleted = 0 AND b.Status = 1
  AND cu.IsDeleted = 0 AND cu.Status = 1
  AND (bu.StartsAtUtc IS NULL OR bu.StartsAtUtc <= SYSUTCDATETIME())
  AND (bu.EndsAtUtc IS NULL OR bu.EndsAtUtc >= SYSUTCDATETIME());";

        List<ControlPlaneBranchMembership> list = [];
        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", userId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new ControlPlaneBranchMembership(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetGuid(5),
                reader.GetBoolean(6),
                reader.GetByte(7),
                reader.IsDBNull(8) ? null : new DateTimeOffset(reader.GetDateTime(8), TimeSpan.Zero),
                reader.IsDBNull(9) ? null : new DateTimeOffset(reader.GetDateTime(9), TimeSpan.Zero),
                []));
        }

        return list;
    }
}
