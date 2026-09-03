SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60401, N'Script reconcile mapping đang kết nối sai database.', 1;
IF DB_NAME() NOT IN (N'TTSmart', N'TTSmart_MAIN_online')
    THROW 60402, N'Script reconcile chỉ cho phép hai database split đã chốt.', 1;
IF LEN(N'$(ScriptChecksum)') <> 64 OR N'$(ScriptChecksum)' LIKE N'%[^0-9A-F]%'
    THROW 60403, N'ScriptChecksum SHA-256 không hợp lệ.', 1;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.CompanyBranchSplit.LegacyMappingReconcile',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60404, N'Không lấy được application lock reconcile.', 1;

DELETE mapping
FROM dbo.MigrationMappings AS mapping
WHERE mapping.TargetTable = N'LegacyRecords'
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.LegacyRecords AS record
      WHERE record.LegacyRecordId = mapping.TargetId
  );

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10003 AND ScriptChecksum <> N'$(ScriptChecksum)')
    THROW 60405, N'Checksum drift của split mapping reconciliation.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10003)
BEGIN
    INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
    VALUES (NEWID(), 10003, N'RemoveDanglingLegacyRecordMappings', N'$(ScriptChecksum)');
END;

COMMIT TRANSACTION;
