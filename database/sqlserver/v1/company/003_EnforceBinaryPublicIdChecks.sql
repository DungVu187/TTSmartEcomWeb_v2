SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Company_V1_Test' THROW 59623,N'Chi chay tren dung database Company v1 test.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Company' AND MigrationNumber=3 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Company' AND MigrationNumber=3) THROW 59624,N'Migration 003 checksum khong khop.',1;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult=sys.sp_getapplock @Resource=N'TTSmart.Company.V1.Baseline',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
IF @lockResult<0 THROW 59625,N'Khong lay duoc application lock Company v1.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Company' AND MigrationNumber=3 AND ScriptChecksum='$(ScriptChecksum)') BEGIN COMMIT; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Company' AND MigrationNumber=3) THROW 59624,N'Migration 003 checksum khong khop.',1;
ALTER TABLE dbo.Brands DROP CONSTRAINT CK_Brands_PublicId; ALTER TABLE dbo.Brands ADD CONSTRAINT CK_Brands_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
ALTER TABLE dbo.Categories DROP CONSTRAINT CK_Categories_PublicId; ALTER TABLE dbo.Categories ADD CONSTRAINT CK_Categories_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
ALTER TABLE dbo.Units DROP CONSTRAINT CK_Units_PublicId; ALTER TABLE dbo.Units ADD CONSTRAINT CK_Units_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
ALTER TABLE dbo.Products DROP CONSTRAINT CK_Products_PublicId; ALTER TABLE dbo.Products ADD CONSTRAINT CK_Products_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
ALTER TABLE dbo.ProductVariants DROP CONSTRAINT CK_ProductVariants_PublicId; ALTER TABLE dbo.ProductVariants ADD CONSTRAINT CK_ProductVariants_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
ALTER TABLE dbo.Files DROP CONSTRAINT CK_Files_PublicId; ALTER TABLE dbo.Files ADD CONSTRAINT CK_Files_PublicId CHECK(PublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%');
INSERT dbo.SchemaVersions(SchemaVersionId,ModuleCode,MigrationNumber,MigrationName,ScriptChecksum) VALUES(NEWID(),N'Company',3,N'EnforceBinaryPublicIdChecks','$(ScriptChecksum)');
COMMIT;
GO
