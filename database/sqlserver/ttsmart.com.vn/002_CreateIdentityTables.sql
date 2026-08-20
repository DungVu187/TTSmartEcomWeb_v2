/* Migration 002: identity, membership, roles và permissions. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [ttsmart.com.vn];
GO

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 2)
BEGIN
    PRINT N'Migration 002 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 1)
    THROW 51002, N'Chưa áp dụng migration 001.', 1;


BEGIN TRANSACTION;

CREATE TABLE dbo.Users
(
    UserId uniqueidentifier NOT NULL,
    DisplayName nvarchar(200) NOT NULL,
    AccountType tinyint NOT NULL CONSTRAINT DF_Users_AccountType DEFAULT (1),
    Status tinyint NOT NULL CONSTRAINT DF_Users_Status DEFAULT (0),
    SecurityStamp uniqueidentifier NOT NULL,
    LastLoginAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Users_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Users_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT CK_Users_DisplayName CHECK (LEN(LTRIM(RTRIM(DisplayName))) > 0),
    CONSTRAINT CK_Users_AccountType CHECK (AccountType IN (1, 2, 3)),
    CONSTRAINT CK_Users_Status CHECK (Status IN (0, 1, 2, 3)),
    CONSTRAINT CK_Users_Version CHECK (Version > 0)
);

CREATE TABLE dbo.UserLogins
(
    UserLoginId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    IdentifierType tinyint NOT NULL,
    DisplayValue nvarchar(320) NOT NULL,
    NormalizedValue nvarchar(320) NOT NULL,
    IsPrimary bit NOT NULL CONSTRAINT DF_UserLogins_IsPrimary DEFAULT (0),
    IsVerified bit NOT NULL CONSTRAINT DF_UserLogins_IsVerified DEFAULT (0),
    VerifiedAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_UserLogins_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserLogins_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserLogins_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_UserLogins_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_UserLogins PRIMARY KEY CLUSTERED (UserLoginId),
    CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_UserLogins_IdentifierType CHECK (IdentifierType IN (1, 2, 3)),
    CONSTRAINT CK_UserLogins_DisplayValue CHECK (LEN(LTRIM(RTRIM(DisplayValue))) > 0),
    CONSTRAINT CK_UserLogins_NormalizedValue CHECK (NormalizedValue = UPPER(LTRIM(RTRIM(DisplayValue)))),
    CONSTRAINT CK_UserLogins_VerifiedAtUtc CHECK (IsVerified = 0 OR VerifiedAtUtc IS NOT NULL),
    CONSTRAINT CK_UserLogins_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_UserLogins_Identifier_NormalizedValue_Active ON dbo.UserLogins (IdentifierType, NormalizedValue) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_UserLogins_User_Primary_Active ON dbo.UserLogins (UserId) WHERE IsPrimary = 1 AND IsDeleted = 0;

CREATE TABLE dbo.UserPasswords
(
    UserPasswordId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    PasswordHash nvarchar(500) NOT NULL,
    HashAlgorithm nvarchar(50) NOT NULL,
    HashVersion int NOT NULL,
    MustRehash bit NOT NULL CONSTRAINT DF_UserPasswords_MustRehash DEFAULT (0),
    MustChangePassword bit NOT NULL CONSTRAINT DF_UserPasswords_MustChangePassword DEFAULT (0),
    FailedAttemptCount int NOT NULL CONSTRAINT DF_UserPasswords_FailedAttemptCount DEFAULT (0),
    LockedUntilUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_UserPasswords_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserPasswords_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserPasswords_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_UserPasswords PRIMARY KEY CLUSTERED (UserPasswordId),
    CONSTRAINT FK_UserPasswords_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT UQ_UserPasswords_User UNIQUE (UserId),
    CONSTRAINT CK_UserPasswords_PasswordHash CHECK (LEN(LTRIM(RTRIM(PasswordHash))) > 0),
    CONSTRAINT CK_UserPasswords_HashAlgorithm CHECK (LEN(LTRIM(RTRIM(HashAlgorithm))) > 0),
    CONSTRAINT CK_UserPasswords_HashVersion CHECK (HashVersion > 0),
    CONSTRAINT CK_UserPasswords_FailedAttemptCount CHECK (FailedAttemptCount >= 0),
    CONSTRAINT CK_UserPasswords_Version CHECK (Version > 0)
);

CREATE TABLE dbo.CompanyUsers
(
    CompanyUserId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    UserType tinyint NOT NULL CONSTRAINT DF_CompanyUsers_UserType DEFAULT (1),
    Status tinyint NOT NULL CONSTRAINT DF_CompanyUsers_Status DEFAULT (1),
    StartsAtUtc datetime2(7) NULL,
    EndsAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_CompanyUsers_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyUsers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_CompanyUsers_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_CompanyUsers PRIMARY KEY CLUSTERED (CompanyUserId),
    CONSTRAINT FK_CompanyUsers_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_CompanyUsers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_CompanyUsers_UserType CHECK (UserType IN (1, 2, 3)),
    CONSTRAINT CK_CompanyUsers_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_CompanyUsers_Period CHECK (EndsAtUtc IS NULL OR StartsAtUtc IS NULL OR EndsAtUtc >= StartsAtUtc),
    CONSTRAINT CK_CompanyUsers_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_CompanyUsers_Company_User_Active ON dbo.CompanyUsers (CompanyId, UserId) WHERE IsDeleted = 0 AND Status = 1;
CREATE INDEX IX_CompanyUsers_User_Status ON dbo.CompanyUsers (UserId, Status, IsDeleted);

CREATE TABLE dbo.BranchUsers
(
    BranchUserId uniqueidentifier NOT NULL,
    BranchId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_BranchUsers_Status DEFAULT (1),
    IsPrimaryBranch bit NOT NULL CONSTRAINT DF_BranchUsers_IsPrimaryBranch DEFAULT (0),
    StartsAtUtc datetime2(7) NULL,
    EndsAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_BranchUsers_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchUsers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_BranchUsers_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_BranchUsers PRIMARY KEY CLUSTERED (BranchUserId),
    CONSTRAINT FK_BranchUsers_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (BranchId),
    CONSTRAINT FK_BranchUsers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_BranchUsers_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_BranchUsers_Period CHECK (EndsAtUtc IS NULL OR StartsAtUtc IS NULL OR EndsAtUtc >= StartsAtUtc),
    CONSTRAINT CK_BranchUsers_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_BranchUsers_Branch_User_Active ON dbo.BranchUsers (BranchId, UserId) WHERE IsDeleted = 0 AND Status = 1;
CREATE UNIQUE INDEX UX_BranchUsers_User_Primary_Active ON dbo.BranchUsers (UserId) WHERE IsPrimaryBranch = 1 AND IsDeleted = 0 AND Status = 1;

CREATE TABLE dbo.Roles
(
    RoleId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NULL,
    RoleCode nvarchar(100) NOT NULL,
    NormalizedRoleCode nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    ScopeType tinyint NOT NULL,
    IsSystemTemplate bit NOT NULL CONSTRAINT DF_Roles_IsSystemTemplate DEFAULT (0),
    Status tinyint NOT NULL CONSTRAINT DF_Roles_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Roles_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Roles_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Roles_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT FK_Roles_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT CK_Roles_NormalizedRoleCode CHECK (NormalizedRoleCode = UPPER(LTRIM(RTRIM(RoleCode)))),
    CONSTRAINT CK_Roles_ScopeType CHECK (ScopeType IN (1, 2)),
    CONSTRAINT CK_Roles_SystemTemplate CHECK ((IsSystemTemplate = 1 AND CompanyId IS NULL) OR (IsSystemTemplate = 0 AND CompanyId IS NOT NULL)),
    CONSTRAINT CK_Roles_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Roles_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Roles_System_NormalizedCode ON dbo.Roles (ScopeType, NormalizedRoleCode) WHERE CompanyId IS NULL AND IsDeleted = 0;
CREATE UNIQUE INDEX UX_Roles_Company_NormalizedCode ON dbo.Roles (CompanyId, ScopeType, NormalizedRoleCode) WHERE CompanyId IS NOT NULL AND IsDeleted = 0;

CREATE TABLE dbo.Permissions
(
    PermissionId uniqueidentifier NOT NULL,
    PermissionCode nvarchar(150) NOT NULL,
    NormalizedPermissionCode nvarchar(150) NOT NULL,
    Name nvarchar(200) NOT NULL,
    ModuleCode nvarchar(100) NOT NULL,
    Description nvarchar(1000) NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Permissions_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Permissions_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Permissions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Permissions_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED (PermissionId),
    CONSTRAINT UQ_Permissions_NormalizedPermissionCode UNIQUE (NormalizedPermissionCode),
    CONSTRAINT CK_Permissions_NormalizedPermissionCode CHECK (NormalizedPermissionCode = UPPER(LTRIM(RTRIM(PermissionCode)))),
    CONSTRAINT CK_Permissions_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Permissions_Version CHECK (Version > 0)
);

CREATE TABLE dbo.RolePermissions
(
    RolePermissionId uniqueidentifier NOT NULL,
    RoleId uniqueidentifier NOT NULL,
    PermissionId uniqueidentifier NOT NULL,
    GrantedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RolePermissions_GrantedAtUtc DEFAULT SYSUTCDATETIME(),
    GrantedByUserId uniqueidentifier NULL,
    Version bigint NOT NULL CONSTRAINT DF_RolePermissions_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RolePermissions_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_RolePermissions_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED (RolePermissionId),
    CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions (PermissionId),
    CONSTRAINT FK_RolePermissions_GrantedByUsers FOREIGN KEY (GrantedByUserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT CK_RolePermissions_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_RolePermissions_Role_Permission_Active ON dbo.RolePermissions (RoleId, PermissionId) WHERE IsDeleted = 0;

CREATE TABLE dbo.UserRoles
(
    UserRoleId uniqueidentifier NOT NULL,
    RoleId uniqueidentifier NOT NULL,
    CompanyUserId uniqueidentifier NULL,
    BranchUserId uniqueidentifier NULL,
    StartsAtUtc datetime2(7) NULL,
    EndsAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_UserRoles_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserRoles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserRoles_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_UserRoles_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserRoleId),
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId),
    CONSTRAINT FK_UserRoles_CompanyUsers FOREIGN KEY (CompanyUserId) REFERENCES dbo.CompanyUsers (CompanyUserId),
    CONSTRAINT FK_UserRoles_BranchUsers FOREIGN KEY (BranchUserId) REFERENCES dbo.BranchUsers (BranchUserId),
    CONSTRAINT CK_UserRoles_OneMembership CHECK ((CompanyUserId IS NULL AND BranchUserId IS NOT NULL) OR (CompanyUserId IS NOT NULL AND BranchUserId IS NULL)),
    CONSTRAINT CK_UserRoles_Period CHECK (EndsAtUtc IS NULL OR StartsAtUtc IS NULL OR EndsAtUtc >= StartsAtUtc),
    CONSTRAINT CK_UserRoles_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_UserRoles_CompanyUser_Role_Active ON dbo.UserRoles (CompanyUserId, RoleId) WHERE CompanyUserId IS NOT NULL AND IsDeleted = 0;
CREATE UNIQUE INDEX UX_UserRoles_BranchUser_Role_Active ON dbo.UserRoles (BranchUserId, RoleId) WHERE BranchUserId IS NOT NULL AND IsDeleted = 0;

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 2, N'002_CreateIdentityTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
