SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=13) THROW 59100,N'Thiếu tiền điều kiện.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=14 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=14) THROW 59101,N'Checksum không khớp.',1;
BEGIN TRANSACTION;
DECLARE @LockResult int;
EXEC @LockResult=sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Baseline',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
IF @LockResult<0 THROW 59190,N'Không lấy được application lock Operational v1.',1;
IF COL_LENGTH(N'dbo.StockOperations',N'TransactionDateUtc') IS NULL ALTER TABLE dbo.StockOperations ADD TransactionDateUtc datetime2(7) NULL;

-- Runtime inventory persistence uses the unified InventoryOrders table.  The
-- previous version only attempted an UPDATE when the column already existed,
-- so a freshly provisioned database still failed with "Invalid column name".
IF OBJECT_ID(N'dbo.InventoryOrders',N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.InventoryOrders',N'TransactionDateUtc') IS NULL ALTER TABLE dbo.InventoryOrders ADD TransactionDateUtc datetime2(7) NULL;
    UPDATE dbo.InventoryOrders SET TransactionDateUtc=COALESCE(TransactionDateUtc,SourceCreatedAtUtc) WHERE TransactionDateUtc IS NULL;
END;

-- Keep the split-table schema compatible for databases that still contain it;
-- do not try to ALTER absent legacy tables.
IF OBJECT_ID(N'dbo.ImportOrders',N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ImportOrders',N'TransactionDateUtc') IS NULL ALTER TABLE dbo.ImportOrders ADD TransactionDateUtc datetime2(7) NULL;
    UPDATE dbo.ImportOrders SET TransactionDateUtc=COALESCE(TransactionDateUtc,SourceCreatedAtUtc) WHERE TransactionDateUtc IS NULL;
END;
IF OBJECT_ID(N'dbo.ExportOrders',N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.ExportOrders',N'TransactionDateUtc') IS NULL ALTER TABLE dbo.ExportOrders ADD TransactionDateUtc datetime2(7) NULL;
    UPDATE dbo.ExportOrders SET TransactionDateUtc=COALESCE(TransactionDateUtc,SourceCreatedAtUtc) WHERE TransactionDateUtc IS NULL;
END;
UPDATE dbo.StockOperations SET TransactionDateUtc=COALESCE(TransactionDateUtc,OccurredAtUtc) WHERE TransactionDateUtc IS NULL;
INSERT dbo.SchemaVersions SELECT NEWID(),N'Operational',14,N'014_AddInventoryTransactionDates.sql','$(ScriptChecksum)',SYSUTCDATETIME(),ORIGINAL_LOGIN();
COMMIT;
