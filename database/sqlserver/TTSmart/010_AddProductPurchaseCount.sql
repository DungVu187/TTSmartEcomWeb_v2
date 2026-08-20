SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
BEGIN TRY
    EXEC sp_getapplock @Resource=N'TTSmart.ProductPurchaseCount.v1', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=60000;

    IF COL_LENGTH(N'dbo.Products', N'PurchaseCount') IS NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.Products ADD PurchaseCount bigint NOT NULL CONSTRAINT DF_Products_PurchaseCount DEFAULT 0;');
        EXEC(N'ALTER TABLE dbo.Products ADD CONSTRAINT CK_Products_PurchaseCount CHECK (PurchaseCount >= 0);');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
