SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'TTSmart_Control_V1_Test' THROW 51600,N'Migration option chi duoc phep chay tren TTSmart_Control_V1_Test.',1;

DECLARE @LockResult int;
EXEC @LockResult=sys.sp_getapplock @Resource=N'TTSmart.ControlPlane.V1.Schema',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=60000;
IF @LockResult<0 THROW 51604,N'Khong lay duoc khoa baseline ControlPlane.',1;
BEGIN TRY
 IF NOT EXISTS (SELECT 1 FROM dbo.DatabaseInfo WHERE DatabaseKind=N'ControlPlane') THROW 51601,N'DatabaseInfo khong phai ControlPlane.',1;
 IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=6 AND ScriptChecksum<>'$(ScriptChecksum)') THROW 51602,N'Checksum migration ControlPlane/006 khong khop.',1;
 IF EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=6)
 BEGIN
  IF EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND (is_auto_close_on=1 OR is_auto_shrink_on=1 OR recovery_model_desc<>N'SIMPLE' OR page_verify_option_desc<>N'CHECKSUM')) THROW 51603,N'Database option drift duoc phat hien.',1;
  IF EXISTS(SELECT 1 FROM sys.database_query_store_options WHERE actual_state_desc NOT IN(N'READ_WRITE',N'READ_ONLY')) THROW 51605,N'Query Store drift duoc phat hien.',1;
  EXEC sys.sp_releaseapplock @Resource=N'TTSmart.ControlPlane.V1.Schema',@LockOwner=N'Session';
  RETURN;
 END;
 IF NOT EXISTS (SELECT 1 FROM dbo.SchemaVersions WHERE ModuleCode=N'ControlPlane' AND MigrationNumber=5) THROW 51606,N'Thieu migration ControlPlane/005.',1;
 ALTER DATABASE CURRENT SET AUTO_CLOSE OFF;
 ALTER DATABASE CURRENT SET AUTO_SHRINK OFF;
 ALTER DATABASE CURRENT SET PAGE_VERIFY CHECKSUM;
 ALTER DATABASE CURRENT SET RECOVERY SIMPLE;
 ALTER DATABASE CURRENT SET QUERY_STORE = ON;
 BEGIN TRANSACTION;
 CREATE INDEX IX_Companies_Status ON dbo.Companies(Status,NormalizedCompanyCode);
 CREATE INDEX IX_Branches_Company_Status ON dbo.Branches(CompanyId,Status);
 CREATE INDEX IX_CompanyUsers_User_Status ON dbo.CompanyUsers(UserId,Status);
 CREATE INDEX IX_BranchUsers_CompanyUser_Status ON dbo.BranchUsers(CompanyUserId,Status);
 CREATE INDEX IX_AiReservations_Balance_Expiry ON dbo.AiReservations(AiBalanceId,Status,ExpiresAtUtc);
 CREATE INDEX IX_AiUsageLogs_Company_Occurred ON dbo.AiUsageLogs(CompanyId,OccurredAtUtc DESC);
 CREATE INDEX IX_DatabaseHealthChecks_Database_Checked ON dbo.DatabaseHealthChecks(ManagedDatabaseId,CheckedAtUtc DESC);
 CREATE INDEX IX_ProvisioningSteps_Job_Status ON dbo.ProvisioningSteps(ProvisioningJobId,Status,StepNumber);
 CREATE INDEX IX_AuditLogs_Company_Occurred ON dbo.AuditLogs(CompanyId,OccurredAtUtc DESC);
 CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs(EntityType,EntityId,OccurredAtUtc DESC);
 INSERT dbo.SchemaVersions(SchemaVersionId,ModuleCode,MigrationNumber,MigrationName,ScriptChecksum) VALUES(NEWID(),N'ControlPlane',6,N'006_CreateIndexesAndDatabaseOptions.sql','$(ScriptChecksum)');
 COMMIT TRANSACTION;
 EXEC sys.sp_releaseapplock @Resource=N'TTSmart.ControlPlane.V1.Schema',@LockOwner=N'Session';
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 EXEC sys.sp_releaseapplock @Resource=N'TTSmart.ControlPlane.V1.Schema',@LockOwner=N'Session';
 THROW;
END CATCH;
