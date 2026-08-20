SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
BEGIN TRY
    EXEC sp_getapplock @Resource=N'TTSmart.StockHistorySourceTimestamps.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF COL_LENGTH(N'dbo.StockOperations', N'SourceCreatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.StockOperations ADD SourceCreatedAtUtc datetime2(7) NULL;');
    IF COL_LENGTH(N'dbo.StockOperations', N'SourceUpdatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.StockOperations ADD SourceUpdatedAtUtc datetime2(7) NULL;');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
