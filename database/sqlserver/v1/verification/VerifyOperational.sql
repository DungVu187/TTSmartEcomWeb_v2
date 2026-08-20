SET NOCOUNT ON;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' THROW 59200,N'Chỉ chạy trên TTSmart_Operational_V1_Test.',1;
IF (SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational') <> 11 THROW 59201,N'Thiếu migration Operational.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE DatabaseKind=N'TTSmart' AND BranchId IS NULL) THROW 59207,N'DatabaseInfo không đúng Operational TTSmart test.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ScriptChecksum IS NULL OR LEN(ScriptChecksum)<>64) THROW 59202,N'Checksum migration không hợp lệ.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1) THROW 59203,N'FK bị vô hiệu hoặc không trusted.',1;
IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1) THROW 59204,N'CHECK bị vô hiệu hoặc không trusted.',1;
IF EXISTS(SELECT 1 FROM sys.columns c JOIN sys.types t ON c.user_type_id=t.user_type_id WHERE OBJECT_SCHEMA_NAME(c.object_id)=N'dbo' AND t.name IN(N'money',N'smallmoney',N'float',N'real',N'text',N'ntext',N'image')) THROW 59205,N'Có kiểu dữ liệu bị cấm.',1;
IF EXISTS(SELECT 1 FROM sys.columns WHERE OBJECT_SCHEMA_NAME(object_id)=N'dbo' AND ((name LIKE N'%Password%' AND name NOT IN(N'PasswordHash') AND name NOT LIKE N'%Id') OR (name LIKE N'%Token%' AND name NOT IN(N'TokenHash') AND name NOT LIKE N'%Id'))) THROW 59206,N'Phát hiện cột credential không được allowlist.',1;
IF EXISTS(SELECT 1 FROM sys.key_constraints WHERE LEFT(name,4) IN(N'PK__',N'UQ__')) OR EXISTS(SELECT 1 FROM sys.foreign_keys WHERE LEFT(name,4)=N'FK__') OR EXISTS(SELECT 1 FROM sys.check_constraints WHERE LEFT(name,4)=N'CK__') OR EXISTS(SELECT 1 FROM sys.default_constraints WHERE LEFT(name,4)=N'DF__') THROW 59208,N'Phát hiện constraint tên tự sinh.',1;
IF EXISTS(SELECT 1 FROM sys.tables t WHERE t.schema_id=SCHEMA_ID(N'dbo') AND NOT EXISTS(SELECT 1 FROM sys.indexes i WHERE i.object_id=t.object_id AND i.index_id=1)) THROW 59209,N'Phát hiện heap không được giải thích.',1;
IF EXISTS(SELECT 1 FROM sys.databases WHERE name=DB_NAME() AND (is_auto_close_on=1 OR is_auto_shrink_on=1 OR page_verify_option_desc<>N'CHECKSUM' OR recovery_model_desc<>N'SIMPLE' OR is_read_committed_snapshot_on=1)) THROW 59210,N'Database options Operational test không đúng baseline.',1;
DECLARE @ConstraintTable nvarchar(517),@ConstraintSql nvarchar(max);
DECLARE OperationalConstraintTables CURSOR LOCAL FAST_FORWARD FOR
SELECT QUOTENAME(SCHEMA_NAME(schema_id))+N'.'+QUOTENAME(name)
FROM sys.tables
WHERE is_ms_shipped=0 AND schema_id=SCHEMA_ID(N'dbo')
ORDER BY name;
OPEN OperationalConstraintTables;
FETCH NEXT FROM OperationalConstraintTables INTO @ConstraintTable;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @ConstraintSql=N'DBCC CHECKCONSTRAINTS (N'''+REPLACE(@ConstraintTable,N'''',N'''''')+N''') WITH ALL_CONSTRAINTS;';
    EXEC sys.sp_executesql @ConstraintSql;
    FETCH NEXT FROM OperationalConstraintTables INTO @ConstraintTable;
END;
CLOSE OperationalConstraintTables;
DEALLOCATE OperationalConstraintTables;
DBCC CHECKDB (N'TTSmart_Operational_V1_Test') WITH PHYSICAL_ONLY, NO_INFOMSGS;
SELECT N'Operational v1 verification passed' AS Result, (SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo')) AS TableCount;
GO
