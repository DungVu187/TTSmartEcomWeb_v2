SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
BEGIN TRY
    EXEC sp_getapplock @Resource=N'TTSmart.InventoryTransactionDates.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;

    IF COL_LENGTH(N'dbo.InventoryOrders', N'TransactionDateUtc') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.InventoryOrders ADD TransactionDateUtc datetime2(7) NULL;');
        EXEC(N'UPDATE dbo.InventoryOrders SET TransactionDateUtc = COALESCE(TransactionDateUtc, SourceCreatedAtUtc) WHERE TransactionDateUtc IS NULL;');
    END;

    IF COL_LENGTH(N'dbo.StockOperations', N'TransactionDateUtc') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.StockOperations ADD TransactionDateUtc datetime2(7) NULL;');
        EXEC(N'UPDATE dbo.StockOperations SET TransactionDateUtc = COALESCE(TransactionDateUtc, OccurredAtUtc) WHERE TransactionDateUtc IS NULL;');
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 13)
    BEGIN
        INSERT INTO dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedAtUtc)
        VALUES (NEWID(), 13, N'013_AddInventoryTransactionDates.sql', CONVERT(char(64), HASHBYTES('SHA2_256', N'013_AddInventoryTransactionDates.sql'), 2), SYSUTCDATETIME());
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
