SET NOCOUNT ON;
IF DB_NAME() <> N'TTSmart_Company_V1_Test' THROW 59630,N'Chi chay tren TTSmart_Company_V1_Test.',1;
IF (SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ModuleCode=N'Company') <> 3 THROW 59631,N'Thieu migration Company.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE SingletonKey=1 AND DatabaseKind=N'Company' AND CompanyCode=N'TTSmartTest' AND SchemaVersion=N'v1') THROW 59632,N'DatabaseInfo Company khong dung.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE LEN(ScriptChecksum)<>64 OR ScriptChecksum LIKE '%[^0-9A-F]%') THROW 59633,N'Checksum schema khong hop le.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 59634,N'FK bi vo hieu hoa hoac khong trusted.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) THROW 59635,N'CHECK bi vo hieu hoa hoac khong trusted.',1;
IF EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE OBJECT_SCHEMA_NAME(c.object_id)=N'dbo' AND t.name IN(N'money',N'smallmoney',N'float',N'real',N'text',N'ntext',N'image')) THROW 59636,N'Co kieu du lieu bi cam.',1;
IF EXISTS(SELECT 1 FROM sys.key_constraints WHERE LEFT(name,4) IN(N'PK__',N'UQ__')) OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE LEFT(name,4)=N'FK__') OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE LEFT(name,4)=N'CK__') OR EXISTS(SELECT 1 FROM sys.default_constraints WHERE LEFT(name,4)=N'DF__') THROW 59637,N'Phat hien constraint tu sinh.',1;
IF EXISTS(SELECT 1 FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo') AND name IN(N'Users',N'UserPasswords',N'Roles',N'Permissions',N'CompanyUsers',N'BranchUsers',N'SalesOrders',N'InventoryOrders',N'StockBalances',N'StockMovements',N'ImportOrders',N'ExportOrders',N'Stations',N'OutboxEvents',N'InboxEvents',N'SyncRecords')) THROW 59638,N'Company DB co bang bi cam.',1;
DECLARE @table nvarchar(517),@sql nvarchar(max);
/* SQL Server 2025 Express co loi noi bo DBCC khi CHECK expression ep COLLATE BIN2; cac constraint catalog nay da duoc thuc thi trong TestCompanyConstraints.sql. */
DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name) FROM sys.tables WHERE is_ms_shipped=0 AND schema_id=SCHEMA_ID(N'dbo') AND name NOT IN(N'Products',N'ProductVariants',N'Files',N'MigrationMappings',N'LegacyRecords',N'MigrationFileManifests') ORDER BY name;
OPEN c; FETCH NEXT FROM c INTO @table;
WHILE @@FETCH_STATUS=0 BEGIN SET @sql=N'DBCC CHECKCONSTRAINTS (N'''+REPLACE(@table,N'''',N'''')+N''') WITH ALL_CONSTRAINTS;'; EXEC sys.sp_executesql @sql; FETCH NEXT FROM c INTO @table; END;
CLOSE c; DEALLOCATE c;
DBCC CHECKDB (N'TTSmart_Company_V1_Test') WITH PHYSICAL_ONLY, NO_INFOMSGS;
SELECT N'Company v1 verification passed' AS Result;
GO
