/*
  Cutover schema for the one local Operational database [TTSmart].
  This script is deliberately the only destructive script in this directory.
  It may only be run against the approved local SQL Server instance.
*/
USE [master];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
IF DB_ID(N'TTSmart') IS NOT NULL
BEGIN
    ALTER DATABASE [TTSmart] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TTSmart];
END;
GO
CREATE DATABASE [TTSmart];
GO
ALTER DATABASE [TTSmart] SET RECOVERY SIMPLE;
ALTER DATABASE [TTSmart] SET READ_COMMITTED_SNAPSHOT ON;
GO
USE [TTSmart];
GO
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock @Resource=N'TTSmart.Schema.30.v1', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=60000;
IF @lockResult < 0 THROW 51000, N'Không thể lấy application lock cho schema TTSmart.', 1;
BEGIN TRANSACTION;

CREATE TABLE dbo.SchemaVersions (
    SchemaVersionId uniqueidentifier NOT NULL PRIMARY KEY,
    MigrationNumber int NOT NULL UNIQUE,
    MigrationName nvarchar(200) NOT NULL UNIQUE,
    ScriptChecksum char(64) NOT NULL,
    AppliedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_SchemaVersions_AppliedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_SchemaVersions_Checksum CHECK (ScriptChecksum NOT LIKE '%[^0-9A-Fa-f]%')
);
CREATE TABLE dbo.NumberSequences (
    NumberSequenceId uniqueidentifier NOT NULL PRIMARY KEY,
    SequenceCode nvarchar(80) NOT NULL UNIQUE,
    NextValue bigint NOT NULL,
    Prefix nvarchar(40) NULL,
    Version bigint NOT NULL CONSTRAINT DF_NumberSequences_Version DEFAULT 0,
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_NumberSequences_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_NumberSequences_NextValue CHECK (NextValue >= 0),
    CONSTRAINT CK_NumberSequences_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Users (
    UserId uniqueidentifier NOT NULL PRIMARY KEY,
    PublicId char(24) NOT NULL UNIQUE,
    Name nvarchar(200) NULL, Phone nvarchar(50) NULL, Email nvarchar(320) NULL,
    PasswordHash nvarchar(500) NULL, Role nvarchar(80) NULL,
    FunctionsJson nvarchar(max) NULL, PermissionsJson nvarchar(max) NULL, AddressesJson nvarchar(max) NULL, OrderTemplatesJson nvarchar(max) NULL,
    StationIdsJson nvarchar(max) NULL, ResetOtpHash char(64) NULL, ResetOtpExpiresAtUtc datetime2(7) NULL, AutoLoginTokenHash char(64) NULL, PasswordChangedAtUtc datetime2(7) NULL,
    SourceCreatedAtUtc datetime2(7) NULL, SourceUpdatedAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Users_Version DEFAULT 0, IsDeleted bit NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT 0,
    RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Users_Version CHECK (Version >= 0)
);
CREATE UNIQUE INDEX UX_Users_Phone ON dbo.Users(Phone) WHERE Phone IS NOT NULL AND IsDeleted=0;
CREATE TABLE dbo.UserStations (
    UserStationId uniqueidentifier NOT NULL PRIMARY KEY, UserId uniqueidentifier NOT NULL, StationId uniqueidentifier NULL,
    SourceStationId char(24) NULL, SortOrder int NOT NULL CONSTRAINT DF_UserStations_SortOrder DEFAULT 0,
    Version bigint NOT NULL CONSTRAINT DF_UserStations_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_UserStations_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_UserStations_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.CartItems (
    CartItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, UserId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL, SourceProductId char(24) NULL,
    VariantIndex int NULL, Quantity decimal(19,6) NULL, Status bit NOT NULL CONSTRAINT DF_CartItems_Status DEFAULT 1, SortOrder int NOT NULL CONSTRAINT DF_CartItems_SortOrder DEFAULT 0,
    Version bigint NOT NULL CONSTRAINT DF_CartItems_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_CartItems_Users FOREIGN KEY(UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_CartItems_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Brands (
    BrandId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Name nvarchar(300) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Brands_Version DEFAULT 0, SourceJson nvarchar(max) NULL, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Brands_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.ProductTypes (
    ProductTypeId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Name nvarchar(300) NULL, Icon nvarchar(1000) NULL,
    Version bigint NOT NULL CONSTRAINT DF_ProductTypes_Version DEFAULT 0, SourceJson nvarchar(max) NULL, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_ProductTypes_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Categories (
    CategoryId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Name nvarchar(300) NULL, ImageUrl nvarchar(2000) NULL,
    ValuesJson nvarchar(max) NULL, Version bigint NOT NULL CONSTRAINT DF_Categories_Version DEFAULT 0, SourceJson nvarchar(max) NULL, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Categories_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.ProductOptions (
    ProductOptionId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, OptionType nvarchar(100) NULL, Value nvarchar(500) NULL,
    SortOrder int NOT NULL CONSTRAINT DF_ProductOptions_SortOrder DEFAULT 0, Version bigint NOT NULL CONSTRAINT DF_ProductOptions_Version DEFAULT 0,
    SourceJson nvarchar(max) NULL, RowVersion rowversion NOT NULL, CONSTRAINT CK_ProductOptions_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Products (
    ProductId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Name nvarchar(500) NULL, NameUnsigned nvarchar(500) NULL, Code nvarchar(200) NULL,
    BrandName nvarchar(300) NULL, TypeName nvarchar(300) NULL, CategoryName nvarchar(300) NULL, CategoryValue nvarchar(500) NULL,
    Description nvarchar(max) NULL, VatRaw nvarchar(200) NULL, Display bit NULL, Adjusted bit NULL, DetailsJson nvarchar(max) NULL, ImagesJson nvarchar(max) NULL, DocumentsJson nvarchar(max) NULL,
    SourceCreatedAtUtc datetime2(7) NULL, SourceUpdatedAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Products_Version DEFAULT 0, IsDeleted bit NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Products_Version CHECK (Version >= 0)
);
CREATE UNIQUE INDEX UX_Products_Code ON dbo.Products(Code) WHERE Code IS NOT NULL AND Code<>N'' AND IsDeleted=0;
CREATE TABLE dbo.ProductVariants (
    ProductVariantId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, ProductId uniqueidentifier NOT NULL,
    SortOrder int NOT NULL, Name nvarchar(500) NULL, Price decimal(19,4) NULL, PriceRaw nvarchar(200) NULL,
    ImportPrice decimal(19,4) NULL, ImportPriceRaw nvarchar(200) NULL, Vat decimal(19,4) NULL, VatRaw nvarchar(200) NULL,
    QuantityForSale decimal(19,6) NULL, QuantityInStorage decimal(19,6) NULL, DetailsJson nvarchar(max) NULL,
    Version bigint NOT NULL CONSTRAINT DF_ProductVariants_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_ProductVariants_Products FOREIGN KEY(ProductId) REFERENCES dbo.Products(ProductId),
    CONSTRAINT UQ_ProductVariants_Product_Sort UNIQUE(ProductId,SortOrder), CONSTRAINT CK_ProductVariants_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.ProductReviews (
    ProductReviewId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, ProductId uniqueidentifier NOT NULL,
    SortOrder int NOT NULL, Rating decimal(19,4) NULL, Content nvarchar(max) NULL, ReviewerName nvarchar(300) NULL,
    DetailsJson nvarchar(max) NULL, Version bigint NOT NULL CONSTRAINT DF_ProductReviews_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_ProductReviews_Products FOREIGN KEY(ProductId) REFERENCES dbo.Products(ProductId), CONSTRAINT CK_ProductReviews_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.SalesOrders (
    SalesOrderId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, OrderCode nvarchar(200) NULL,
    CustomerPhoneSnapshot nvarchar(50) NULL, CustomerNameSnapshot nvarchar(300) NULL, Total decimal(19,4) NULL, TotalRaw nvarchar(200) NULL,
    Status nvarchar(100) NULL, State nvarchar(100) NULL, Paid bit NULL, CompletedAtUtc datetime2(7) NULL,
    ImagesJson nvarchar(max) NULL, SourceCreatedAtUtc datetime2(7) NULL, SourceUpdatedAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_SalesOrders_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT CK_SalesOrders_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.SalesOrderItems (
    SalesOrderItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, SalesOrderId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL, SourceProductId char(24) NULL, VariantIndex int NULL,
    Quantity decimal(19,6) NULL, DetailsJson nvarchar(max) NULL, SortOrder int NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_SalesOrderItems_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_SalesOrderItems_SalesOrders FOREIGN KEY(SalesOrderId) REFERENCES dbo.SalesOrders(SalesOrderId), CONSTRAINT CK_SalesOrderItems_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.InventoryOrders (
    InventoryOrderId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Direction nvarchar(10) NOT NULL,
    OrderName nvarchar(300) NULL, Note nvarchar(max) NULL, UserName nvarchar(300) NULL, Total decimal(19,4) NULL, TotalRaw nvarchar(200) NULL,
    Status bit NULL, CompletedAtUtc datetime2(7) NULL, ImagesJson nvarchar(max) NULL, SourceCreatedAtUtc datetime2(7) NULL, SourceUpdatedAtUtc datetime2(7) NULL,
    Version bigint NOT NULL CONSTRAINT DF_InventoryOrders_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_InventoryOrders_Direction CHECK(Direction IN(N'Import',N'Export')), CONSTRAINT CK_InventoryOrders_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.InventoryOrderItems (
    InventoryOrderItemId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, InventoryOrderId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL, ProductVariantId uniqueidentifier NULL, SourceProductId char(24) NULL,
    Price decimal(19,4) NULL, PriceRaw nvarchar(200) NULL, Vat decimal(19,4) NULL, VatRaw nvarchar(200) NULL,
    Quantity decimal(19,6) NULL, ProgressQuantity decimal(19,6) NULL, StockAppliedQuantity decimal(19,6) NULL,
    Unit nvarchar(100) NULL, Note nvarchar(max) NULL, DetailsJson nvarchar(max) NULL, SortOrder int NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_InventoryOrderItems_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_InventoryOrderItems_InventoryOrders FOREIGN KEY(InventoryOrderId) REFERENCES dbo.InventoryOrders(InventoryOrderId), CONSTRAINT CK_InventoryOrderItems_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.StockOperations (
    StockOperationId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, OperationType nvarchar(100) NULL,
    SourceReference nvarchar(200) NULL, OccurredAtUtc datetime2(7) NULL, DetailsJson nvarchar(max) NULL,
    Version bigint NOT NULL CONSTRAINT DF_StockOperations_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT CK_StockOperations_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.StockMovementLines (
    StockMovementLineId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, StockOperationId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL, SourceProductId char(24) NULL, Quantity decimal(19,6) NULL, BalanceBefore decimal(19,6) NULL, BalanceAfter decimal(19,6) NULL,
    DetailsJson nvarchar(max) NULL, SortOrder int NOT NULL, Version bigint NOT NULL CONSTRAINT DF_StockMovementLines_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_StockMovementLines_StockOperations FOREIGN KEY(StockOperationId) REFERENCES dbo.StockOperations(StockOperationId), CONSTRAINT CK_StockMovementLines_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Stations (
    StationId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Name nvarchar(300) NULL, Code nvarchar(100) NULL,
    DetailsJson nvarchar(max) NULL, Version bigint NOT NULL CONSTRAINT DF_Stations_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT CK_Stations_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.StationProducts (
    StationProductId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, StationId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NULL, SourceProductId char(24) NULL, SortOrder int NOT NULL, DetailsJson nvarchar(max) NULL,
    Version bigint NOT NULL CONSTRAINT DF_StationProducts_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT FK_StationProducts_Stations FOREIGN KEY(StationId) REFERENCES dbo.Stations(StationId), CONSTRAINT CK_StationProducts_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.StorefrontSettings (
    StorefrontSettingsId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, ConfigurationJson nvarchar(max) NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_StorefrontSettings_Version DEFAULT 0, SourceUpdatedAtUtc datetime2(7) NULL, RowVersion rowversion NOT NULL, CONSTRAINT CK_StorefrontSettings_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.VoiceSettings (
    VoiceSettingsId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, ConfigurationJson nvarchar(max) NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_VoiceSettings_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT CK_VoiceSettings_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Integrations (
    IntegrationId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, IntegrationType nvarchar(50) NOT NULL,
    ConfigurationJson nvarchar(max) NOT NULL, SecretReference nvarchar(500) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Integrations_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT UQ_Integrations_Type UNIQUE(IntegrationType), CONSTRAINT CK_Integrations_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.ActivityLogs (
    ActivityLogId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, Action nvarchar(300) NULL, ActorName nvarchar(300) NULL,
    DetailsJson nvarchar(max) NULL, CreatedAtUtc datetime2(7) NULL, Version bigint NOT NULL CONSTRAINT DF_ActivityLogs_Version DEFAULT 0, RowVersion rowversion NOT NULL, CONSTRAINT CK_ActivityLogs_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.Files (
    FileId uniqueidentifier NOT NULL PRIMARY KEY, PublicId char(24) NOT NULL UNIQUE, StorageKey nvarchar(1000) NULL, FileName nvarchar(500) NULL,
    MimeType nvarchar(200) NULL, ByteLength bigint NULL, Sha256 char(64) NULL, SourceUrl nvarchar(2000) NULL,
    OwnerType nvarchar(100) NULL, OwnerPublicId char(24) NULL, DetailsJson nvarchar(max) NULL,
    Version bigint NOT NULL CONSTRAINT DF_Files_Version DEFAULT 0, RowVersion rowversion NOT NULL,
    CONSTRAINT CK_Files_StorageKey CHECK(StorageKey IS NULL OR (StorageKey NOT LIKE N'%..%' AND StorageKey NOT LIKE N'%:%' AND StorageKey NOT LIKE N'/%' AND StorageKey NOT LIKE N'\\%')),
    CONSTRAINT CK_Files_Version CHECK (Version >= 0)
);
CREATE TABLE dbo.MigrationRuns (
    MigrationRunId uniqueidentifier NOT NULL PRIMARY KEY, SourceSystem nvarchar(100) NOT NULL, SourceDatabase nvarchar(100) NOT NULL,
    SourceCollection nvarchar(100) NULL, Status nvarchar(30) NOT NULL, StartedAtUtc datetime2(7) NOT NULL, FinishedAtUtc datetime2(7) NULL,
    Summary nvarchar(max) NULL, RowVersion rowversion NOT NULL, CONSTRAINT CK_MigrationRuns_Status CHECK(Status IN(N'Running',N'Completed',N'Failed'))
);
CREATE TABLE dbo.MigrationMappings (
    MigrationMappingId uniqueidentifier NOT NULL PRIMARY KEY, MigrationRunId uniqueidentifier NOT NULL, SourceSystem nvarchar(100) NOT NULL,
    SourceDatabase nvarchar(100) NOT NULL, SourceCollection nvarchar(100) NOT NULL, SourceKey nvarchar(200) NOT NULL, SourceKeyType nvarchar(50) NOT NULL,
    SourcePath nvarchar(500) NOT NULL, MappingFingerprint char(64) NOT NULL UNIQUE, TargetTable nvarchar(128) NOT NULL, TargetId uniqueidentifier NULL,
    CONSTRAINT FK_MigrationMappings_Runs FOREIGN KEY(MigrationRunId) REFERENCES dbo.MigrationRuns(MigrationRunId)
);
CREATE TABLE dbo.MigrationManifests (
    MigrationManifestId uniqueidentifier NOT NULL PRIMARY KEY, MigrationRunId uniqueidentifier NOT NULL, SourceDatabase nvarchar(100) NOT NULL, SourceCollection nvarchar(100) NOT NULL,
    DocumentCount bigint NOT NULL, MappedCount bigint NOT NULL, OwnerExcludedCount bigint NOT NULL CONSTRAINT DF_MigrationManifests_OwnerExcluded DEFAULT 0,
    BlockedCount bigint NOT NULL CONSTRAINT DF_MigrationManifests_Blocked DEFAULT 0, SkippedCount bigint NOT NULL CONSTRAINT DF_MigrationManifests_Skipped DEFAULT 0,
    ErrorCount bigint NOT NULL CONSTRAINT DF_MigrationManifests_Error DEFAULT 0, FileCount bigint NOT NULL CONSTRAINT DF_MigrationManifests_File DEFAULT 0,
    ManifestChecksum char(64) NOT NULL, ProfiledAtUtc datetime2(7) NOT NULL, CONSTRAINT FK_MigrationManifests_Runs FOREIGN KEY(MigrationRunId) REFERENCES dbo.MigrationRuns(MigrationRunId),
    CONSTRAINT UQ_MigrationManifests_Run_Collection UNIQUE(MigrationRunId,SourceCollection)
);
CREATE TABLE dbo.MigrationIssues (
    MigrationIssueId uniqueidentifier NOT NULL PRIMARY KEY, MigrationRunId uniqueidentifier NOT NULL, SourcePath nvarchar(500) NOT NULL,
    IssueCode nvarchar(100) NOT NULL, Severity nvarchar(20) NOT NULL, Status nvarchar(20) NOT NULL, SafeDetail nvarchar(2000) NULL,
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_MigrationIssues_Created DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_MigrationIssues_Runs FOREIGN KEY(MigrationRunId) REFERENCES dbo.MigrationRuns(MigrationRunId), CONSTRAINT CK_MigrationIssues_Status CHECK(Status IN(N'Open',N'Resolved'))
);
CREATE TABLE dbo.LegacyRecords (
    LegacyRecordId uniqueidentifier NOT NULL PRIMARY KEY, MigrationRunId uniqueidentifier NOT NULL, SourceDatabase nvarchar(100) NOT NULL,
    SourceCollection nvarchar(100) NOT NULL, SourceKey nvarchar(200) NOT NULL, SourceKeyType nvarchar(50) NOT NULL, SourcePath nvarchar(500) NOT NULL,
    SourceFingerprint char(64) NOT NULL UNIQUE, CanonicalExtendedJson nvarchar(max) NOT NULL, ContentSha256 char(64) NOT NULL, PreservationReason nvarchar(200) NOT NULL,
    CONSTRAINT FK_LegacyRecords_Runs FOREIGN KEY(MigrationRunId) REFERENCES dbo.MigrationRuns(MigrationRunId)
);

INSERT dbo.SchemaVersions(SchemaVersionId,MigrationNumber,MigrationName,ScriptChecksum)
VALUES(NEWID(),1,N'000_RecreateTTSmart30.sql',REPLICATE('0',64));
COMMIT TRANSACTION;
EXEC sys.sp_releaseapplock @Resource=N'TTSmart.Schema.30.v1', @LockOwner=N'Session';
GO
