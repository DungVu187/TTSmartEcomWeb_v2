SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
GO
USE [TTSmart];
GO
IF DB_NAME() <> N'TTSmart' THROW 51101, N'Script phải chạy trên [TTSmart].', 1;
IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber=1) BEGIN PRINT N'Migration 001 đã được áp dụng; không có thay đổi.'; RETURN; END;
    THROW 51102, N'SchemaVersions đã tồn tại nhưng không có migration 001.', 1;
END;
BEGIN TRANSACTION;
CREATE TABLE dbo.SchemaVersions (SchemaVersionId uniqueidentifier NOT NULL PRIMARY KEY, MigrationNumber int NOT NULL UNIQUE, MigrationName nvarchar(300) NOT NULL UNIQUE, ScriptChecksum char(64) NULL, AppliedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), AppliedBy nvarchar(128) NOT NULL, CONSTRAINT CK_SchemaVersions_Number CHECK(MigrationNumber>0), CONSTRAINT CK_SchemaVersions_Checksum CHECK(ScriptChecksum IS NULL OR ScriptChecksum NOT LIKE '%[^0-9A-Fa-f]%'));
CREATE TABLE dbo.DatabaseInfo (DatabaseInfoId uniqueidentifier NOT NULL PRIMARY KEY, SingletonKey tinyint NOT NULL UNIQUE, DatabaseRole nvarchar(50) NOT NULL, SchemaName nvarchar(128) NOT NULL, Version bigint NOT NULL DEFAULT 1, CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL, CONSTRAINT CK_DatabaseInfo_Singleton CHECK(SingletonKey=1), CONSTRAINT CK_DatabaseInfo_Version CHECK(Version>0));
CREATE TABLE dbo.MigrationRuns (MigrationRunId uniqueidentifier NOT NULL PRIMARY KEY, RunName nvarchar(200) NOT NULL, SourceSystem nvarchar(100) NOT NULL, Status nvarchar(20) NOT NULL, StartedAtUtc datetime2(7) NOT NULL, FinishedAtUtc datetime2(7) NULL, CorrelationId uniqueidentifier NOT NULL, Summary nvarchar(1000) NULL, Version bigint NOT NULL DEFAULT 1, CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL, CONSTRAINT CK_MigrationRuns_Status CHECK(Status IN(N'Pending',N'Running',N'Completed',N'Failed')), CONSTRAINT CK_MigrationRuns_Time CHECK(FinishedAtUtc IS NULL OR FinishedAtUtc>=StartedAtUtc));
CREATE TABLE dbo.MigrationIssues (MigrationIssueId uniqueidentifier NOT NULL PRIMARY KEY, MigrationRunId uniqueidentifier NOT NULL, SourceCollection nvarchar(100) NOT NULL, LegacyObjectId char(24) NULL, IssueCode nvarchar(100) NOT NULL, SafeDetail nvarchar(2000) NULL, Status nvarchar(20) NOT NULL DEFAULT N'Open', CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), ResolvedAtUtc datetime2(7) NULL, CONSTRAINT FK_MigrationIssues_Runs FOREIGN KEY(MigrationRunId) REFERENCES dbo.MigrationRuns(MigrationRunId), CONSTRAINT CK_MigrationIssues_Status CHECK(Status IN(N'Open',N'Resolved',N'Accepted')));
CREATE TABLE dbo.LegacyIds (LegacyIdId uniqueidentifier NOT NULL PRIMARY KEY, SourceCollection nvarchar(100) NOT NULL, LegacyObjectId char(24) NOT NULL, TargetTable nvarchar(128) NOT NULL, TargetId uniqueidentifier NOT NULL, CreatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT UQ_LegacyIds_Source UNIQUE(SourceCollection,LegacyObjectId), CONSTRAINT UQ_LegacyIds_Target UNIQUE(TargetTable,TargetId));
CREATE TABLE dbo.NumberSequences (NumberSequenceId uniqueidentifier NOT NULL PRIMARY KEY, SequenceCode nvarchar(50) NOT NULL, NextValue bigint NOT NULL, Prefix nvarchar(30) NULL, Version bigint NOT NULL DEFAULT 1, UpdatedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), RowVersion rowversion NOT NULL, CONSTRAINT UQ_NumberSequences_Code UNIQUE(SequenceCode), CONSTRAINT CK_NumberSequences_NextValue CHECK(NextValue>0));
INSERT dbo.DatabaseInfo(DatabaseInfoId,SingletonKey,DatabaseRole,SchemaName) VALUES(NEWID(),1,N'TTSmartSales',N'dbo');
INSERT dbo.SchemaVersions(SchemaVersionId,MigrationNumber,MigrationName,ScriptChecksum,AppliedBy) VALUES(NEWID(),1,N'001_CreateSystemTables.sql',NULL,ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
