/* Migration 009: ràng buộc consistency nội bộ và index truy vấn trọng yếu. */
SET NOCOUNT ON; SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;
GO
USE [TTSmart];
GO
IF DB_NAME()<>N'TTSmart' THROW 51190,N'Script phải chạy trên [TTSmart].',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber=9) BEGIN PRINT N'Migration 009 đã được áp dụng; không có thay đổi.'; RETURN; END;
IF NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber=8) THROW 51191,N'Chưa áp dụng migration 008.',1;
BEGIN TRANSACTION;
ALTER TABLE dbo.ProductVariants ADD CONSTRAINT UQ_ProductVariants_Product_Variant UNIQUE(ProductId,ProductVariantId);
ALTER TABLE dbo.CartItems ADD CONSTRAINT FK_CartItems_ProductVariantPair FOREIGN KEY(ProductId,ProductVariantId) REFERENCES dbo.ProductVariants(ProductId,ProductVariantId);
ALTER TABLE dbo.SalesOrderItems ADD CONSTRAINT FK_SalesOrderItems_ProductVariantPair FOREIGN KEY(ProductId,ProductVariantId) REFERENCES dbo.ProductVariants(ProductId,ProductVariantId);
ALTER TABLE dbo.ImportOrderItems ADD CONSTRAINT CK_ImportOrderItems_Progress CHECK(ReceivedQuantity<=Quantity AND (StockAppliedQuantity IS NULL OR StockAppliedQuantity<=Quantity));
ALTER TABLE dbo.ExportOrderItems ADD CONSTRAINT CK_ExportOrderItems_Progress CHECK(ExportedQuantity<=Quantity AND (StockAppliedQuantity IS NULL OR StockAppliedQuantity<=Quantity));
ALTER TABLE dbo.ProductVariants ADD CONSTRAINT CK_ProductVariants_Prices CHECK((SalePrice IS NULL OR SalePrice>=0) AND (ImportPrice IS NULL OR ImportPrice>=0) AND (ProfitPercent IS NULL OR ProfitPercent>=-100));
CREATE INDEX IX_ImportOrders_Completed_Date ON dbo.ImportOrders(IsCompleted,CreatedAtUtc DESC);
CREATE INDEX IX_ExportOrders_Completed_Date ON dbo.ExportOrders(IsCompleted,CreatedAtUtc DESC);
CREATE INDEX IX_ImportOrderItems_Product ON dbo.ImportOrderItems(ProductId) WHERE ProductId IS NOT NULL;
CREATE INDEX IX_ExportOrderItems_Product ON dbo.ExportOrderItems(ProductId) WHERE ProductId IS NOT NULL;
CREATE INDEX IX_ProductVariants_Product ON dbo.ProductVariants(ProductId,IsDeleted);
INSERT dbo.SchemaVersions(SchemaVersionId,MigrationNumber,MigrationName,ScriptChecksum,AppliedBy) VALUES(NEWID(),9,N'009_HardenConstraintsAndIndexes.sql',NULL,ORIGINAL_LOGIN());
COMMIT TRANSACTION;
GO
