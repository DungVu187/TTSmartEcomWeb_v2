SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=11) THROW 59200,N'Thiếu tiền điều kiện.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=12 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=12) THROW 59201,N'Checksum không khớp.',1;
BEGIN TRANSACTION;
DECLARE @OperationalMigrationLockResult int;
EXEC @OperationalMigrationLockResult=sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Baseline',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
IF @OperationalMigrationLockResult<0 THROW 59290,N'Không lấy được application lock Operational v1.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=12 AND ScriptChecksum='$(ScriptChecksum)') BEGIN COMMIT; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=12) THROW 59201,N'Checksum không khớp.',1;
CREATE TABLE dbo.LegacyCounters (
    LegacyCounterId uniqueidentifier NOT NULL CONSTRAINT PK_LegacyCounters PRIMARY KEY,
    PublicId char(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
    CounterKey nvarchar(200) NOT NULL,
    SequenceValue bigint NOT NULL,
    SourceVersion int NOT NULL CONSTRAINT DF_LegacyCounters_Version DEFAULT 0,
    MigratedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_LegacyCounters_Migrated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_LegacyCounters_PublicId UNIQUE(PublicId),
    CONSTRAINT UQ_LegacyCounters_Key UNIQUE(CounterKey),
    CONSTRAINT CK_LegacyCounters_PublicId CHECK(PublicId NOT LIKE '%[^0-9a-f]%' AND LEN(PublicId)=24),
    CONSTRAINT CK_LegacyCounters_Sequence CHECK(SequenceValue>=0),
    CONSTRAINT CK_LegacyCounters_Version CHECK(SourceVersion>=0)
);
INSERT dbo.SchemaVersions SELECT NEWID(),N'Operational',12,N'012_CreateLegacyCounterTables.sql','$(ScriptChecksum)',SYSUTCDATETIME(),ORIGINAL_LOGIN(); COMMIT;
GO
