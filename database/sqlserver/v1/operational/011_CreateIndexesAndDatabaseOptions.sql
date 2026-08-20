SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=10) THROW 59100,N'Thiếu tiền điều kiện.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=11 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=11) THROW 59101,N'Checksum không khớp.',1;
BEGIN TRANSACTION;
DECLARE @OperationalMigrationLockResult int;
EXEC @OperationalMigrationLockResult = sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Baseline', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
IF @OperationalMigrationLockResult < 0 THROW 59190,N'Không lấy được application lock Operational v1.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=11 AND ScriptChecksum='$(ScriptChecksum)') BEGIN COMMIT; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=11) THROW 59101,N'Checksum không khớp.',1;
CREATE INDEX IX_Users_Email ON dbo.Users(Email) WHERE Email IS NOT NULL;
CREATE INDEX IX_SalesOrders_Status ON dbo.SalesOrders(Status,CreatedAtUtc);
CREATE INDEX IX_StockMovementLines_Variant ON dbo.StockMovementLines(ProductVariantId);
CREATE INDEX IX_ActivityLogs_Occurred ON dbo.ActivityLogs(OccurredAtUtc);
INSERT dbo.SchemaVersions SELECT NEWID(),N'Operational',11,N'011_CreateIndexesAndDatabaseOptions.sql','$(ScriptChecksum)',SYSUTCDATETIME(),ORIGINAL_LOGIN(); COMMIT;
/* Không bật READ_COMMITTED_SNAPSHOT: chưa có kiểm thử transaction tồn kho. */
GO
