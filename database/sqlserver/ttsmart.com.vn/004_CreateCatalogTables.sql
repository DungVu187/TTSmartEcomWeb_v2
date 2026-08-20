/* Migration 004: catalog sản phẩm và khách hàng dùng chung theo company. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO
USE [ttsmart.com.vn];
GO
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 4)
BEGIN
    PRINT N'Migration 004 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 3)
    THROW 51004, N'Chưa áp dụng migration 003.', 1;

BEGIN TRANSACTION;

CREATE TABLE dbo.Brands
(
    BrandId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    BrandCode nvarchar(100) NOT NULL,
    NormalizedBrandCode nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Brands_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Brands_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Brands_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Brands_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Brands_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Brands PRIMARY KEY CLUSTERED (BrandId),
    CONSTRAINT FK_Brands_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT CK_Brands_NormalizedBrandCode CHECK (NormalizedBrandCode = UPPER(LTRIM(RTRIM(BrandCode)))),
    CONSTRAINT CK_Brands_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Brands_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Brands_Company_NormalizedBrandCode ON dbo.Brands (CompanyId, NormalizedBrandCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Brands_Company_Name ON dbo.Brands (CompanyId, Name, IsDeleted);

CREATE TABLE dbo.ProductTypes
(
    ProductTypeId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    ProductTypeCode nvarchar(100) NOT NULL,
    NormalizedProductTypeCode nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    Status tinyint NOT NULL CONSTRAINT DF_ProductTypes_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_ProductTypes_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductTypes_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductTypes_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_ProductTypes_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_ProductTypes PRIMARY KEY CLUSTERED (ProductTypeId),
    CONSTRAINT FK_ProductTypes_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT CK_ProductTypes_NormalizedProductTypeCode CHECK (NormalizedProductTypeCode = UPPER(LTRIM(RTRIM(ProductTypeCode)))),
    CONSTRAINT CK_ProductTypes_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_ProductTypes_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_ProductTypes_Company_NormalizedCode ON dbo.ProductTypes (CompanyId, NormalizedProductTypeCode) WHERE IsDeleted = 0;

CREATE TABLE dbo.Categories
(
    CategoryId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    ParentCategoryId uniqueidentifier NULL,
    CategoryCode nvarchar(100) NOT NULL,
    NormalizedCategoryCode nvarchar(100) NOT NULL,
    Name nvarchar(200) NOT NULL,
    SortOrder int NOT NULL CONSTRAINT DF_Categories_SortOrder DEFAULT (0),
    Status tinyint NOT NULL CONSTRAINT DF_Categories_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Categories_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Categories_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Categories_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Categories_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId),
    CONSTRAINT FK_Categories_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_Categories_ParentCategories FOREIGN KEY (ParentCategoryId) REFERENCES dbo.Categories (CategoryId),
    CONSTRAINT CK_Categories_NormalizedCategoryCode CHECK (NormalizedCategoryCode = UPPER(LTRIM(RTRIM(CategoryCode)))),
    CONSTRAINT CK_Categories_ParentCategory CHECK (ParentCategoryId IS NULL OR ParentCategoryId <> CategoryId),
    CONSTRAINT CK_Categories_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Categories_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Categories_Company_NormalizedCode ON dbo.Categories (CompanyId, NormalizedCategoryCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Categories_Company_Parent ON dbo.Categories (CompanyId, ParentCategoryId, SortOrder, IsDeleted);

CREATE TABLE dbo.Units
(
    UnitId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    UnitCode nvarchar(50) NOT NULL,
    NormalizedUnitCode nvarchar(50) NOT NULL,
    Name nvarchar(100) NOT NULL,
    DecimalScale tinyint NOT NULL CONSTRAINT DF_Units_DecimalScale DEFAULT (0),
    Status tinyint NOT NULL CONSTRAINT DF_Units_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Units_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Units_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Units_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Units_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Units PRIMARY KEY CLUSTERED (UnitId),
    CONSTRAINT FK_Units_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT CK_Units_NormalizedUnitCode CHECK (NormalizedUnitCode = UPPER(LTRIM(RTRIM(UnitCode)))),
    CONSTRAINT CK_Units_DecimalScale CHECK (DecimalScale BETWEEN 0 AND 6),
    CONSTRAINT CK_Units_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Units_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Units_Company_NormalizedCode ON dbo.Units (CompanyId, NormalizedUnitCode) WHERE IsDeleted = 0;

CREATE TABLE dbo.Products
(
    ProductId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    BrandId uniqueidentifier NULL,
    ProductTypeId uniqueidentifier NULL,
    DefaultUnitId uniqueidentifier NOT NULL,
    ProductCode nvarchar(100) NOT NULL,
    NormalizedProductCode nvarchar(100) NOT NULL,
    Name nvarchar(300) NOT NULL,
    Description nvarchar(2000) NULL,
    ListPrice decimal(19,4) NULL,
    CurrencyCode char(3) NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Products_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Products_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Products_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Products_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (ProductId),
    CONSTRAINT FK_Products_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT FK_Products_Brands FOREIGN KEY (BrandId) REFERENCES dbo.Brands (BrandId),
    CONSTRAINT FK_Products_ProductTypes FOREIGN KEY (ProductTypeId) REFERENCES dbo.ProductTypes (ProductTypeId),
    CONSTRAINT FK_Products_Units FOREIGN KEY (DefaultUnitId) REFERENCES dbo.Units (UnitId),
    CONSTRAINT CK_Products_NormalizedProductCode CHECK (NormalizedProductCode = UPPER(LTRIM(RTRIM(ProductCode)))),
    CONSTRAINT CK_Products_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
    CONSTRAINT CK_Products_PriceCurrency CHECK ((ListPrice IS NULL AND CurrencyCode IS NULL) OR (ListPrice IS NOT NULL AND CurrencyCode IS NOT NULL)),
    CONSTRAINT CK_Products_CurrencyCode CHECK (CurrencyCode IS NULL OR CurrencyCode NOT LIKE '%[^A-Z]%'),
    CONSTRAINT CK_Products_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Products_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Products_Company_NormalizedCode ON dbo.Products (CompanyId, NormalizedProductCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Products_Company_Name ON dbo.Products (CompanyId, Name, Status, IsDeleted);

CREATE TABLE dbo.ProductCategories
(
    ProductCategoryId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    CategoryId uniqueidentifier NOT NULL,
    Version bigint NOT NULL CONSTRAINT DF_ProductCategories_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductCategories_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductCategories_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_ProductCategories_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_ProductCategories PRIMARY KEY CLUSTERED (ProductCategoryId),
    CONSTRAINT FK_ProductCategories_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
    CONSTRAINT FK_ProductCategories_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (CategoryId),
    CONSTRAINT CK_ProductCategories_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_ProductCategories_Product_Category_Active ON dbo.ProductCategories (ProductId, CategoryId) WHERE IsDeleted = 0;
CREATE INDEX IX_ProductCategories_Category_Product ON dbo.ProductCategories (CategoryId, ProductId) WHERE IsDeleted = 0;

CREATE TABLE dbo.ProductVariants
(
    ProductVariantId uniqueidentifier NOT NULL,
    ProductId uniqueidentifier NOT NULL,
    VariantCode nvarchar(100) NOT NULL,
    NormalizedVariantCode nvarchar(100) NOT NULL,
    Name nvarchar(300) NOT NULL,
    Barcode nvarchar(100) NULL,
    ListPrice decimal(19,4) NULL,
    CurrencyCode char(3) NULL,
    Status tinyint NOT NULL CONSTRAINT DF_ProductVariants_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_ProductVariants_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductVariants_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ProductVariants_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_ProductVariants_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_ProductVariants PRIMARY KEY CLUSTERED (ProductVariantId),
    CONSTRAINT FK_ProductVariants_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (ProductId),
    CONSTRAINT CK_ProductVariants_NormalizedVariantCode CHECK (NormalizedVariantCode = UPPER(LTRIM(RTRIM(VariantCode)))),
    CONSTRAINT CK_ProductVariants_PriceCurrency CHECK ((ListPrice IS NULL AND CurrencyCode IS NULL) OR (ListPrice IS NOT NULL AND CurrencyCode IS NOT NULL)),
    CONSTRAINT CK_ProductVariants_CurrencyCode CHECK (CurrencyCode IS NULL OR CurrencyCode NOT LIKE '%[^A-Z]%'),
    CONSTRAINT CK_ProductVariants_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_ProductVariants_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_ProductVariants_Product_NormalizedCode ON dbo.ProductVariants (ProductId, NormalizedVariantCode) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_ProductVariants_Barcode_Active ON dbo.ProductVariants (Barcode) WHERE Barcode IS NOT NULL AND IsDeleted = 0;

CREATE TABLE dbo.Customers
(
    CustomerId uniqueidentifier NOT NULL,
    CompanyId uniqueidentifier NOT NULL,
    CustomerCode nvarchar(100) NOT NULL,
    NormalizedCustomerCode nvarchar(100) NOT NULL,
    Name nvarchar(300) NOT NULL,
    CustomerType tinyint NOT NULL CONSTRAINT DF_Customers_CustomerType DEFAULT (1),
    TaxCode nvarchar(64) NULL,
    Status tinyint NOT NULL CONSTRAINT DF_Customers_Status DEFAULT (1),
    Version bigint NOT NULL CONSTRAINT DF_Customers_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_Customers_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (CustomerId),
    CONSTRAINT FK_Customers_Companies FOREIGN KEY (CompanyId) REFERENCES dbo.Companies (CompanyId),
    CONSTRAINT CK_Customers_NormalizedCustomerCode CHECK (NormalizedCustomerCode = UPPER(LTRIM(RTRIM(CustomerCode)))),
    CONSTRAINT CK_Customers_CustomerType CHECK (CustomerType IN (1, 2)),
    CONSTRAINT CK_Customers_Status CHECK (Status IN (0, 1, 2)),
    CONSTRAINT CK_Customers_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_Customers_Company_NormalizedCode ON dbo.Customers (CompanyId, NormalizedCustomerCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Customers_Company_Name ON dbo.Customers (CompanyId, Name, Status, IsDeleted);

CREATE TABLE dbo.CustomerContacts
(
    CustomerContactId uniqueidentifier NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    ContactType tinyint NOT NULL,
    DisplayValue nvarchar(320) NOT NULL,
    NormalizedValue nvarchar(320) NOT NULL,
    IsPrimary bit NOT NULL CONSTRAINT DF_CustomerContacts_IsPrimary DEFAULT (0),
    IsVerified bit NOT NULL CONSTRAINT DF_CustomerContacts_IsVerified DEFAULT (0),
    Version bigint NOT NULL CONSTRAINT DF_CustomerContacts_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CustomerContacts_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CustomerContacts_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_CustomerContacts_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_CustomerContacts PRIMARY KEY CLUSTERED (CustomerContactId),
    CONSTRAINT FK_CustomerContacts_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT CK_CustomerContacts_ContactType CHECK (ContactType IN (1, 2, 3)),
    CONSTRAINT CK_CustomerContacts_NormalizedValue CHECK (NormalizedValue = UPPER(LTRIM(RTRIM(DisplayValue)))),
    CONSTRAINT CK_CustomerContacts_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_CustomerContacts_Customer_Primary_Active ON dbo.CustomerContacts (CustomerId) WHERE IsPrimary = 1 AND IsDeleted = 0;
CREATE INDEX IX_CustomerContacts_NormalizedValue ON dbo.CustomerContacts (ContactType, NormalizedValue) WHERE IsDeleted = 0;

CREATE TABLE dbo.CustomerAddresses
(
    CustomerAddressId uniqueidentifier NOT NULL,
    CustomerId uniqueidentifier NOT NULL,
    Label nvarchar(100) NULL,
    RecipientName nvarchar(200) NOT NULL,
    ContactPhone nvarchar(32) NULL,
    AddressLine nvarchar(1000) NOT NULL,
    ProvinceCode nvarchar(32) NULL,
    IsDefault bit NOT NULL CONSTRAINT DF_CustomerAddresses_IsDefault DEFAULT (0),
    Version bigint NOT NULL CONSTRAINT DF_CustomerAddresses_Version DEFAULT (1),
    CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CustomerAddresses_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_CustomerAddresses_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    IsDeleted bit NOT NULL CONSTRAINT DF_CustomerAddresses_IsDeleted DEFAULT (0),
    RowVersion rowversion NOT NULL,
    CONSTRAINT PK_CustomerAddresses PRIMARY KEY CLUSTERED (CustomerAddressId),
    CONSTRAINT FK_CustomerAddresses_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (CustomerId),
    CONSTRAINT CK_CustomerAddresses_RecipientName CHECK (LEN(LTRIM(RTRIM(RecipientName))) > 0),
    CONSTRAINT CK_CustomerAddresses_AddressLine CHECK (LEN(LTRIM(RTRIM(AddressLine))) > 0),
    CONSTRAINT CK_CustomerAddresses_Version CHECK (Version > 0)
);
CREATE UNIQUE INDEX UX_CustomerAddresses_Customer_Default_Active ON dbo.CustomerAddresses (CustomerId) WHERE IsDefault = 1 AND IsDeleted = 0;

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 4, N'004_CreateCatalogTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
