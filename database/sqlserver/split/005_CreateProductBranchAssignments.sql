SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60501, N'Script assignment đang kết nối sai Company database.', 1;

DECLARE @CompanyId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(CompanyId)');
DECLARE @BranchId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(BranchId)');
IF @CompanyId IS NULL OR @CompanyId = CONVERT(uniqueidentifier, 0x0)
    THROW 60502, N'CompanyId không hợp lệ.', 1;
IF @BranchId IS NULL OR @BranchId = CONVERT(uniqueidentifier, 0x0)
    THROW 60503, N'BranchId không hợp lệ.', 1;
IF UPPER(N'$(BranchCode)') <> N'MAIN'
    THROW 60504, N'Migration bảo toàn ban đầu chỉ áp dụng cho Branch MAIN.', 1;
IF LEN(N'$(ScriptChecksum)') <> 64 OR N'$(ScriptChecksum)' LIKE N'%[^0-9A-F]%'
    THROW 60505, N'ScriptChecksum SHA-256 không hợp lệ.', 1;

IF OBJECT_ID(N'dbo.SchemaVersions', N'U') IS NULL
    THROW 60510, N'Thiếu SchemaVersions; phải chạy migration Company nền trước.', 1;
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10004 AND ISNULL(ScriptChecksum, N'') <> N'$(ScriptChecksum)')
    THROW 60509, N'Checksum drift của Product Branch assignment migration.', 1;
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10004 AND ScriptChecksum = N'$(ScriptChecksum)')
BEGIN
    SELECT CAST(0 AS bigint) AS ProductCount, CAST(0 AS bigint) AS ActiveBefore, CAST(0 AS bigint) AS ActiveAfter;
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.Company.ProductBranchAssignments',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60506, N'Không lấy được application lock assignment.', 1;

IF NOT EXISTS
(
    SELECT 1
    FROM dbo.CompanyDatabaseInfo
    WHERE SingletonKey = 1
      AND CompanyId = @CompanyId
      AND CompanyCode = N'$(CompanyCode)'
      AND DatabaseKind = N'CompanyShared'
)
    THROW 60507, N'CompanyDatabaseInfo không khớp Company cần migration.', 1;

IF OBJECT_ID(N'dbo.ProductBranchAssignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductBranchAssignments
    (
        ProductBranchAssignmentId uniqueidentifier NOT NULL,
        ProductId uniqueidentifier NOT NULL,
        BranchId uniqueidentifier NOT NULL,
        IsActive bit NOT NULL,
        AssignedAtUtc datetime2(7) NOT NULL,
        AssignedByUserId uniqueidentifier NULL,
        RevokedAtUtc datetime2(7) NULL,
        RevokedByUserId uniqueidentifier NULL,
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_ProductBranchAssignments PRIMARY KEY CLUSTERED (ProductBranchAssignmentId),
        CONSTRAINT UQ_ProductBranchAssignments_Product_Branch UNIQUE (ProductId, BranchId),
        CONSTRAINT FK_ProductBranchAssignments_Product FOREIGN KEY (ProductId) REFERENCES dbo.Products(ProductId),
        CONSTRAINT CK_ProductBranchAssignments_State CHECK
        (
            (IsActive = 1 AND RevokedAtUtc IS NULL AND RevokedByUserId IS NULL)
            OR (IsActive = 0 AND RevokedAtUtc IS NOT NULL)
        )
    );
    CREATE INDEX IX_ProductBranchAssignments_Branch_Active_Product
        ON dbo.ProductBranchAssignments(BranchId, IsActive, ProductId);
END;

DECLARE @ProductCount bigint = (SELECT COUNT_BIG(*) FROM dbo.Products WHERE IsDeleted = 0);
DECLARE @ActiveBefore bigint =
(
    SELECT COUNT_BIG(*)
    FROM dbo.ProductBranchAssignments a
    INNER JOIN dbo.Products p ON p.ProductId = a.ProductId
    WHERE a.BranchId = @BranchId AND a.IsActive = 1 AND p.IsDeleted = 0
);

INSERT dbo.ProductBranchAssignments
    (ProductBranchAssignmentId, ProductId, BranchId, IsActive, AssignedAtUtc, AssignedByUserId)
SELECT NEWID(), p.ProductId, @BranchId, 1, SYSUTCDATETIME(), NULL
FROM dbo.Products p
WHERE p.IsDeleted = 0
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.ProductBranchAssignments a
      WHERE a.ProductId = p.ProductId AND a.BranchId = @BranchId
  );

UPDATE a
SET IsActive = 1,
    AssignedAtUtc = SYSUTCDATETIME(),
    AssignedByUserId = NULL,
    RevokedAtUtc = NULL,
    RevokedByUserId = NULL
FROM dbo.ProductBranchAssignments a
INNER JOIN dbo.Products p ON p.ProductId = a.ProductId
WHERE a.BranchId = @BranchId AND a.IsActive = 0 AND p.IsDeleted = 0;

DECLARE @ActiveAfter bigint =
(
    SELECT COUNT_BIG(*)
    FROM dbo.ProductBranchAssignments a
    INNER JOIN dbo.Products p ON p.ProductId = a.ProductId
    WHERE a.BranchId = @BranchId AND a.IsActive = 1 AND p.IsDeleted = 0
);

IF @ActiveAfter <> @ProductCount OR @ActiveAfter < @ActiveBefore
    THROW 60508, N'Không bảo toàn đủ Product hiện hữu cho Branch MAIN.', 1;

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
VALUES (NEWID(), 10004, N'CreateProductBranchAssignmentsAndBackfillMain', N'$(ScriptChecksum)');

COMMIT TRANSACTION;

SELECT @ProductCount AS ProductCount, @ActiveBefore AS ActiveBefore, @ActiveAfter AS ActiveAfter;
