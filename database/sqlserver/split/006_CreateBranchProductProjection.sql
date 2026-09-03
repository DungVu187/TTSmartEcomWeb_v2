SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60601, N'Script projection đang kết nối sai Branch database.', 1;

DECLARE @CompanyId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(CompanyId)');
DECLARE @BranchId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(BranchId)');
IF @CompanyId IS NULL OR @BranchId IS NULL
    THROW 60602, N'CompanyId hoặc BranchId không hợp lệ.', 1;
IF LEN(N'$(ScriptChecksum)') <> 64 OR N'$(ScriptChecksum)' LIKE N'%[^0-9A-F]%'
    THROW 60603, N'ScriptChecksum SHA-256 không hợp lệ.', 1;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.Branch.ProductProjection',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60604, N'Không lấy được application lock Branch projection.', 1;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.BranchDatabaseInfo
    WHERE SingletonKey = 1 AND CompanyId = @CompanyId AND BranchId = @BranchId
      AND DatabaseKind = N'BranchOperational'
)
    THROW 60605, N'BranchDatabaseInfo không khớp phạm vi migration.', 1;

IF OBJECT_ID(N'dbo.BranchStockBalances', N'U') IS NULL
    THROW 60606, N'Thiếu BranchStockBalances; không thể tạo Branch projection.', 1;

IF OBJECT_ID(N'dbo.BranchProductVariants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchProductVariants
    (
        BranchProductVariantId uniqueidentifier NOT NULL,
        ProductId uniqueidentifier NOT NULL,
        ProductVariantId uniqueidentifier NOT NULL,
        Price decimal(19,4) NULL,
        PriceRaw nvarchar(100) NULL,
        ImportPrice decimal(19,4) NULL,
        ImportPriceRaw nvarchar(100) NULL,
        IsActive bit NOT NULL CONSTRAINT DF_BranchProductVariants_IsActive DEFAULT (1),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchProductVariants_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedByUserId uniqueidentifier NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_BranchProductVariants PRIMARY KEY CLUSTERED (BranchProductVariantId),
        CONSTRAINT UQ_BranchProductVariants_Variant UNIQUE (ProductVariantId),
        CONSTRAINT UQ_BranchProductVariants_Product_Variant UNIQUE (ProductId, ProductVariantId)
    );
    CREATE INDEX IX_BranchProductVariants_Product_Active
        ON dbo.BranchProductVariants(ProductId, IsActive, ProductVariantId);
END;

IF OBJECT_ID(N'dbo.BranchProductStatistics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BranchProductStatistics
    (
        ProductId uniqueidentifier NOT NULL,
        PurchaseCount bigint NOT NULL CONSTRAINT DF_BranchProductStatistics_PurchaseCount DEFAULT (0),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_BranchProductStatistics_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_BranchProductStatistics PRIMARY KEY CLUSTERED (ProductId),
        CONSTRAINT CK_BranchProductStatistics_PurchaseCount CHECK (PurchaseCount >= 0)
    );
END;

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10005 AND ScriptChecksum <> N'$(ScriptChecksum)')
    THROW 60607, N'Checksum drift của Branch Product projection migration.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10005)
BEGIN
    INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
    VALUES (NEWID(), 10005, N'CreateBranchProductProjection', N'$(ScriptChecksum)');
END;

COMMIT TRANSACTION;
