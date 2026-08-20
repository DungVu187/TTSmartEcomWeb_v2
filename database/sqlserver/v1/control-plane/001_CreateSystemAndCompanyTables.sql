SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRANSACTION;
DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock @Resource = N'TTSmart.ControlPlane.V1.Schema', @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
IF @LockResult < 0 THROW 51103, N'Khong lay duoc khoa baseline ControlPlane.', 1;
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode = N'ControlPlane' AND MigrationNumber = 1 AND ScriptChecksum <> '$(ScriptChecksum)') THROW 51101, N'Checksum migration ControlPlane/001 khong khop.', 1;
    IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode = N'ControlPlane' AND MigrationNumber = 1) BEGIN COMMIT TRANSACTION; RETURN; END;
    THROW 51102, N'SchemaVersions da ton tai nhung baseline ControlPlane/001 khong ro trang thai.', 1;
END;

CREATE TABLE dbo.SchemaVersions
(
    SchemaVersionId uniqueidentifier NOT NULL,
    ModuleCode nvarchar(80) NOT NULL,
    MigrationNumber int NOT NULL,
    MigrationName nvarchar(260) NOT NULL,
    ScriptChecksum char(64) NOT NULL,
    AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    AppliedBy nvarchar(128) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedBy DEFAULT ORIGINAL_LOGIN(),
    CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (SchemaVersionId),
    CONSTRAINT UQ_SchemaVersions_Module_Migration UNIQUE (ModuleCode, MigrationNumber),
    CONSTRAINT UQ_SchemaVersions_Module_Name UNIQUE (ModuleCode, MigrationName),
    CONSTRAINT CK_SchemaVersions_ModuleCode CHECK (LEN(LTRIM(RTRIM(ModuleCode))) > 0),
    CONSTRAINT CK_SchemaVersions_MigrationNumber CHECK (MigrationNumber > 0),
    CONSTRAINT CK_SchemaVersions_ScriptChecksum CHECK (ScriptChecksum COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%' AND LEN(ScriptChecksum) = 64)
);

CREATE TABLE dbo.DatabaseInfo
(
    DatabaseInfoId uniqueidentifier NOT NULL,
    DatabaseKind nvarchar(40) NOT NULL,
    DatabaseCode nvarchar(80) NOT NULL,
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_DatabaseInfo_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_DatabaseInfo PRIMARY KEY CLUSTERED (DatabaseInfoId),
    CONSTRAINT UQ_DatabaseInfo_DatabaseKind UNIQUE (DatabaseKind),
    CONSTRAINT UQ_DatabaseInfo_DatabaseCode UNIQUE (DatabaseCode),
    CONSTRAINT CK_DatabaseInfo_DatabaseKind CHECK (DatabaseKind = N'ControlPlane'),
    CONSTRAINT CK_DatabaseInfo_DatabaseCode CHECK (LEN(LTRIM(RTRIM(DatabaseCode))) > 0)
);

CREATE TABLE dbo.Companies
(
    CompanyId uniqueidentifier NOT NULL,
    CompanyCode nvarchar(64) NOT NULL,
    NormalizedCompanyCode nvarchar(64) NOT NULL,
    LegalName nvarchar(300) NOT NULL,
    DisplayName nvarchar(300) NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Companies_Status DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Companies_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Companies_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED (CompanyId),
    CONSTRAINT UQ_Companies_NormalizedCompanyCode UNIQUE (NormalizedCompanyCode),
    CONSTRAINT CK_Companies_Code CHECK (LEN(LTRIM(RTRIM(CompanyCode))) > 0 AND NormalizedCompanyCode = UPPER(LTRIM(RTRIM(CompanyCode)))),
    CONSTRAINT CK_Companies_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.CompanySettings
(
    CompanySettingId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    SettingCode nvarchar(100) NOT NULL,
    SettingValueJson nvarchar(max) NULL,
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanySettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanySettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_CompanySettings PRIMARY KEY CLUSTERED (CompanySettingId),
    CONSTRAINT FK_CompanySettings_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT UQ_CompanySettings_Company_Code UNIQUE (CompanyId, SettingCode),
    CONSTRAINT CK_CompanySettings_Code CHECK (LEN(LTRIM(RTRIM(SettingCode))) > 0),
    CONSTRAINT CK_CompanySettings_Json CHECK (SettingValueJson IS NULL OR ISJSON(SettingValueJson) = 1)
);

CREATE TABLE dbo.Branches
(
    BranchId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    BranchCode nvarchar(64) NOT NULL,
    NormalizedBranchCode nvarchar(64) NOT NULL,
    Name nvarchar(300) NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Branches_Status DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Branches_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Branches_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY CLUSTERED (BranchId),
    CONSTRAINT FK_Branches_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT UQ_Branches_Company_Code UNIQUE (CompanyId, NormalizedBranchCode),
    CONSTRAINT UQ_Branches_Company_Branch UNIQUE (CompanyId, BranchId),
    CONSTRAINT CK_Branches_Code CHECK (LEN(LTRIM(RTRIM(BranchCode))) > 0 AND NormalizedBranchCode = UPPER(LTRIM(RTRIM(BranchCode)))),
    CONSTRAINT CK_Branches_Status CHECK (Status IN (0, 1, 2))
);

CREATE TABLE dbo.SecretReferences
(
    SecretReferenceId uniqueidentifier NOT NULL,
    ProviderCode nvarchar(80) NOT NULL,
    ReferenceKey nvarchar(500) NOT NULL,
    PurposeCode nvarchar(80) NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_SecretReferences_Status DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SecretReferences_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SecretReferences_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_SecretReferences PRIMARY KEY CLUSTERED (SecretReferenceId),
    CONSTRAINT UQ_SecretReferences_Provider_Reference UNIQUE (ProviderCode, ReferenceKey),
    CONSTRAINT CK_SecretReferences_ProviderCode CHECK (LEN(LTRIM(RTRIM(ProviderCode))) > 0),
    CONSTRAINT CK_SecretReferences_ReferenceKey CHECK (LEN(LTRIM(RTRIM(ReferenceKey))) > 0),
    CONSTRAINT CK_SecretReferences_PurposeCode CHECK (LEN(LTRIM(RTRIM(PurposeCode))) > 0),
    CONSTRAINT CK_SecretReferences_Status CHECK (Status IN (0, 1, 2))
);

INSERT dbo.DatabaseInfo (DatabaseInfoId, DatabaseKind, DatabaseCode) VALUES (NEWID(), N'ControlPlane', N'ControlPlaneV1');
INSERT dbo.SchemaVersions (SchemaVersionId, ModuleCode, MigrationNumber, MigrationName, ScriptChecksum)
VALUES (NEWID(), N'ControlPlane', 1, N'001_CreateSystemAndCompanyTables.sql', '$(ScriptChecksum)');
COMMIT TRANSACTION;
