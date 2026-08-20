SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
GO
USE [TTSmart];
GO
IF DB_NAME()<>N'TTSmart' THROW 51180,N'Script phải chạy trên [TTSmart].',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber=8) BEGIN PRINT N'Migration 008 đã được áp dụng; không có thay đổi.'; RETURN; END;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber=7) THROW 51181,N'Chưa áp dụng migration 007.',1;
BEGIN TRANSACTION;
CREATE TABLE dbo.ActivityLogs (ActivityLogId uniqueidentifier NOT NULL PRIMARY KEY, LegacyObjectId char(24) NULL, ActorUserId uniqueidentifier NULL, UserName nvarchar(200) NULL, Action nvarchar(200) NOT NULL, ProductId uniqueidentifier NULL, LegacyProductObjectId char(24) NULL, ProductName nvarchar(300) NULL, CreatedAtUtc datetime2(7) NULL, UpdatedAtUtc datetime2(7) NULL, MigratedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), CONSTRAINT FK_ActivityLogs_Users FOREIGN KEY(ActorUserId) REFERENCES dbo.Users(UserId), CONSTRAINT FK_ActivityLogs_Products FOREIGN KEY(ProductId) REFERENCES dbo.Products(ProductId));
CREATE UNIQUE INDEX UX_ActivityLogs_LegacyObjectId ON dbo.ActivityLogs(LegacyObjectId) WHERE LegacyObjectId IS NOT NULL;
CREATE INDEX IX_ActivityLogs_CreatedAtUtc ON dbo.ActivityLogs(CreatedAtUtc DESC);
CREATE TABLE dbo.ActivityLogDetails (ActivityLogDetailId uniqueidentifier NOT NULL PRIMARY KEY, LegacyObjectId char(24) NULL, ActivityLogId uniqueidentifier NOT NULL, FieldName nvarchar(200) NOT NULL, OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL, SortOrder int NOT NULL DEFAULT 0, CONSTRAINT FK_ActivityLogDetails_Logs FOREIGN KEY(ActivityLogId) REFERENCES dbo.ActivityLogs(ActivityLogId));
CREATE UNIQUE INDEX UX_ActivityLogDetails_LegacyObjectId ON dbo.ActivityLogDetails(LegacyObjectId) WHERE LegacyObjectId IS NOT NULL;
CREATE TABLE dbo.ArchivedChatMessages (ArchivedChatMessageId uniqueidentifier NOT NULL PRIMARY KEY, LegacyObjectId char(24) NULL, SessionId nvarchar(200) NULL, SenderName nvarchar(200) NULL, SenderPhone nvarchar(32) NULL, SenderRole nvarchar(50) NULL, Message nvarchar(max) NOT NULL, CreatedAtUtc datetime2(7) NULL, UpdatedAtUtc datetime2(7) NULL, ArchivedAtUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(), RetentionStatus nvarchar(20) NOT NULL DEFAULT N'Restricted', CONSTRAINT CK_ArchivedChatMessages_Retention CHECK(RetentionStatus IN(N'Restricted',N'Expired',N'LegalHold')));
CREATE UNIQUE INDEX UX_ArchivedChatMessages_LegacyObjectId ON dbo.ArchivedChatMessages(LegacyObjectId) WHERE LegacyObjectId IS NOT NULL;
INSERT dbo.SchemaVersions(SchemaVersionId,MigrationNumber,MigrationName,ScriptChecksum,AppliedBy) VALUES(NEWID(),8,N'008_CreateAuditAndMigrationTables.sql',NULL,ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
