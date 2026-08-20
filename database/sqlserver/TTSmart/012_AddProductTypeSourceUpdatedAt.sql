SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
BEGIN TRY
    EXEC sp_getapplock @Resource=N'TTSmart.ProductTypeSourceUpdatedAt.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;
    IF COL_LENGTH(N'dbo.ProductTypes', N'SourceUpdatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE dbo.ProductTypes ADD SourceUpdatedAtUtc datetime2(7) NULL;');
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
