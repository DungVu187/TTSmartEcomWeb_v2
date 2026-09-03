SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60101, N'Script Company đang kết nối sai database.', 1;

DECLARE @CompanyId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(CompanyId)');
IF @CompanyId IS NULL OR @CompanyId = CONVERT(uniqueidentifier, 0x0)
    THROW 60102, N'CompanyId không hợp lệ.', 1;
IF N'$(CompanyCode)' NOT LIKE N'[A-Za-z0-9]%'
    THROW 60103, N'CompanyCode không hợp lệ.', 1;
IF LEN(N'$(ScriptChecksum)') <> 64 OR N'$(ScriptChecksum)' LIKE N'%[^0-9A-F]%'
    THROW 60104, N'ScriptChecksum SHA-256 không hợp lệ.', 1;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.CompanyBranchSplit.Company',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60105, N'Không lấy được application lock Company.', 1;

IF OBJECT_ID(N'dbo.CompanyDatabaseInfo', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanyDatabaseInfo
    (
        CompanyDatabaseInfoId uniqueidentifier NOT NULL,
        SingletonKey tinyint NOT NULL,
        CompanyId uniqueidentifier NOT NULL,
        CompanyCode nvarchar(64) NOT NULL,
        DatabaseKind nvarchar(40) NOT NULL,
        SchemaVersion nvarchar(100) NOT NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyDatabaseInfo_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CompanyDatabaseInfo_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        RowVersion rowversion NOT NULL,
        CONSTRAINT PK_CompanyDatabaseInfo PRIMARY KEY CLUSTERED (CompanyDatabaseInfoId),
        CONSTRAINT UQ_CompanyDatabaseInfo_Singleton UNIQUE (SingletonKey),
        CONSTRAINT CK_CompanyDatabaseInfo_Singleton CHECK (SingletonKey = 1),
        CONSTRAINT CK_CompanyDatabaseInfo_Kind CHECK (DatabaseKind = N'CompanyShared')
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.CompanyDatabaseInfo WHERE SingletonKey = 1)
BEGIN
    INSERT dbo.CompanyDatabaseInfo
        (CompanyDatabaseInfoId, SingletonKey, CompanyId, CompanyCode, DatabaseKind, SchemaVersion)
    VALUES
        (NEWID(), 1, @CompanyId, N'$(CompanyCode)', N'CompanyShared', N'company-split-v1');
END
ELSE IF EXISTS
(
    SELECT 1
    FROM dbo.CompanyDatabaseInfo
    WHERE SingletonKey = 1
      AND (CompanyId <> @CompanyId OR CompanyCode <> N'$(CompanyCode)' OR DatabaseKind <> N'CompanyShared')
)
    THROW 60106, N'CompanyDatabaseInfo hiện hữu không khớp topology yêu cầu.', 1;

IF OBJECT_ID(N'dbo.DatabaseSplitIssues', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DatabaseSplitIssues
    (
        DatabaseSplitIssueId uniqueidentifier NOT NULL,
        IssueCode nvarchar(100) NOT NULL,
        SourceTable nvarchar(256) NOT NULL,
        AffectedRows bigint NOT NULL,
        Status nvarchar(40) NOT NULL,
        SafeDetail nvarchar(1000) NULL,
        CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_DatabaseSplitIssues_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DatabaseSplitIssues PRIMARY KEY CLUSTERED (DatabaseSplitIssueId),
        CONSTRAINT UQ_DatabaseSplitIssues_Code UNIQUE (IssueCode),
        CONSTRAINT CK_DatabaseSplitIssues_Rows CHECK (AffectedRows >= 0),
        CONSTRAINT CK_DatabaseSplitIssues_Status CHECK (Status IN (N'Open', N'Resolved'))
    );
END;

DECLARE @UnresolvedFiles bigint = CASE WHEN OBJECT_ID(N'dbo.Files', N'U') IS NULL THEN 0 ELSE (SELECT COUNT_BIG(*) FROM dbo.Files WHERE OwnerType IS NULL) END;
IF @UnresolvedFiles > 0 AND NOT EXISTS (SELECT 1 FROM dbo.DatabaseSplitIssues WHERE IssueCode = N'FILE_OWNERSHIP_UNRESOLVED')
BEGIN
    INSERT dbo.DatabaseSplitIssues
        (DatabaseSplitIssueId, IssueCode, SourceTable, AffectedRows, Status, SafeDetail)
    VALUES
        (NEWID(), N'FILE_OWNERSHIP_UNRESOLVED', N'dbo.Files', @UnresolvedFiles, N'Open',
         N'File metadata nguồn không có OwnerType; file được giữ tại Branch DB, URL sản phẩm vẫn được bảo toàn trong Product Master.');
END;

DELETE FROM dbo.ActivityLogs
WHERE Action IN (N'update_user_permissions', N'delete_user');

DELETE mapping
FROM dbo.MigrationMappings AS mapping
WHERE mapping.TargetTable NOT IN
(
    N'Brands', N'Categories', N'ProductTypes', N'Products', N'ProductVariants', N'ActivityLogs', N'LegacyRecords'
)
OR (mapping.TargetTable = N'ActivityLogs'
    AND NOT EXISTS (SELECT 1 FROM dbo.ActivityLogs AS activity WHERE activity.ActivityLogId = mapping.TargetId));

DELETE record
FROM dbo.LegacyRecords AS record
WHERE record.SourceCollection NOT IN (N'brands', N'types', N'sections', N'products', N'activitylogs')
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
WHERE SourceCollection NOT IN (N'brands', N'types', N'sections', N'products', N'activitylogs');

DROP TABLE IF EXISTS dbo.CartItems;
DROP TABLE IF EXISTS dbo.UserStations;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.InventoryOrderItems;
DROP TABLE IF EXISTS dbo.InventoryOrders;
DROP TABLE IF EXISTS dbo.SalesOrderItems;
DROP TABLE IF EXISTS dbo.SalesOrders;
DROP TABLE IF EXISTS dbo.StationProducts;
DROP TABLE IF EXISTS dbo.Stations;
DROP TABLE IF EXISTS dbo.StockMovementLines;
DROP TABLE IF EXISTS dbo.StockOperations;
DROP TABLE IF EXISTS dbo.NumberSequences;
DROP TABLE IF EXISTS dbo.ProductReviews;
DROP TABLE IF EXISTS dbo.Files;
DROP TABLE IF EXISTS dbo.Integrations;
DROP TABLE IF EXISTS dbo.StorefrontSettings;
DROP TABLE IF EXISTS dbo.VoiceSettings;

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10001 AND ScriptChecksum <> N'$(ScriptChecksum)')
    THROW 60107, N'Checksum drift của Company split migration.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 10001)
BEGIN
    INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
    VALUES (NEWID(), 10001, N'SplitCurrentDataToCompanySharedV1', N'$(ScriptChecksum)');
END;

COMMIT TRANSACTION;
