SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=9) THROW 59000,N'Thiếu tiền điều kiện.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10) THROW 59001,N'Checksum không khớp.',1;
BEGIN TRANSACTION;
DECLARE @OperationalMigrationLockResult int;
EXEC @OperationalMigrationLockResult = sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Baseline', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
IF @OperationalMigrationLockResult < 0 THROW 59090,N'Không lấy được application lock Operational v1.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10 AND ScriptChecksum='$(ScriptChecksum)') BEGIN COMMIT; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10) THROW 59001,N'Checksum không khớp.',1;
CREATE TABLE dbo.ActivityLogs (ActivityLogId uniqueidentifier NOT NULL CONSTRAINT PK_ActivityLogs PRIMARY KEY, PublicId char(24) COLLATE Latin1_General_100_BIN2 NOT NULL, ActorUserId uniqueidentifier NULL, ActorDisplayNameSnapshot nvarchar(200) NULL, ProductId uniqueidentifier NULL, ProductNameSnapshot nvarchar(300) NULL, ActionCode nvarchar(200) NOT NULL, OccurredAtUtc datetime2(7) NOT NULL, SafeDetail nvarchar(2000) NULL, CONSTRAINT FK_ActivityLogs_Actor FOREIGN KEY(ActorUserId) REFERENCES dbo.Users(UserId), CONSTRAINT FK_ActivityLogs_Product FOREIGN KEY(ProductId) REFERENCES dbo.Products(ProductId), CONSTRAINT UQ_ActivityLogs_PublicId UNIQUE(PublicId), CONSTRAINT CK_ActivityLogs_PublicId CHECK(PublicId NOT LIKE '%[^0-9a-f]%' AND LEN(PublicId)=24));
CREATE TABLE dbo.ActivityLogDetails (ActivityLogDetailId uniqueidentifier NOT NULL CONSTRAINT PK_ActivityLogDetails PRIMARY KEY, ActivityLogId uniqueidentifier NOT NULL, FieldName nvarchar(200) NOT NULL, OldValue nvarchar(max) NULL, NewValue nvarchar(max) NULL, SortOrder int NOT NULL CONSTRAINT DF_ActivityLogDetails_Sort DEFAULT 0, CONSTRAINT FK_ActivityLogDetails_Log FOREIGN KEY(ActivityLogId) REFERENCES dbo.ActivityLogs(ActivityLogId));
CREATE TABLE dbo.ArchivedChatMessages (ArchivedChatMessageId uniqueidentifier NOT NULL CONSTRAINT PK_ArchivedChatMessages PRIMARY KEY, PublicId char(24) COLLATE Latin1_General_100_BIN2 NOT NULL, SessionReference nvarchar(300) NULL, SenderReference nvarchar(300) NULL, SenderDisplayNameSnapshot nvarchar(200) NULL, MessageBody nvarchar(max) NOT NULL, SourceOccurredAtUtc datetime2(7) NULL, ArchivedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ArchivedChatMessages_Archived DEFAULT SYSUTCDATETIME(), RetentionUntilUtc datetime2(7) NULL, RetentionStatus nvarchar(20) COLLATE Latin1_General_100_BIN2 NOT NULL CONSTRAINT DF_ArchivedChatMessages_Retention DEFAULT N'Restricted', CONSTRAINT UQ_ArchivedChatMessages_PublicId UNIQUE(PublicId), CONSTRAINT CK_ArchivedChatMessages_PublicId CHECK(PublicId NOT LIKE '%[^0-9a-f]%' AND LEN(PublicId)=24), CONSTRAINT CK_ArchivedChatMessages_Retention CHECK(RetentionStatus IN(N'Restricted',N'Expired',N'LegalHold')));
GO
CREATE OR ALTER TRIGGER dbo.TR_ActivityLogs_Immutable ON dbo.ActivityLogs AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF USER_NAME()<>N'dbo'
 BEGIN ROLLBACK TRANSACTION; THROW 59091,N'ActivityLogs không cho phép sửa hoặc xóa trực tiếp.',1; END;
END;
GO
CREATE OR ALTER TRIGGER dbo.TR_ArchivedChatMessages_Immutable ON dbo.ArchivedChatMessages AFTER UPDATE, DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF USER_NAME()<>N'dbo'
 BEGIN ROLLBACK TRANSACTION; THROW 59092,N'ArchivedChatMessages không cho phép sửa hoặc xóa trực tiếp.',1; END;
END;
GO
DENY UPDATE, DELETE ON dbo.ActivityLogs TO OperationalRuntime;
DENY UPDATE, DELETE ON dbo.ActivityLogDetails TO OperationalRuntime;
DENY UPDATE, DELETE ON dbo.ArchivedChatMessages TO OperationalRuntime;
GO
CREATE OR ALTER PROCEDURE dbo.PurgeActivityLogs @BeforeUtc datetime2(7)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 DELETE d FROM dbo.ActivityLogDetails d JOIN dbo.ActivityLogs l ON l.ActivityLogId=d.ActivityLogId WHERE l.OccurredAtUtc<@BeforeUtc;
 DELETE FROM dbo.ActivityLogs WHERE OccurredAtUtc<@BeforeUtc;
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
CREATE OR ALTER PROCEDURE dbo.PurgeArchivedChatMessages @BeforeUtc datetime2(7)
WITH EXECUTE AS OWNER
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 BEGIN TRY BEGIN TRANSACTION;
 DELETE FROM dbo.ArchivedChatMessages WHERE ArchivedAtUtc<@BeforeUtc AND RetentionStatus=N'Expired';
 COMMIT; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
END;
GO
GRANT EXECUTE ON dbo.PurgeActivityLogs TO OperationalAuditMaintenance;
GRANT EXECUTE ON dbo.PurgeArchivedChatMessages TO OperationalAuditMaintenance;
GO
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10)
    INSERT dbo.SchemaVersions SELECT NEWID(),N'Operational',10,N'010_CreateAuditAndArchiveTables.sql','$(ScriptChecksum)',SYSUTCDATETIME(),ORIGINAL_LOGIN();
IF @@TRANCOUNT>0 COMMIT;
GO
