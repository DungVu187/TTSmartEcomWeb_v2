SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60201, N'Script Branch đang kết nối sai database.', 1;

DECLARE @CompanyId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(CompanyId)');
DECLARE @BranchId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(BranchId)');
IF @CompanyId IS NULL OR @CompanyId = CONVERT(uniqueidentifier, 0x0)
    THROW 60202, N'CompanyId không hợp lệ.', 1;
IF @BranchId IS NULL OR @BranchId = CONVERT(uniqueidentifier, 0x0)
    THROW 60203, N'BranchId không hợp lệ.', 1;
IF LEN(N'$(ScriptChecksum)') <> 64 OR N'$(ScriptChecksum)' LIKE N'%[^0-9A-F]%'
    THROW 60204, N'ScriptChecksum SHA-256 không hợp lệ.', 1;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.CompanyBranchSplit.Branch',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60205, N'Không lấy được application lock Branch.', 1;

IF OBJECT_ID(N'dbo.BranchDatabaseInfo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchDatabaseInfo
    (
        BranchDatabaseInfoId uniqueidentifier NOT NULL,
        SingletonKey tinyint NOT NULL,
        CompanyId uniqueidentifier NOT NULL,
        BranchId uniqueidentifier NOT NULL,
        CompanyCode nvarchar(64) NOT NULL,
        BranchCode nvarchar(64) NOT NULL,
        DatabaseKind nvarchar(40) NOT NULL,
        SchemaVersion nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchDatabaseInfo_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchDatabaseInfo_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_BranchDatabaseInfo PRIMARY KEY CLUSTERED (BranchDatabaseInfoId),
        CONSTRAINT UQ_BranchDatabaseInfo_Singleton UNIQUE (SingletonKey),
        CONSTRAINT CK_BranchDatabaseInfo_Singleton CHECK (SingletonKey = 1),
        CONSTRAINT CK_BranchDatabaseInfo_Kind CHECK (DatabaseKind = N'BranchOperational')
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.BranchDatabaseInfo WHERE SingletonKey = 1)
BEGIN
    INSERT dbo.BranchDatabaseInfo
        (BranchDatabaseInfoId, SingletonKey, CompanyId, BranchId, CompanyCode, BranchCode, DatabaseKind, SchemaVersion)
    VALUES
        (NEWID(), 1, @CompanyId, @BranchId, N'$(CompanyCode)', N'$(BranchCode)', N'BranchOperational', N'branch-split-v1');
END
ELSE IF EXISTS
(
    SELECT 1
    FROM dbo.BranchDatabaseInfo
    WHERE SingletonKey = 1
      AND (CompanyId <> @CompanyId OR BranchId <> @BranchId OR CompanyCode <> N'$(CompanyCode)'
           OR BranchCode <> N'$(BranchCode)' OR DatabaseKind <> N'BranchOperational')
)
    THROW 60206, N'BranchDatabaseInfo hiện hữu không khớp topology yêu cầu.', 1;

IF OBJECT_ID(N'dbo.BranchStockBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchStockBalances
    (
        ProductVariantId uniqueidentifier NOT NULL,
        ProductId uniqueidentifier NOT NULL,
        ProductPublicId char(24) NOT NULL,
        ProductVariantPublicId char(24) NOT NULL,
        VariantPosition int NOT NULL,
        QuantityForSale decimal(19,6) NULL,
        QuantityInStorage decimal(19,6) NULL,
        ProductCodeSnapshot nvarchar(200) NULL,
        ProductNameSnapshot nvarchar(1000) NULL,
        VariantNameSnapshot nvarchar(1000) NULL,
        SourceVersion bigint NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchStockBalances_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchStockBalances_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_BranchStockBalances PRIMARY KEY CLUSTERED (ProductVariantId),
        CONSTRAINT CK_BranchStockBalances_ProductPublicId CHECK
            (LEN(ProductPublicId) = 24 AND ProductPublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%'),
        CONSTRAINT CK_BranchStockBalances_VariantPublicId CHECK
            (LEN(ProductVariantPublicId) = 24 AND ProductVariantPublicId COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9a-f]%'),
        CONSTRAINT CK_BranchStockBalances_Position CHECK (VariantPosition >= 0),
        CONSTRAINT CK_BranchStockBalances_SourceVersion CHECK (SourceVersion >= 0)
    );
    CREATE INDEX IX_BranchStockBalances_ProductId ON dbo.BranchStockBalances(ProductId);
END;

INSERT dbo.BranchStockBalances
(
    ProductVariantId, ProductId, ProductPublicId, ProductVariantPublicId, VariantPosition,
    QuantityForSale, QuantityInStorage, ProductCodeSnapshot, ProductNameSnapshot,
    VariantNameSnapshot, SourceVersion
)
SELECT
    variant.ProductVariantId,
    variant.ProductId,
    product.PublicId,
    variant.PublicId,
    variant.SortOrder,
    variant.QuantityForSale,
    variant.QuantityInStorage,
    product.Code,
    product.Name,
    variant.Name,
    variant.Version
FROM dbo.ProductVariants AS variant
JOIN dbo.Products AS product ON product.ProductId = variant.ProductId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.BranchStockBalances AS balance
    WHERE balance.ProductVariantId = variant.ProductVariantId
);

IF (SELECT COUNT_BIG(*) FROM dbo.BranchStockBalances) <> (SELECT COUNT_BIG(*) FROM dbo.ProductVariants)
    THROW 60207, N'Không bảo toàn đủ ProductVariant vào BranchStockBalances.', 1;

DELETE FROM dbo.ActivityLogs
WHERE Action NOT IN (N'update_user_permissions', N'delete_user');

DELETE mapping
FROM dbo.MigrationMappings AS mapping
WHERE mapping.TargetTable IN (N'Brands', N'Categories', N'ProductTypes', N'Products', N'ProductVariants')
   OR (mapping.TargetTable = N'ActivityLogs'
       AND NOT EXISTS (SELECT 1 FROM dbo.ActivityLogs AS activity WHERE activity.ActivityLogId = mapping.TargetId));

DELETE record
FROM dbo.LegacyRecords AS record
WHERE record.SourceCollection IN (N'brands', N'types', N'sections', N'products')
   OR (record.SourceCollection = N'activitylogs'
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.MigrationMappings AS mapping
           WHERE mapping.SourceCollection = record.SourceCollection
             AND mapping.SourceKey = record.SourceKey
             AND mapping.SourcePath = record.SourcePath
             AND mapping.TargetTable = N'ActivityLogs'
       ));

DELETE FROM dbo.MigrationManifests
WHERE SourceCollection IN (N'brands', N'types', N'sections', N'products');

DROP TABLE IF EXISTS dbo.ProductReviews;
DROP TABLE IF EXISTS dbo.ProductVariants;
DROP TABLE IF EXISTS dbo.Products;
DROP TABLE IF EXISTS dbo.ProductOptions;
DROP TABLE IF EXISTS dbo.ProductTypes;
DROP TABLE IF EXISTS dbo.Categories;
DROP TABLE IF EXISTS dbo.Brands;

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10002 AND ScriptChecksum <> N'$(ScriptChecksum)')
    THROW 60208, N'Checksum drift của Branch split migration.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10002)
BEGIN
    INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
    VALUES (NEWID(), 10002, N'SplitCurrentDataToBranchOperationalV1', N'$(ScriptChecksum)');
END;

COMMIT TRANSACTION;
