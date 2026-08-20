SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock @Resource=N'TTSmart.ControlPlane.V1.Schema', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
IF @LockResult < 0 THROW 51203, N'Khong lay duoc khoa baseline ControlPlane.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseInfo WHERE DatabaseKind = N'ControlPlane') THROW 51200, N'DatabaseInfo khong phai ControlPlane.', 1;
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=2 AND ScriptChecksum <> '$(ScriptChecksum)') THROW 51201, N'Checksum migration ControlPlane/002 khong khop.', 1;
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=2) BEGIN COMMIT TRANSACTION; RETURN; END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=1) THROW 51202, N'Thieu migration ControlPlane/001.', 1;

CREATE TABLE dbo.Users
(
 UserId uniqueidentifier NOT NULL, DisplayName nvarchar(200) NOT NULL, Status tinyint NOT NULL CONSTRAINT DF_Users_Status DEFAULT(1), SecurityStamp uniqueidentifier NOT NULL,
 CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Users_CreatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Users_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_Users PRIMARY KEY CLUSTERED(UserId), CONSTRAINT CK_Users_DisplayName CHECK(LEN(LTRIM(RTRIM(DisplayName)))>0), CONSTRAINT CK_Users_Status CHECK(Status IN(0,1,2))
);
CREATE TABLE dbo.UserLogins
(
 UserLoginId uniqueidentifier NOT NULL, UserId uniqueidentifier NOT NULL, LoginType tinyint NOT NULL, LoginValue nvarchar(320) NOT NULL, NormalizedLoginValue nvarchar(320) NOT NULL, IsPrimary bit NOT NULL CONSTRAINT DF_UserLogins_IsPrimary DEFAULT(0), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserLogins_CreatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_UserLogins PRIMARY KEY CLUSTERED(UserLoginId), CONSTRAINT FK_UserLogins_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId), CONSTRAINT UQ_UserLogins_Type_Normalized UNIQUE(LoginType, NormalizedLoginValue), CONSTRAINT CK_UserLogins_Type CHECK(LoginType IN(1,2,3)), CONSTRAINT CK_UserLogins_Value CHECK(LEN(LTRIM(RTRIM(LoginValue)))>0 AND NormalizedLoginValue=UPPER(LTRIM(RTRIM(LoginValue))))
);
CREATE UNIQUE INDEX UX_UserLogins_User_Primary ON dbo.UserLogins(UserId) WHERE IsPrimary=1;
CREATE TABLE dbo.UserPasswords
(
 UserPasswordId uniqueidentifier NOT NULL, UserId uniqueidentifier NOT NULL, PasswordHash nvarchar(500) NOT NULL, HashAlgorithm nvarchar(50) NOT NULL, HashVersion int NOT NULL, SecurityStamp uniqueidentifier NOT NULL, MustRehash bit NOT NULL CONSTRAINT DF_UserPasswords_MustRehash DEFAULT(0), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserPasswords_CreatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_UserPasswords_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_UserPasswords PRIMARY KEY CLUSTERED(UserPasswordId), CONSTRAINT FK_UserPasswords_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId), CONSTRAINT UQ_UserPasswords_User UNIQUE(UserId), CONSTRAINT CK_UserPasswords_Hash CHECK(LEN(LTRIM(RTRIM(PasswordHash)))>0), CONSTRAINT CK_UserPasswords_Algorithm CHECK(LEN(LTRIM(RTRIM(HashAlgorithm)))>0), CONSTRAINT CK_UserPasswords_HashVersion CHECK(HashVersion>0)
);
CREATE TABLE dbo.CompanyUsers
(
 CompanyUserId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, UserId uniqueidentifier NOT NULL, Status tinyint NOT NULL CONSTRAINT DF_CompanyUsers_Status DEFAULT(1), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyUsers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_CompanyUsers PRIMARY KEY CLUSTERED(CompanyUserId), CONSTRAINT FK_CompanyUsers_Companies FOREIGN KEY(CompanyId) REFERENCES dbo.Companies(CompanyId), CONSTRAINT FK_CompanyUsers_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId), CONSTRAINT UQ_CompanyUsers_Company_User UNIQUE(CompanyId,UserId), CONSTRAINT UQ_CompanyUsers_Company_CompanyUser UNIQUE(CompanyId,CompanyUserId), CONSTRAINT UQ_CompanyUsers_CompanyUser_User UNIQUE(CompanyUserId,UserId), CONSTRAINT CK_CompanyUsers_Status CHECK(Status IN(0,1,2))
);
CREATE TABLE dbo.BranchUsers
(
 BranchUserId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, BranchId uniqueidentifier NOT NULL, CompanyUserId uniqueidentifier NOT NULL, UserId uniqueidentifier NOT NULL, Status tinyint NOT NULL CONSTRAINT DF_BranchUsers_Status DEFAULT(1), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchUsers_CreatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchUsers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_BranchUsers PRIMARY KEY CLUSTERED(BranchUserId), CONSTRAINT FK_BranchUsers_Company_Branch FOREIGN KEY(CompanyId,BranchId) REFERENCES dbo.Branches(CompanyId,BranchId), CONSTRAINT FK_BranchUsers_Company_CompanyUser FOREIGN KEY(CompanyId,CompanyUserId) REFERENCES dbo.CompanyUsers(CompanyId,CompanyUserId), CONSTRAINT FK_BranchUsers_CompanyUser_User FOREIGN KEY(CompanyUserId,UserId) REFERENCES dbo.CompanyUsers(CompanyUserId,UserId), CONSTRAINT UQ_BranchUsers_Branch_User UNIQUE(BranchId,UserId), CONSTRAINT UQ_BranchUsers_BranchUser_Company UNIQUE(BranchUserId,CompanyId), CONSTRAINT CK_BranchUsers_Status CHECK(Status IN(0,1,2))
);
CREATE TABLE dbo.RoleTemplates
(
 RoleTemplateId uniqueidentifier NOT NULL, TemplateCode nvarchar(100) NOT NULL, NormalizedTemplateCode nvarchar(100) NOT NULL, Name nvarchar(200) NOT NULL, ScopeType tinyint NOT NULL, Status tinyint NOT NULL CONSTRAINT DF_RoleTemplates_Status DEFAULT(1), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RoleTemplates_CreatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_RoleTemplates PRIMARY KEY CLUSTERED(RoleTemplateId), CONSTRAINT UQ_RoleTemplates_Code UNIQUE(NormalizedTemplateCode), CONSTRAINT CK_RoleTemplates_Code CHECK(NormalizedTemplateCode=UPPER(LTRIM(RTRIM(TemplateCode))) AND LEN(LTRIM(RTRIM(TemplateCode)))>0), CONSTRAINT CK_RoleTemplates_Scope CHECK(ScopeType IN(1,2)), CONSTRAINT CK_RoleTemplates_Status CHECK(Status IN(0,1,2))
);
CREATE TABLE dbo.Roles
(
 RoleId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, RoleCode nvarchar(100) NOT NULL, NormalizedRoleCode nvarchar(100) NOT NULL, Name nvarchar(200) NOT NULL, ScopeType tinyint NOT NULL, Status tinyint NOT NULL CONSTRAINT DF_Roles_Status DEFAULT(1), CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Roles_CreatedAtUtc DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Roles_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED(RoleId), CONSTRAINT FK_Roles_Companies FOREIGN KEY(CompanyId) REFERENCES dbo.Companies(CompanyId), CONSTRAINT UQ_Roles_Company_Code UNIQUE(CompanyId,NormalizedRoleCode), CONSTRAINT UQ_Roles_Company_Role UNIQUE(CompanyId,RoleId), CONSTRAINT CK_Roles_Code CHECK(NormalizedRoleCode=UPPER(LTRIM(RTRIM(RoleCode))) AND LEN(LTRIM(RTRIM(RoleCode)))>0), CONSTRAINT CK_Roles_Scope CHECK(ScopeType IN(1,2)), CONSTRAINT CK_Roles_Status CHECK(Status IN(0,1,2))
);
CREATE TABLE dbo.Permissions
(
 PermissionId uniqueidentifier NOT NULL, PermissionCode nvarchar(150) NOT NULL, NormalizedPermissionCode nvarchar(150) NOT NULL, Name nvarchar(200) NOT NULL, CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Permissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL,
 CONSTRAINT PK_Permissions PRIMARY KEY CLUSTERED(PermissionId), CONSTRAINT UQ_Permissions_Code UNIQUE(NormalizedPermissionCode), CONSTRAINT CK_Permissions_Code CHECK(NormalizedPermissionCode=UPPER(LTRIM(RTRIM(PermissionCode))) AND LEN(LTRIM(RTRIM(PermissionCode)))>0)
);
CREATE TABLE dbo.RolePermissions
(
 RolePermissionId uniqueidentifier NOT NULL, RoleId uniqueidentifier NOT NULL, PermissionId uniqueidentifier NOT NULL, CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_RolePermissions_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_RolePermissions PRIMARY KEY CLUSTERED(RolePermissionId), CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY(RoleId) REFERENCES dbo.Roles(RoleId), CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY(PermissionId) REFERENCES dbo.Permissions(PermissionId), CONSTRAINT UQ_RolePermissions_Role_Permission UNIQUE(RoleId,PermissionId)
);
CREATE TABLE dbo.CompanyUserRoles
(
 CompanyUserRoleId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, CompanyUserId uniqueidentifier NOT NULL, RoleId uniqueidentifier NOT NULL, CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyUserRoles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_CompanyUserRoles PRIMARY KEY CLUSTERED(CompanyUserRoleId), CONSTRAINT FK_CompanyUserRoles_Company_User FOREIGN KEY(CompanyId,CompanyUserId) REFERENCES dbo.CompanyUsers(CompanyId,CompanyUserId), CONSTRAINT FK_CompanyUserRoles_Company_Role FOREIGN KEY(CompanyId,RoleId) REFERENCES dbo.Roles(CompanyId,RoleId), CONSTRAINT UQ_CompanyUserRoles_User_Role UNIQUE(CompanyUserId,RoleId)
);
CREATE TABLE dbo.BranchUserRoles
(
 BranchUserRoleId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, BranchUserId uniqueidentifier NOT NULL, RoleId uniqueidentifier NOT NULL, CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchUserRoles_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
 CONSTRAINT PK_BranchUserRoles PRIMARY KEY CLUSTERED(BranchUserRoleId), CONSTRAINT FK_BranchUserRoles_BranchUser_Company FOREIGN KEY(BranchUserId,CompanyId) REFERENCES dbo.BranchUsers(BranchUserId,CompanyId), CONSTRAINT FK_BranchUserRoles_Company_Role FOREIGN KEY(CompanyId,RoleId) REFERENCES dbo.Roles(CompanyId,RoleId), CONSTRAINT UQ_BranchUserRoles_User_Role UNIQUE(BranchUserId,RoleId)
);
EXEC(N'CREATE TRIGGER dbo.TR_CompanyUserRoles_RequireCompanyScope ON dbo.CompanyUserRoles AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted AS i JOIN dbo.Roles AS r ON r.RoleId=i.RoleId WHERE r.ScopeType<>1) BEGIN ROLLBACK TRANSACTION; THROW 51210,N''CompanyUserRoles chi nhan role scope Company.'',1; END; END;');
EXEC(N'CREATE TRIGGER dbo.TR_BranchUserRoles_RequireBranchScope ON dbo.BranchUserRoles AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM inserted AS i JOIN dbo.Roles AS r ON r.RoleId=i.RoleId WHERE r.ScopeType<>2) BEGIN ROLLBACK TRANSACTION; THROW 51211,N''BranchUserRoles chi nhan role scope Branch.'',1; END; END;');
EXEC(N'CREATE TRIGGER dbo.TR_Roles_PreventAssignedScopeChange ON dbo.Roles AFTER UPDATE AS BEGIN SET NOCOUNT ON; IF UPDATE(ScopeType) AND EXISTS(SELECT 1 FROM inserted AS i JOIN deleted AS d ON d.RoleId=i.RoleId WHERE i.ScopeType<>d.ScopeType AND (EXISTS(SELECT 1 FROM dbo.CompanyUserRoles AS cur WHERE cur.RoleId=i.RoleId) OR EXISTS(SELECT 1 FROM dbo.BranchUserRoles AS bur WHERE bur.RoleId=i.RoleId))) BEGIN ROLLBACK TRANSACTION; THROW 51212,N''Khong duoc doi scope cua role da duoc gan.'',1; END; END;');
INSERT dbo.SchemaVersions(SchemaVersionId,ModuleCode,MigrationNumber,MigrationName,ScriptChecksum) VALUES(NEWID(),N'ControlPlane',2,N'002_CreateIdentityTables.sql','$(ScriptChecksum)');
COMMIT TRANSACTION;
