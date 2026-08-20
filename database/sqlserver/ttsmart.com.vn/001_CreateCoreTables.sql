/*
    Migration 001: schema version, công ty, chi nhánh và registry database chi nhánh.
    Chạy sau 000_CreateDatabase.sql bằng sqlcmd với Windows Authentication.
*/
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

IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 1)
    BEGIN
        PRINT N'Migration 001 đã được áp dụng; không có thay đổi.';
        RETURN;
    END;

    THROW 51001, N'Phát hiện schema không rõ trạng thái: dbo.SchemaVersions đã tồn tại nhưng không có migration 001.', 1;
END;

BEGIN TRANSACTION;

CREATE TABLE dbo.SchemaVersions
(
    SchemaVersionId uniqueidentifier NOT NULL,
    MigrationNumber int NOT NULL,
    MigrationName nvarchar(300) NOT NULL,
    ScriptChecksum char(64) NULL,
    AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    AppliedBy nvarchar(128) NOT NULL,
    CONSTRAINT PK_SchemaVersions PRIMARY KEY CLUSTERED (SchemaVersionId),
    CONSTRAINT UQ_SchemaVersions_MigrationNumber UNIQUE (MigrationNumber),
    CONSTRAINT UQ_SchemaVersions_MigrationName UNIQUE (MigrationName),
    CONSTRAINT CK_SchemaVersions_MigrationNumber CHECK (MigrationNumber > 0),
    CONSTRAINT CK_SchemaVersions_ScriptChecksum CHECK (ScriptChecksum IS NULL OR ScriptChecksum NOT LIKE '%[^0-9A-Fa-f]%')
);

CREATE TABLE dbo.Companies
(
    CompanyId uniqueidentifier NOT NULL,
    CompanyCode nvarchar(64) NOT NULL,
    NormalizedCompanyCode nvarchar(64) NOT NULL,
    LegalName nvarchar(300) NOT NULL,
    DisplayName nvarchar(300) NOT NULL,
    TaxCode nvarchar(64) NULL,
    ContactEmail nvarchar(320) NULL,
    ContactPhone nvarchar(32) NULL,
    AddressLine nvarchar(1000) NULL,
    TimezoneId nvarchar(100) NOT NULL CONSTRAINT DF_Companies_TimezoneId DEFAULT N'Asia/Ho_Chi_Minh',
    Status tinyint NOT NULL CONSTRAINT DF_Companies_Status DEFAULT (0),
    Version bigint NOT NULL CONSTRAINT DF_Companies_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Companies_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Companies_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Companies_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Companies PRIMARY KEY CLUSTERED (CompanyId),
    CONSTRAINT UQ_Companies_NormalizedCompanyCode UNIQUE (NormalizedCompanyCode),
    CONSTRAINT CK_Companies_CompanyCode CHECK (LEN(LTRIM(RTRIM(CompanyCode))) > 0),
    CONSTRAINT CK_Companies_NormalizedCompanyCode CHECK (NormalizedCompanyCode = UPPER(LTRIM(RTRIM(CompanyCode)))),
    CONSTRAINT CK_Companies_Status CHECK (Status IN (0, 1, 2, 3)),
    CONSTRAINT CK_Companies_Version CHECK (Version > 0)
);

CREATE TABLE dbo.Branches
(
    BranchId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    BranchCode nvarchar(64) NOT NULL,
    NormalizedBranchCode nvarchar(64) NOT NULL,
    Name nvarchar(300) NOT NULL,
    IsHeadOffice bit NOT NULL CONSTRAINT DF_Branches_IsHeadOffice DEFAULT (0),
    Email nvarchar(320) NULL,
    Phone nvarchar(32) NULL,
    AddressLine nvarchar(1000) NULL,
    TimezoneId nvarchar(100) NOT NULL CONSTRAINT DF_Branches_TimezoneId DEFAULT N'Asia/Ho_Chi_Minh',
    Status tinyint NOT NULL CONSTRAINT DF_Branches_Status DEFAULT (0),
    Version bigint NOT NULL CONSTRAINT DF_Branches_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Branches_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Branches_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Branches_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Branches PRIMARY KEY CLUSTERED (BranchId),
    CONSTRAINT FK_Branches_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT UQ_Branches_Company_NormalizedBranchCode UNIQUE (CompanyId, NormalizedBranchCode),
    CONSTRAINT CK_Branches_BranchCode CHECK (LEN(LTRIM(RTRIM(BranchCode))) > 0),
    CONSTRAINT CK_Branches_NormalizedBranchCode CHECK (NormalizedBranchCode = UPPER(LTRIM(RTRIM(BranchCode)))),
    CONSTRAINT CK_Branches_Status CHECK (Status IN (0, 1, 2, 3)),
    CONSTRAINT CK_Branches_Version CHECK (Version > 0)
);

CREATE UNIQUE INDEX UX_Branches_Company_HeadOffice
    ON dbo.Branches (CompanyId)
    WHERE IsHeadOffice = 1 AND IsDeleted = 0;
CREATE INDEX IX_Branches_Company_Status
    ON dbo.Branches (CompanyId, Status, IsDeleted);

CREATE TABLE dbo.SecretReferences
(
    SecretReferenceId uniqueidentifier NOT NULL,
    ProviderType tinyint NOT NULL,
    ExternalKey nvarchar(500) NOT NULL,
    Purpose tinyint NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_SecretReferences_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_SecretReferences_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SecretReferences_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SecretReferences_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_SecretReferences_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_SecretReferences PRIMARY KEY CLUSTERED (SecretReferenceId),
    CONSTRAINT UQ_SecretReferences_Provider_ExternalKey UNIQUE (ProviderType, ExternalKey),
    CONSTRAINT CK_SecretReferences_ExternalKey CHECK (LEN(LTRIM(RTRIM(ExternalKey))) > 0),
    CONSTRAINT CK_SecretReferences_ProviderType CHECK (ProviderType IN (1, 2, 3, 4)),
    CONSTRAINT CK_SecretReferences_Purpose CHECK (Purpose IN (1, 2, 3, 4)),
    CONSTRAINT CK_SecretReferences_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_SecretReferences_Version CHECK (Version > 0)
);

CREATE TABLE dbo.BranchDatabases
(
    BranchDatabaseId uniqueidentifier NOT NULL,
    BranchId uniqueidentifier NOT NULL,
    ServerAlias nvarchar(100) NOT NULL,
    DatabaseName nvarchar(128) NOT NULL,
    NormalizedDatabaseName nvarchar(128) NOT NULL,
    SqlLoginName nvarchar(128) NOT NULL,
    SecretReferenceId uniqueidentifier NOT NULL,
    ProvisioningStatus tinyint NOT NULL CONSTRAINT DF_BranchDatabases_ProvisioningStatus DEFAULT (0),
    SchemaVersion nvarchar(64) NULL,
    LastHealthCheckAtUtc datetime2(7) NULL,
    LastValidatedAtUtc datetime2(7) NULL,
    FailureCode nvarchar(100) NULL,
    FailureSummary nvarchar(1000) NULL,
    Version bigint NOT NULL CONSTRAINT DF_BranchDatabases_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchDatabases_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchDatabases_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_BranchDatabases PRIMARY KEY CLUSTERED (BranchDatabaseId),
    CONSTRAINT FK_BranchDatabases_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (BranchId),
    CONSTRAINT FK_BranchDatabases_SecretReferences FOREIGN KEY (SecretReferenceId) REFERENCES dbo.SecretReferences (SecretReferenceId),
    CONSTRAINT UQ_BranchDatabases_Branch UNIQUE (BranchId),
    CONSTRAINT UQ_BranchDatabases_NormalizedDatabaseName UNIQUE (NormalizedDatabaseName),
    CONSTRAINT UQ_BranchDatabases_SqlLoginName UNIQUE (SqlLoginName),
    CONSTRAINT CK_BranchDatabases_DatabaseName CHECK (DatabaseName LIKE N'%[_]online' AND LEN(LTRIM(RTRIM(DatabaseName))) > 7),
    CONSTRAINT CK_BranchDatabases_NormalizedDatabaseName CHECK (NormalizedDatabaseName = UPPER(LTRIM(RTRIM(DatabaseName)))),
    CONSTRAINT CK_BranchDatabases_ProvisioningStatus CHECK (ProvisioningStatus IN (0, 1, 2, 3, 4, 5, 6, 7)),
    CONSTRAINT CK_BranchDatabases_Version CHECK (Version > 0)
);
CREATE INDEX IX_BranchDatabases_ProvisioningStatus ON dbo.BranchDatabases (ProvisioningStatus, UpdatedAtUtc);

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 1, N'001_CreateCoreTables.sql', NULL, ORIGINAL_LOGIN());

COMMIT TRANSACTION;
GO
