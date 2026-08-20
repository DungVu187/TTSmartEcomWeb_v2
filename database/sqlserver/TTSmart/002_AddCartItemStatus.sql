/* Bổ sung trạng thái chọn hàng của giỏ: field legacy CartItem.status. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'TTSmart'
    THROW 51201, N'Script phải chạy trên database [TTSmart].', 1;
IF '$(ScriptChecksum)' LIKE '%[^0-9A-F]%'
    OR LEN('$(ScriptChecksum)') <> 64
    THROW 51202, N'Phải truyền SHA-256 viết hoa của script qua ScriptChecksum.', 1;

BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = N'TTSmart.SchemaMigration.002.AddCartItemStatus',
    @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 60000;
IF @lockResult < 0 THROW 51203, N'Không lấy được application lock cho schema migration.', 1;

IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 2)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 2 AND ScriptChecksum <> '$(ScriptChecksum)')
        THROW 51204, N'Checksum migration 002 không khớp.', 1;
    COMMIT TRANSACTION;
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE MigrationNumber = 1)
    THROW 51205, N'Thiếu baseline schema migration 001.', 1;

IF COL_LENGTH(N'dbo.CartItems', N'Status') IS NULL
    ALTER TABLE dbo.CartItems ADD Status bit NOT NULL CONSTRAINT DF_CartItems_Status DEFAULT 1 WITH VALUES;

INSERT dbo.SchemaVersions(SchemaVersionId, MigrationNumber, MigrationName, ScriptChecksum)
VALUES(NEWID(), 2, N'002_AddCartItemStatus.sql', '$(ScriptChecksum)');
COMMIT TRANSACTION;
