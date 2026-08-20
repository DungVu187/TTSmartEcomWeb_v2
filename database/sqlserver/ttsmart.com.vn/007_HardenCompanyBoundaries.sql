/*
    Migration 007: hoàn thiện ràng buộc cùng-company cho dữ liệu catalog,
    branch membership và usage AI. Script giả định database mới tạo, chưa seed dữ liệu.
*/
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
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 7)
BEGIN
    PRINT N'Migration 007 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 6)
    THROW 51007, N'Chưa áp dụng migration 006.', 1;
IF EXISTS (SELECT 1 FROM dbo.Products)
   OR EXISTS (SELECT 1 FROM dbo.ProductCategories)
   OR EXISTS (SELECT 1 FROM dbo.BranchUsers)
   OR EXISTS (SELECT 1 FROM dbo.AiUsageLogs)
    THROW 51008, N'Migration 007 chỉ được chạy trước khi seed hoặc nhập dữ liệu; cần kế hoạch chuyển đổi riêng cho database có dữ liệu.', 1;

BEGIN TRANSACTION;

ALTER TABLE dbo.Branches ADD CONSTRAINT UQ_Branches_Company_Branch UNIQUE (CompanyId, BranchId);
ALTER TABLE dbo.CompanyUsers ADD CONSTRAINT UQ_CompanyUsers_Company_CompanyUser UNIQUE (CompanyId, CompanyUserId);
ALTER TABLE dbo.CompanyUsers ADD CONSTRAINT UQ_CompanyUsers_CompanyUser_User UNIQUE (CompanyUserId, UserId);

ALTER TABLE dbo.BranchUsers ADD CompanyId uniqueidentifier NOT NULL;
ALTER TABLE dbo.BranchUsers ADD CompanyUserId uniqueidentifier NOT NULL;
ALTER TABLE dbo.BranchUsers ADD CONSTRAINT FK_BranchUsers_Company_Branch FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches (CompanyId, BranchId);
ALTER TABLE dbo.BranchUsers ADD CONSTRAINT FK_BranchUsers_Company_CompanyUser FOREIGN KEY (CompanyId, CompanyUserId) REFERENCES dbo.CompanyUsers (CompanyId, CompanyUserId);
ALTER TABLE dbo.BranchUsers ADD CONSTRAINT FK_BranchUsers_CompanyUser_User FOREIGN KEY (CompanyUserId, UserId) REFERENCES dbo.CompanyUsers (CompanyUserId, UserId);
CREATE INDEX IX_BranchUsers_CompanyUser_Status ON dbo.BranchUsers (CompanyUserId, Status, IsDeleted);

ALTER TABLE dbo.Brands ADD CONSTRAINT UQ_Brands_Company_Brand UNIQUE (CompanyId, BrandId);
ALTER TABLE dbo.ProductTypes ADD CONSTRAINT UQ_ProductTypes_Company_ProductType UNIQUE (CompanyId, ProductTypeId);
ALTER TABLE dbo.Units ADD CONSTRAINT UQ_Units_Company_Unit UNIQUE (CompanyId, UnitId);
ALTER TABLE dbo.Categories ADD CONSTRAINT UQ_Categories_Company_Category UNIQUE (CompanyId, CategoryId);

ALTER TABLE dbo.Categories DROP CONSTRAINT FK_Categories_ParentCategories;
ALTER TABLE dbo.Categories ADD CONSTRAINT FK_Categories_Company_ParentCategory FOREIGN KEY (CompanyId, ParentCategoryId) REFERENCES dbo.Categories (CompanyId, CategoryId);

ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Brands;
ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_ProductTypes;
ALTER TABLE dbo.Products DROP CONSTRAINT FK_Products_Units;
ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Company_Brands FOREIGN KEY (CompanyId, BrandId) REFERENCES dbo.Brands (CompanyId, BrandId);
ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Company_ProductTypes FOREIGN KEY (CompanyId, ProductTypeId) REFERENCES dbo.ProductTypes (CompanyId, ProductTypeId);
ALTER TABLE dbo.Products ADD CONSTRAINT FK_Products_Company_Units FOREIGN KEY (CompanyId, DefaultUnitId) REFERENCES dbo.Units (CompanyId, UnitId);

ALTER TABLE dbo.ProductCategories ADD CompanyId uniqueidentifier NOT NULL;
ALTER TABLE dbo.ProductCategories DROP CONSTRAINT FK_ProductCategories_Products;
ALTER TABLE dbo.ProductCategories DROP CONSTRAINT FK_ProductCategories_Categories;
ALTER TABLE dbo.Products ADD CONSTRAINT UQ_Products_Company_Product UNIQUE (CompanyId, ProductId);
ALTER TABLE dbo.ProductCategories ADD CONSTRAINT FK_ProductCategories_Company_Products FOREIGN KEY (CompanyId, ProductId) REFERENCES dbo.Products (CompanyId, ProductId);
ALTER TABLE dbo.ProductCategories ADD CONSTRAINT FK_ProductCategories_Company_Categories FOREIGN KEY (CompanyId, CategoryId) REFERENCES dbo.Categories (CompanyId, CategoryId);
CREATE INDEX IX_ProductCategories_Company_Category_Product ON dbo.ProductCategories (CompanyId, CategoryId, ProductId) WHERE IsDeleted = 0;

ALTER TABLE dbo.AiUsageLogs DROP CONSTRAINT FK_AiUsageLogs_Branches;
ALTER TABLE dbo.AiUsageLogs ADD CONSTRAINT FK_AiUsageLogs_Company_Branches FOREIGN KEY (CompanyId, BranchId) REFERENCES dbo.Branches (CompanyId, BranchId);

ALTER TABLE dbo.BranchDatabases ADD CONSTRAINT CK_BranchDatabases_DatabaseNameCharacters CHECK (DatabaseName NOT LIKE N'%[^A-Za-z0-9_]%' AND SqlLoginName NOT LIKE N'%[^A-Za-z0-9_]%');

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 7, N'007_HardenCompanyBoundaries.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
