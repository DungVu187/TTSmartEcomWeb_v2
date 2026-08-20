/* Migration 003: danh mục tính năng và cấu hình theo company/branch. */
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
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 3)
BEGIN
    PRINT N'Migration 003 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 2)
    THROW 51003, N'Chưa áp dụng migration 002.', 1;

BEGIN TRANSACTION;

CREATE TABLE dbo.Features
(
    FeatureId uniqueidentifier NOT NULL,
    FeatureCode nvarchar(150) NOT NULL,
    NormalizedFeatureCode nvarchar(150) NOT NULL,
    Name nvarchar(200) NOT NULL,
    ScopeType tinyint NOT NULL,
    ModuleCode nvarchar(100) NOT NULL,
    ConfigurationSchemaJson nvarchar(max) NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Features_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Features_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Features_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Features_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Features_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Features PRIMARY KEY CLUSTERED (FeatureId),
    CONSTRAINT UQ_Features_NormalizedFeatureCode UNIQUE (NormalizedFeatureCode),
    CONSTRAINT CK_Features_NormalizedFeatureCode CHECK (NormalizedFeatureCode = UPPER(LTRIM(RTRIM(FeatureCode)))),
    CONSTRAINT CK_Features_ScopeType CHECK (ScopeType IN (1, 2, 3)),
    CONSTRAINT CK_Features_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Features_ConfigurationSchemaJson CHECK (ConfigurationSchemaJson IS NULL OR ISJSON(ConfigurationSchemaJson) = 1),
    CONSTRAINT CK_Features_Version CHECK (Version > 0)
);

CREATE TABLE dbo.CompanyFeatureSettings
(
    CompanyFeatureSettingId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    FeatureId uniqueidentifier NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_CompanyFeatureSettings_IsEnabled DEFAULT (0),
    ConfigurationJson nvarchar(max) NULL,
    EffectiveFromUtc datetime2(7) NULL,
    EffectiveToUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_CompanyFeatureSettings_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyFeatureSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyFeatureSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_CompanyFeatureSettings_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_CompanyFeatureSettings PRIMARY KEY CLUSTERED (CompanyFeatureSettingId),
    CONSTRAINT FK_CompanyFeatureSettings_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_CompanyFeatureSettings_Features FOREIGN KEY (FeatureId) REFERENCES dbo.Features (FeatureId),
    CONSTRAINT CK_CompanyFeatureSettings_ConfigurationJson CHECK (ConfigurationJson IS NULL OR ISJSON(ConfigurationJson) = 1),
    CONSTRAINT CK_CompanyFeatureSettings_Period CHECK (EffectiveToUtc IS NULL OR EffectiveFromUtc IS NULL OR EffectiveToUtc >= EffectiveFromUtc),
    CONSTRAINT CK_CompanyFeatureSettings_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_CompanyFeatureSettings_Company_Feature_Active ON dbo.CompanyFeatureSettings (CompanyId, FeatureId) WHERE IsDeleted = 0;
CREATE INDEX IX_CompanyFeatureSettings_Feature_Enabled ON dbo.CompanyFeatureSettings (FeatureId, IsEnabled, IsDeleted);

CREATE TABLE dbo.BranchFeatureSettings
(
    BranchFeatureSettingId uniqueidentifier NOT NULL,
    BranchId uniqueidentifier NOT NULL,
    FeatureId uniqueidentifier NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_BranchFeatureSettings_IsEnabled DEFAULT (0),
    ConfigurationJson nvarchar(max) NULL,
    EffectiveFromUtc datetime2(7) NULL,
    EffectiveToUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_BranchFeatureSettings_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchFeatureSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchFeatureSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_BranchFeatureSettings_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_BranchFeatureSettings PRIMARY KEY CLUSTERED (BranchFeatureSettingId),
    CONSTRAINT FK_BranchFeatureSettings_Branches FOREIGN KEY (BranchId) REFERENCES dbo.Branches (BranchId),
    CONSTRAINT FK_BranchFeatureSettings_Features FOREIGN KEY (FeatureId) REFERENCES dbo.Features (FeatureId),
    CONSTRAINT CK_BranchFeatureSettings_ConfigurationJson CHECK (ConfigurationJson IS NULL OR ISJSON(ConfigurationJson) = 1),
    CONSTRAINT CK_BranchFeatureSettings_Period CHECK (EffectiveToUtc IS NULL OR EffectiveFromUtc IS NULL OR EffectiveToUtc >= EffectiveFromUtc),
    CONSTRAINT CK_BranchFeatureSettings_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_BranchFeatureSettings_Branch_Feature_Active ON dbo.BranchFeatureSettings (BranchId, FeatureId) WHERE IsDeleted = 0;
CREATE INDEX IX_BranchFeatureSettings_Feature_Enabled ON dbo.BranchFeatureSettings (FeatureId, IsEnabled, IsDeleted);

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 3, N'003_CreateFeatureTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
