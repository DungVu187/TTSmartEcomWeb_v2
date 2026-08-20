SET NOCOUNT ON; SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Operational_V1_Test' OR NOT EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=12) THROW 59300,N'Thiếu tiền điều kiện.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=13 AND ScriptChecksum='$(ScriptChecksum)') RETURN;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=13) THROW 59301,N'Checksum không khớp.',1;
BEGIN TRANSACTION;
DECLARE @OperationalMigrationLockResult int;
EXEC @OperationalMigrationLockResult=sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.Baseline',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=60000;
IF @OperationalMigrationLockResult<0 THROW 59390,N'Không lấy được application lock Operational v1.',1;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=13 AND ScriptChecksum='$(ScriptChecksum)') BEGIN COMMIT; RETURN; END;
IF EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'Operational' AND MigrationNumber=13) THROW 59301,N'Checksum không khớp.',1;
CREATE TABLE dbo.ChipSettings (
    ChipSettingId uniqueidentifier NOT NULL CONSTRAINT PK_ChipSettings PRIMARY KEY,
    PublicId char(24) COLLATE Latin1_General_100_BIN2 NOT NULL,
    SourceVersion int NOT NULL CONSTRAINT DF_ChipSettings_Version DEFAULT 0,
    MigratedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_ChipSettings_Migrated DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_ChipSettings_PublicId UNIQUE(PublicId),
    CONSTRAINT CK_ChipSettings_PublicId CHECK(PublicId NOT LIKE '%[^0-9a-f]%' AND LEN(PublicId)=24),
    CONSTRAINT CK_ChipSettings_Version CHECK(SourceVersion>=0)
);
INSERT dbo.SchemaVersions SELECT NEWID(),N'Operational',13,N'013_CreateChipSettingsTables.sql','$(ScriptChecksum)',SYSUTCDATETIME(),ORIGINAL_LOGIN(); COMMIT;
GO
