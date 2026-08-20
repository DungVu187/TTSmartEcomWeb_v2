/* Migration 008: đưa Product/Customer ra khỏi database tổng theo ownership đã chốt. */
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
IF DB_NAME() <> N'ttsmart.com.vn'
    THROW 51080, N'Script phải chạy trên [ttsmart.com.vn].', 1;
IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 8)
BEGIN
    PRINT N'Migration 008 đã được áp dụng; không có thay đổi.';
    RETURN;
END;
IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 7)
    THROW 51081, N'Chưa áp dụng migration 007.', 1;

DECLARE @SalesTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
INSERT @SalesTables (TableName) VALUES
    (N'Brands'), (N'ProductTypes'), (N'Categories'), (N'Units'), (N'Products'),
    (N'ProductCategories'), (N'ProductVariants'), (N'Customers'), (N'CustomerContacts'), (N'CustomerAddresses');

IF (SELECT COUNT(*) FROM sys.tables t JOIN @SalesTables st ON st.TableName = t.name WHERE t.schema_id = SCHEMA_ID(N'dbo')) <> 10
    THROW 51082, N'Không tìm thấy đủ mười bảng nghiệp vụ dự kiến; dừng để tránh drop sai.', 1;
IF EXISTS (
    SELECT 1 FROM sys.partitions p JOIN sys.tables t ON t.object_id = p.object_id
    JOIN @SalesTables st ON st.TableName = t.name
    WHERE t.schema_id = SCHEMA_ID(N'dbo') AND p.index_id IN (0, 1) AND p.rows > 0)
    THROW 51083, N'Có dữ liệu nghiệp vụ trong bảng cần loại; không được drop.', 1;
IF EXISTS (
    SELECT 1 FROM sys.foreign_keys fk JOIN @SalesTables st ON st.TableName = OBJECT_NAME(fk.referenced_object_id)
    WHERE OBJECT_NAME(fk.parent_object_id) NOT IN (SELECT TableName FROM @SalesTables))
    THROW 51084, N'Phát hiện foreign key từ đối tượng ngoài danh sách; không được drop.', 1;
IF EXISTS (
    SELECT 1 FROM sys.sql_expression_dependencies d JOIN sys.objects o ON o.object_id = d.referencing_id
    JOIN @SalesTables st ON st.TableName = d.referenced_entity_name
    WHERE o.is_ms_shipped = 0 AND o.type IN (N'V', N'P', N'FN', N'TF', N'IF'))
    THROW 51085, N'Phát hiện module SQL phụ thuộc vào bảng cần loại; không được drop.', 1;
IF EXISTS (
    SELECT 1 FROM sys.triggers tr JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN @SalesTables st ON st.TableName = t.name WHERE t.schema_id = SCHEMA_ID(N'dbo'))
    THROW 51086, N'Phát hiện trigger trên bảng cần loại; không được drop.', 1;

BEGIN TRANSACTION;
DROP TABLE dbo.ProductCategories;
DROP TABLE dbo.ProductVariants;
DROP TABLE dbo.CustomerContacts;
DROP TABLE dbo.CustomerAddresses;
DROP TABLE dbo.Products;
DROP TABLE dbo.Customers;
ALTER TABLE dbo.Categories DROP CONSTRAINT FK_Categories_Company_ParentCategory;
DROP TABLE dbo.Categories;
DROP TABLE dbo.ProductTypes;
DROP TABLE dbo.Brands;
DROP TABLE dbo.Units;

INSERT dbo.SchemaVersions (SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum, AppliedBy)
VALUES (NEWID(), 8, N'008_RemoveSalesTables.sql', NULL, ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
