using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using TTSmartEcom.Application.Abstractions.Authentication;
using TTSmartEcom.Application.Abstractions.Security;
using TTSmartEcom.Application.Security;
using TTSmartEcom.Domain.Security;
using TTSmartEcom.Infrastructure.SqlServer;
using TTSmartEcom.Infrastructure.SqlServer.Security;
using Xunit.Sdk;

namespace TTSmartEcom.IntegrationTests;

public sealed class ControlPlaneIdentityIntegrationTests
{
    private const string ControlPlaneSchema = """
        CREATE TABLE dbo.Users
        (
            UserId uniqueidentifier NOT NULL PRIMARY KEY,
            DisplayName nvarchar(200) NOT NULL,
            AccountType tinyint NOT NULL DEFAULT (1),
            Status tinyint NOT NULL DEFAULT (1),
            SecurityStamp uniqueidentifier NOT NULL DEFAULT (NEWID()),
            LastLoginAtUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.UserLogins
        (
            UserLoginId uniqueidentifier NOT NULL PRIMARY KEY,
            UserId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Users(UserId),
            IdentifierType tinyint NOT NULL,
            DisplayValue nvarchar(320) NOT NULL,
            NormalizedValue nvarchar(320) NOT NULL,
            IsPrimary bit NOT NULL DEFAULT (0),
            IsVerified bit NOT NULL DEFAULT (0),
            VerifiedAtUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.UserPasswords
        (
            UserPasswordId uniqueidentifier NOT NULL PRIMARY KEY,
            UserId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Users(UserId),
            PasswordHash nvarchar(500) NOT NULL,
            HashAlgorithm nvarchar(50) NOT NULL DEFAULT ('BCrypt'),
            HashVersion int NOT NULL DEFAULT (1),
            MustRehash bit NOT NULL DEFAULT (0),
            MustChangePassword bit NOT NULL DEFAULT (0),
            FailedAttemptCount int NOT NULL DEFAULT (0),
            LockedUntilUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME())
        );

        CREATE TABLE dbo.Companies
        (
            CompanyId uniqueidentifier NOT NULL PRIMARY KEY,
            CompanyCode nvarchar(64) NOT NULL,
            NormalizedCompanyCode nvarchar(64) NOT NULL,
            DisplayName nvarchar(300) NOT NULL,
            Status tinyint NOT NULL DEFAULT (1),
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.Branches
        (
            BranchId uniqueidentifier NOT NULL PRIMARY KEY,
            CompanyId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Companies(CompanyId),
            BranchCode nvarchar(64) NOT NULL,
            NormalizedBranchCode nvarchar(64) NOT NULL,
            Name nvarchar(300) NOT NULL,
            IsHeadOffice bit NOT NULL DEFAULT (0),
            Status tinyint NOT NULL DEFAULT (1),
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.CompanyUsers
        (
            CompanyUserId uniqueidentifier NOT NULL PRIMARY KEY,
            CompanyId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Companies(CompanyId),
            UserId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Users(UserId),
            UserType tinyint NOT NULL DEFAULT (1),
            Status tinyint NOT NULL DEFAULT (1),
            StartsAtUtc datetime2(7) NULL,
            EndsAtUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.BranchUsers
        (
            BranchUserId uniqueidentifier NOT NULL PRIMARY KEY,
            BranchId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Branches(BranchId),
            UserId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Users(UserId),
            Status tinyint NOT NULL DEFAULT (1),
            IsPrimaryBranch bit NOT NULL DEFAULT (0),
            StartsAtUtc datetime2(7) NULL,
            EndsAtUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.Roles
        (
            RoleId uniqueidentifier NOT NULL PRIMARY KEY,
            CompanyId uniqueidentifier NULL FOREIGN KEY REFERENCES dbo.Companies(CompanyId),
            RoleCode nvarchar(100) NOT NULL,
            NormalizedRoleCode nvarchar(100) NOT NULL,
            Name nvarchar(200) NOT NULL,
            ScopeType tinyint NOT NULL,
            IsSystemTemplate bit NOT NULL DEFAULT (0),
            Status tinyint NOT NULL DEFAULT (1),
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.Permissions
        (
            PermissionId uniqueidentifier NOT NULL PRIMARY KEY,
            PermissionCode nvarchar(150) NOT NULL,
            NormalizedPermissionCode nvarchar(150) NOT NULL,
            Name nvarchar(200) NOT NULL,
            ModuleCode nvarchar(100) NOT NULL,
            Status tinyint NOT NULL DEFAULT (1),
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.RolePermissions
        (
            RolePermissionId uniqueidentifier NOT NULL PRIMARY KEY,
            RoleId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Roles(RoleId),
            PermissionId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Permissions(PermissionId),
            GrantedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            GrantedByUserId uniqueidentifier NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );

        CREATE TABLE dbo.UserRoles
        (
            UserRoleId uniqueidentifier NOT NULL PRIMARY KEY,
            RoleId uniqueidentifier NOT NULL FOREIGN KEY REFERENCES dbo.Roles(RoleId),
            CompanyUserId uniqueidentifier NULL FOREIGN KEY REFERENCES dbo.CompanyUsers(CompanyUserId),
            BranchUserId uniqueidentifier NULL FOREIGN KEY REFERENCES dbo.BranchUsers(BranchUserId),
            StartsAtUtc datetime2(7) NULL,
            EndsAtUtc datetime2(7) NULL,
            Version bigint NOT NULL DEFAULT (1),
            CreatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(7) NOT NULL DEFAULT (SYSUTCDATETIME()),
            IsDeleted bit NOT NULL DEFAULT (0)
        );
        """;

    [Fact]
    public async Task ControlPlaneIdentityReader_ResolvesSuperAdminAndCompanyBoundariesAccurately()
    {
        string? configuredConnection = Environment.GetEnvironmentVariable("TTSMART_SQL_INTEGRATION_CONNECTION");
        if (string.IsNullOrWhiteSpace(configuredConnection))
        {
            throw SkipException.ForSkip("Cần TTSMART_SQL_INTEGRATION_CONNECTION trỏ SQL Server local dành cho test cô lập.");
        }

        string databaseName = $"TTSmartEcomV2ControlPlaneIntegration_{Guid.NewGuid():N}";
        SqlConnectionStringBuilder master = new(configuredConnection) { InitialCatalog = "master" };
        SqlConnectionStringBuilder test = new(configuredConnection) { InitialCatalog = databaseName };

        try
        {
            await ExecuteAsync(master.ConnectionString, $"CREATE DATABASE [{databaseName}];");
            await ExecuteAsync(test.ConnectionString, ControlPlaneSchema);

            Guid superAdminId = Guid.NewGuid();
            Guid companyAdminId = Guid.NewGuid();
            Guid employeeHnId = Guid.NewGuid();
            Guid companyA = Guid.NewGuid();
            Guid companyB = Guid.NewGuid();
            Guid branchA_HN = Guid.NewGuid();
            Guid branchA_SG = Guid.NewGuid();
            Guid branchB_HN = Guid.NewGuid();
            Guid cuAdminA = Guid.NewGuid();
            Guid cuEmpHn = Guid.NewGuid();
            Guid buEmpHn = Guid.NewGuid();
            Guid roleAdmin = Guid.NewGuid();
            Guid roleStaff = Guid.NewGuid();
            Guid permProdView = Guid.NewGuid();
            Guid permOrderCreate = Guid.NewGuid();

            string bcryptHash = global::BCrypt.Net.BCrypt.HashPassword("AdminPassword123!", 10);

            // 1. Seed Companies & Branches
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Companies(CompanyId, CompanyCode, NormalizedCompanyCode, DisplayName, Status)
                VALUES('{companyA}', N'ABC', N'ABC', N'Cong ty ABC', 1),
                      ('{companyB}', N'XYZ', N'XYZ', N'Cong ty XYZ', 1);

                INSERT dbo.Branches(BranchId, CompanyId, BranchCode, NormalizedBranchCode, Name, IsHeadOffice, Status)
                VALUES('{branchA_HN}', '{companyA}', N'HN', N'HN', N'Chi nhanh Ha Noi', 1, 1),
                      ('{branchA_SG}', '{companyA}', N'SG', N'SG', N'Chi nhanh Sai Gon', 0, 1),
                      ('{branchB_HN}', '{companyB}', N'HN', N'HN', N'Chi nhanh XYZ Ha Noi', 1, 1);
                """);

            // 2. Seed Permissions & Roles
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Permissions(PermissionId, PermissionCode, NormalizedPermissionCode, Name, ModuleCode, Status)
                VALUES('{permProdView}', N'product.view', N'PRODUCT.VIEW', N'Xem san pham', N'product', 1),
                      ('{permOrderCreate}', N'order.create', N'ORDER.CREATE', N'Tao don', N'order', 1);

                INSERT dbo.Roles(RoleId, CompanyId, RoleCode, NormalizedRoleCode, Name, ScopeType, IsSystemTemplate, Status)
                VALUES('{roleAdmin}', NULL, N'company_admin', N'COMPANY_ADMIN', N'Admin Cong ty', 1, 1, 1),
                      ('{roleStaff}', NULL, N'branch_staff', N'BRANCH_STAFF', N'Nhan vien Chi nhanh', 2, 1, 1);

                INSERT dbo.RolePermissions(RolePermissionId, RoleId, PermissionId)
                VALUES(NEWID(), '{roleAdmin}', '{permProdView}'),
                      (NEWID(), '{roleStaff}', '{permOrderCreate}');
                """);

            // 3. Seed Users & Logins & Passwords
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.Users(UserId, DisplayName, AccountType, Status)
                VALUES('{superAdminId}', N'Super Admin', 1, 1),
                      ('{companyAdminId}', N'Admin ABC', 2, 1),
                      ('{employeeHnId}', N'Employee HN', 3, 1);

                INSERT dbo.UserLogins(UserLoginId, UserId, IdentifierType, DisplayValue, NormalizedValue, IsPrimary, IsVerified)
                VALUES(NEWID(), '{superAdminId}', 2, N'superadmin@ttsmart.com.vn', N'SUPERADMIN@TTSMART.COM.VN', 1, 1),
                      (NEWID(), '{companyAdminId}', 1, N'0901234567', N'0901234567', 1, 1),
                      (NEWID(), '{employeeHnId}', 1, N'0909876543', N'0909876543', 1, 1);

                INSERT dbo.UserPasswords(UserPasswordId, UserId, PasswordHash, HashAlgorithm, HashVersion)
                VALUES(NEWID(), '{superAdminId}', N'{bcryptHash}', N'BCrypt', 1),
                      (NEWID(), '{companyAdminId}', N'{bcryptHash}', N'BCrypt', 1),
                      (NEWID(), '{employeeHnId}', N'{bcryptHash}', N'BCrypt', 1);
                """);

            // 4. Seed Memberships & UserRoles
            await ExecuteAsync(test.ConnectionString, $"""
                INSERT dbo.CompanyUsers(CompanyUserId, CompanyId, UserId, UserType, Status)
                VALUES('{cuAdminA}', '{companyA}', '{companyAdminId}', 2, 1),
                      ('{cuEmpHn}', '{companyA}', '{employeeHnId}', 3, 1);

                INSERT dbo.BranchUsers(BranchUserId, BranchId, UserId, Status, IsPrimaryBranch)
                VALUES('{buEmpHn}', '{branchA_HN}', '{employeeHnId}', 1, 1);

                INSERT dbo.UserRoles(UserRoleId, RoleId, CompanyUserId, BranchUserId)
                VALUES(NEWID(), '{roleAdmin}', '{cuAdminA}', NULL),
                      (NEWID(), '{roleStaff}', NULL, '{buEmpHn}');
                """);

            TestControlDbFactory factory = new(test.ConnectionString);
            SqlControlPlaneIdentityReader reader = new(factory);
            SqlControlPlaneUserRepository userRepo = new(factory);
            IPasswordHashCompatibilityVerifier passwordVerifier = new SqlPasswordHashCompatibilityVerifier();
            ControlPlaneAuthenticationService authService = new(userRepo, reader, passwordVerifier);
            AccessScopeService scopeService = new();

            // Test 1: SuperAdmin Resolution
            ICurrentUserContext? superAdminCtx = await reader.FindContextByIdAsync(superAdminId, CancellationToken.None);
            Assert.NotNull(superAdminCtx);
            Assert.True(superAdminCtx.IsPlatformSuperAdmin);
            Assert.True(scopeService.CanAccessCompany(superAdminCtx, companyA));
            Assert.True(scopeService.CanAccessCompany(superAdminCtx, companyB));
            Assert.True(scopeService.CanAccessBranch(superAdminCtx, branchA_HN));
            Assert.True(scopeService.CanAccessBranch(superAdminCtx, branchB_HN));

            // Test 2: Company Admin Resolution
            ICurrentUserContext? adminCtx = await reader.FindContextByLoginAsync("0901234567", CancellationToken.None);
            Assert.NotNull(adminCtx);
            Assert.False(adminCtx.IsPlatformSuperAdmin);
            Assert.True(scopeService.CanAccessCompany(adminCtx, companyA));
            Assert.False(scopeService.CanAccessCompany(adminCtx, companyB));
            Assert.True(scopeService.HasCompanyPermission(adminCtx, companyA, "product.view"));
            Assert.False(scopeService.HasCompanyPermission(adminCtx, companyB, "product.view"));

            // Test 3: Branch Employee Resolution
            ICurrentUserContext? empCtx = await reader.FindContextByLoginAsync("0909876543", CancellationToken.None);
            Assert.NotNull(empCtx);
            Assert.True(scopeService.CanAccessBranch(empCtx, branchA_HN));
            Assert.False(scopeService.CanAccessBranch(empCtx, branchA_SG));
            Assert.False(scopeService.CanAccessBranch(empCtx, branchB_HN));
            Assert.True(scopeService.HasBranchPermission(empCtx, branchA_HN, "order.create"));
            Assert.False(scopeService.HasBranchPermission(empCtx, branchA_SG, "order.create"));

            // Test 4: Authentication Service
            ControlPlaneAuthResult successAuth = await authService.AuthenticateAsync("0901234567", "AdminPassword123!", CancellationToken.None);
            Assert.Equal(ControlPlaneAuthStatus.Success, successAuth.Status);
            Assert.NotNull(successAuth.UserContext);

            ControlPlaneAuthResult failedAuth = await authService.AuthenticateAsync("0901234567", "WrongPassword", CancellationToken.None);
            Assert.Equal(ControlPlaneAuthStatus.InvalidCredentials, failedAuth.Status);
        }
        finally
        {
            await ExecuteAsync(master.ConnectionString, $"""
                IF DB_ID(N'{databaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{databaseName}];
                END;
                """);
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using SqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using SqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestControlDbFactory(string connectionString) : IControlDbConnectionFactory
    {
        public SqlConnection Create() => new(connectionString);
    }
}
