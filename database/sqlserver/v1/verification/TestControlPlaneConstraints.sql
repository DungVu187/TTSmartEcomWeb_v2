SET NOCOUNT ON;
/* Expected constraint failures are caught; the outer test transaction is always rolled back. */
SET XACT_ABORT OFF;
IF DB_NAME() <> N'TTSmart_Control_V1_Test' THROW 51800,N'Test chi duoc phep chay tren database test ControlPlane.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.DatabaseInfo WHERE DatabaseKind=N'ControlPlane') THROW 51801,N'DatabaseInfo khong dung DatabaseKind.',1;

/* Mỗi ca kiểm tra trigger được tách transaction vì SQL Server đánh dấu transaction lỗi sau khi trigger từ chối lệnh. */
DECLARE @IsolatedRejected bit,@ICompany uniqueidentifier=NEWID(),@IBranch uniqueidentifier=NEWID(),@IUser uniqueidentifier=NEWID(),@ICompanyUser uniqueidentifier=NEWID(),@IRole uniqueidentifier=NEWID(),@IBranchUser uniqueidentifier=NEWID();
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_ROLE',N'ISO_ROLE',N'Isolated role',N'Isolated role');
INSERT dbo.Branches(BranchId,CompanyId,BranchCode,NormalizedBranchCode,Name) VALUES(@IBranch,@ICompany,N'ISO',N'ISO',N'Isolated branch');
INSERT dbo.Users(UserId,DisplayName,SecurityStamp) VALUES(@IUser,N'Isolated user',NEWID());
INSERT dbo.CompanyUsers(CompanyUserId,CompanyId,UserId) VALUES(@ICompanyUser,@ICompany,@IUser);
INSERT dbo.BranchUsers(BranchUserId,CompanyId,BranchId,CompanyUserId,UserId) VALUES(@IBranchUser,@ICompany,@IBranch,@ICompanyUser,@IUser);
INSERT dbo.Roles(RoleId,CompanyId,RoleCode,NormalizedRoleCode,Name,ScopeType) VALUES(@IRole,@ICompany,N'ISO_BRANCH',N'ISO_BRANCH',N'Isolated branch role',2);
INSERT dbo.BranchUserRoles(BranchUserRoleId,CompanyId,BranchUserId,RoleId) VALUES(NEWID(),@ICompany,@IBranchUser,@IRole);
SET @IsolatedRejected=0;
BEGIN TRY UPDATE dbo.Roles SET ScopeType=1 WHERE RoleId=@IRole; END TRY BEGIN CATCH SET @IsolatedRejected=1; END CATCH;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @IsolatedRejected=0 THROW 51811,N'Doi scope cua role da duoc gan khong bi chan.',1;

BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_SCOPE',N'ISO_SCOPE',N'Isolated scope',N'Isolated scope');
INSERT dbo.Users(UserId,DisplayName,SecurityStamp) VALUES(@IUser,N'Isolated user',NEWID());
INSERT dbo.CompanyUsers(CompanyUserId,CompanyId,UserId) VALUES(@ICompanyUser,@ICompany,@IUser);
INSERT dbo.Roles(RoleId,CompanyId,RoleCode,NormalizedRoleCode,Name,ScopeType) VALUES(@IRole,@ICompany,N'ISO_BRANCH_SCOPE',N'ISO_BRANCH_SCOPE',N'Isolated branch role',2);
SET @IsolatedRejected=0;
BEGIN TRY INSERT dbo.CompanyUserRoles(CompanyUserRoleId,CompanyId,CompanyUserId,RoleId) VALUES(NEWID(),@ICompany,@ICompanyUser,@IRole); END TRY BEGIN CATCH SET @IsolatedRejected=1; END CATCH;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @IsolatedRejected=0 THROW 51803,N'Gan Branch role vao CompanyUser khong bi chan.',1;

DECLARE @IFeature uniqueidentifier=NEWID();
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_FEATURE',N'ISO_FEATURE',N'Isolated feature',N'Isolated feature');
INSERT dbo.Branches(BranchId,CompanyId,BranchCode,NormalizedBranchCode,Name) VALUES(@IBranch,@ICompany,N'ISO',N'ISO',N'Isolated branch');
INSERT dbo.Features(FeatureId,FeatureCode,NormalizedFeatureCode,Name) VALUES(@IFeature,N'ISO_FEATURE',N'ISO_FEATURE',N'Isolated feature');
INSERT dbo.CompanyFeatures(CompanyFeatureId,CompanyId,FeatureId,IsEnabled) VALUES(NEWID(),@ICompany,@IFeature,0);
SET @IsolatedRejected=0;
BEGIN TRY INSERT dbo.BranchFeatures(BranchFeatureId,CompanyId,BranchId,FeatureId,IsEnabled) VALUES(NEWID(),@ICompany,@IBranch,@IFeature,1); END TRY BEGIN CATCH SET @IsolatedRejected=1; END CATCH;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @IsolatedRejected=0 THROW 51804,N'Branch feature khong entitlement Company khong bi chan.',1;

DECLARE @IBalance uniqueidentifier=NEWID();
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_AI',N'ISO_AI',N'Isolated AI',N'Isolated AI');
INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@IBalance,@ICompany,N'VOICE',10);
SET @IsolatedRejected=0;
EXECUTE AS USER=N'ControlPlaneRuntimeUser';
BEGIN TRY UPDATE dbo.AiBalances SET AvailableAmount=9 WHERE AiBalanceId=@IBalance; END TRY BEGIN CATCH SET @IsolatedRejected=1; END CATCH;
REVERT;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @IsolatedRejected=0 OR EXISTS(SELECT 1 FROM dbo.AiBalances WHERE AiBalanceId=@IBalance AND AvailableAmount=9) THROW 51816,N'Runtime user sua truc tiep AI ledger hoac de lai du lieu.',1;

BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_AI_REL',N'ISO_AI_REL',N'Isolated AI relation',N'Isolated AI relation');
INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@IBalance,@ICompany,N'VOICE',10);
SET @IsolatedRejected=0;
BEGIN TRY INSERT dbo.AiReservations(AiReservationId,AiBalanceId,CompanyId,IdempotencyKey,ReservedAmount,ExpiresAtUtc) VALUES(NEWID(),@IBalance,NEWID(),N'iso-wrong-balance',1,DATEADD(MINUTE,5,SYSUTCDATETIME())); END TRY BEGIN CATCH SET @IsolatedRejected=1; END CATCH;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @IsolatedRejected=0 THROW 51813,N'Reservation AI khac balance/company khong bi chan.',1;

DECLARE @IReservationResult TABLE(AiReservationId uniqueidentifier);
DECLARE @IReservation uniqueidentifier,@IExpiry datetime2(7)=DATEADD(MINUTE,5,SYSUTCDATETIME());
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_AI_USE',N'ISO_AI_USE',N'Isolated AI use',N'Isolated AI use');
INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@IBalance,@ICompany,N'VOICE',10);
INSERT @IReservationResult EXEC dbo.ReserveAiAmount @AiBalanceId=@IBalance,@CompanyId=@ICompany,@Amount=1,@IdempotencyKey=N'iso-ai-reserve',@ExpiresAtUtc=@IExpiry;
SET @IReservation=(SELECT AiReservationId FROM @IReservationResult);
EXEC dbo.FinalizeAiReservation @AiReservationId=@IReservation,@CompanyId=@ICompany,@TransactionType=2,@IdempotencyKey=N'iso-ai-consume';
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;

/* Retry cung key phai tra lai dung ket qua cu; khac tham so phai bi tu choi. */
DECLARE @IRetryResultOne TABLE(AiReservationId uniqueidentifier);
DECLARE @IRetryResultTwo TABLE(AiReservationId uniqueidentifier);
DECLARE @IFinalOne TABLE(AiTransactionId uniqueidentifier);
DECLARE @IFinalTwo TABLE(AiTransactionId uniqueidentifier);
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@ICompany,N'ISO_AI_RETRY',N'ISO_AI_RETRY',N'Isolated AI retry',N'Isolated AI retry');
INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@IBalance,@ICompany,N'VOICE',10);
INSERT @IRetryResultOne EXEC dbo.ReserveAiAmount @AiBalanceId=@IBalance,@CompanyId=@ICompany,@Amount=2,@IdempotencyKey=N'iso-ai-retry',@ExpiresAtUtc=@IExpiry;
INSERT @IRetryResultTwo EXEC dbo.ReserveAiAmount @AiBalanceId=@IBalance,@CompanyId=@ICompany,@Amount=2,@IdempotencyKey=N'iso-ai-retry',@ExpiresAtUtc=@IExpiry;
IF (SELECT AiReservationId FROM @IRetryResultOne)<>(SELECT AiReservationId FROM @IRetryResultTwo) THROW 51824,N'Retry reservation cung key khong tra lai reservation cu.',1;
SET @IReservation=(SELECT AiReservationId FROM @IRetryResultOne);
INSERT @IFinalOne EXEC dbo.FinalizeAiReservation @AiReservationId=@IReservation,@CompanyId=@ICompany,@TransactionType=2,@IdempotencyKey=N'iso-ai-final';
INSERT @IFinalTwo EXEC dbo.FinalizeAiReservation @AiReservationId=@IReservation,@CompanyId=@ICompany,@TransactionType=2,@IdempotencyKey=N'iso-ai-final';
IF (SELECT AiTransactionId FROM @IFinalOne)<>(SELECT AiTransactionId FROM @IFinalTwo) THROW 51826,N'Retry finalize cung key khong tra lai transaction cu.',1;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;

/* Bien idempotency duoc kiem tra o bien: 120 ky tu hop le, 121 va 128 bi tu choi thay vi bi cat ngam. */
DECLARE @LengthCompany uniqueidentifier=NEWID(),@LengthBalance uniqueidentifier=NEWID(),@LengthRejected bit=0,@Key120 nvarchar(128)=REPLICATE(N'k',120),@Key121 nvarchar(128)=REPLICATE(N'x',121),@Key128 nvarchar(128)=REPLICATE(N'y',128);
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@LengthCompany,N'ISO_AI_LENGTH',N'ISO_AI_LENGTH',N'Isolated AI length',N'Isolated AI length');
INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@LengthBalance,@LengthCompany,N'VOICE',10);
EXEC dbo.ReserveAiAmount @AiBalanceId=@LengthBalance,@CompanyId=@LengthCompany,@Amount=1,@IdempotencyKey=@Key120,@ExpiresAtUtc=@IExpiry;
BEGIN TRY EXEC dbo.ReserveAiAmount @AiBalanceId=@LengthBalance,@CompanyId=@LengthCompany,@Amount=1,@IdempotencyKey=@Key121,@ExpiresAtUtc=@IExpiry; END TRY BEGIN CATCH IF ERROR_NUMBER()=51322 SET @LengthRejected=1; ELSE THROW; END CATCH;
IF @LengthRejected=0 THROW 51831,N'Idempotency key 121 ky tu khong bi tu choi.',1;
SET @LengthRejected=0;
BEGIN TRY EXEC dbo.ReserveAiAmount @AiBalanceId=@LengthBalance,@CompanyId=@LengthCompany,@Amount=1,@IdempotencyKey=@Key128,@ExpiresAtUtc=@IExpiry; END TRY BEGIN CATCH IF ERROR_NUMBER()=51322 SET @LengthRejected=1; ELSE THROW; END CATCH;
IF @LengthRejected=0 THROW 51832,N'Idempotency key 128 ky tu khong bi tu choi.',1;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;

BEGIN TRANSACTION;
DECLARE @CompanyA uniqueidentifier=NEWID(),@CompanyB uniqueidentifier=NEWID(),@BranchA uniqueidentifier=NEWID(),@BranchB uniqueidentifier=NEWID(),@UserA uniqueidentifier=NEWID(),@UserB uniqueidentifier=NEWID(),@CompanyUserA uniqueidentifier=NEWID(),@CompanyUserB uniqueidentifier=NEWID(),@CompanyRoleA uniqueidentifier=NEWID(),@BranchRoleA uniqueidentifier=NEWID(),@Feature uniqueidentifier=NEWID(),@ServerA uniqueidentifier=NEWID(),@ServerB uniqueidentifier=NEWID(),@Template uniqueidentifier=NEWID(),@ControlTemplate uniqueidentifier=NEWID(),@Secret uniqueidentifier=NEWID(),@ManagedDatabase uniqueidentifier=NEWID(),@Job uniqueidentifier=NEWID(),@BalanceA uniqueidentifier=NEWID(),@BalanceB uniqueidentifier=NEWID(),@Rejected bit;

INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@CompanyA,N'COMPANY_A',N'COMPANY_A',N'Company A',N'Company A'),(@CompanyB,N'COMPANY_B',N'COMPANY_B',N'Company B',N'Company B');
INSERT dbo.Branches(BranchId,CompanyId,BranchCode,NormalizedBranchCode,Name) VALUES(@BranchA,@CompanyA,N'A01',N'A01',N'Branch A'),(@BranchB,@CompanyB,N'B01',N'B01',N'Branch B');
INSERT dbo.Users(UserId,DisplayName,SecurityStamp) VALUES(@UserA,N'User A',NEWID()),(@UserB,N'User B',NEWID());
INSERT dbo.CompanyUsers(CompanyUserId,CompanyId,UserId) VALUES(@CompanyUserA,@CompanyA,@UserA),(@CompanyUserB,@CompanyB,@UserB);
INSERT dbo.BranchUsers(BranchUserId,CompanyId,BranchId,CompanyUserId,UserId) VALUES(NEWID(),@CompanyA,@BranchA,@CompanyUserA,@UserA);
INSERT dbo.Roles(RoleId,CompanyId,RoleCode,NormalizedRoleCode,Name,ScopeType) VALUES(@CompanyRoleA,@CompanyA,N'COMPANY_ROLE',N'COMPANY_ROLE',N'Company role',1),(@BranchRoleA,@CompanyA,N'BRANCH_ROLE',N'BRANCH_ROLE',N'Branch role',2);
INSERT dbo.BranchUserRoles(BranchUserRoleId,CompanyId,BranchUserId,RoleId) SELECT NEWID(),@CompanyA,BranchUserId,@BranchRoleA FROM dbo.BranchUsers WHERE CompanyId=@CompanyA AND BranchId=@BranchA AND UserId=@UserA;

SET @Rejected=0;
BEGIN TRY INSERT dbo.CompanyUserRoles(CompanyUserRoleId,CompanyId,CompanyUserId,RoleId) VALUES(NEWID(),@CompanyB,@CompanyUserB,@CompanyRoleA); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51802,N'Gan role Company A cho user Company B khong bi chan.',1;
INSERT dbo.Features(FeatureId,FeatureCode,NormalizedFeatureCode,Name) VALUES(@Feature,N'VOICE',N'VOICE',N'Voice');
INSERT dbo.CompanyFeatures(CompanyFeatureId,CompanyId,FeatureId,IsEnabled) VALUES(NEWID(),@CompanyA,@Feature,1);
INSERT dbo.BranchFeatures(BranchFeatureId,CompanyId,BranchId,FeatureId,IsEnabled) VALUES(NEWID(),@CompanyA,@BranchA,@Feature,1);

SET @Rejected=0;
BEGIN TRY INSERT dbo.AuditLogs(AuditLogId,CompanyId,BranchId,ActionCode,EntityType,Outcome,CorrelationId) VALUES(NEWID(),@CompanyB,@BranchA,N'test.audit',N'Test',1,NEWID()); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51805,N'Audit Branch khac Company khong bi chan.',1;
SET @Rejected=0;
BEGIN TRY INSERT dbo.AuditLogs(AuditLogId,BranchId,ActionCode,EntityType,Outcome,CorrelationId) VALUES(NEWID(),@BranchA,N'test.audit',N'Test',1,NEWID()); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51812,N'Audit co Branch nhung khong co Company khong bi chan.',1;

INSERT dbo.AiBalances(AiBalanceId,CompanyId,AiType,AvailableAmount) VALUES(@BalanceA,@CompanyA,N'VOICE',10),(@BalanceB,@CompanyB,N'VOICE',10);
DECLARE @AiExpiry datetime2(7)=DATEADD(MINUTE,5,SYSUTCDATETIME());
DECLARE @ReservationResult TABLE(AiReservationId uniqueidentifier);
INSERT @ReservationResult EXEC dbo.ReserveAiAmount @AiBalanceId=@BalanceA,@CompanyId=@CompanyA,@Amount=2,@IdempotencyKey=N'ai-reserve-a',@ExpiresAtUtc=@AiExpiry;
DECLARE @ReservationA uniqueidentifier=(SELECT AiReservationId FROM @ReservationResult);
EXEC dbo.FinalizeAiReservation @AiReservationId=@ReservationA,@CompanyId=@CompanyA,@TransactionType=2,@IdempotencyKey=N'ai-consume-a';
DECLARE @AiTransactionA uniqueidentifier=(SELECT AiTransactionId FROM dbo.AiTransactions WHERE AiReservationId=@ReservationA);
SET @Rejected=0;
BEGIN TRY INSERT dbo.AiUsageLogs(AiUsageLogId,CompanyId,AiTransactionId,ProviderCode,OperationCode) VALUES(NEWID(),@CompanyB,@AiTransactionA,N'test',N'usage'); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51815,N'Usage log AI khac Company khong bi chan.',1;

INSERT dbo.DatabaseServers(DatabaseServerId,ServerCode,NormalizedServerCode,HostAlias,DeploymentMode) VALUES(@ServerA,N'SERVER_A',N'SERVER_A',N'local-a',1),(@ServerB,N'SERVER_B',N'SERVER_B',N'local-b',1);
INSERT dbo.DatabaseTemplates(TemplateId,TemplateCode,NormalizedTemplateCode,DatabaseKind) VALUES(@Template,N'OPERATIONAL_V1',N'OPERATIONAL_V1',N'Operational'),(@ControlTemplate,N'CONTROL_V1',N'CONTROL_V1',N'ControlPlane');
DECLARE @OperationalRelease uniqueidentifier=NEWID(),@ControlRelease uniqueidentifier=NEWID(),@Checksum char(64)=REPLICATE('A',64),@OtherChecksum char(64)=REPLICATE('B',64);
INSERT dbo.DatabaseReleases(DatabaseReleaseId,TemplateId,ReleaseCode,ScriptChecksum,Status,ReleasedAtUtc) VALUES(@OperationalRelease,@Template,N'v1',@Checksum,2,SYSUTCDATETIME()),(@ControlRelease,@ControlTemplate,N'v1',@OtherChecksum,2,SYSUTCDATETIME());
INSERT dbo.SecretReferences(SecretReferenceId,ProviderCode,ReferenceKey,PurposeCode) VALUES(@Secret,N'LocalTest',N'ref-sql-login',N'SqlLogin');
INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId,CurrentReleaseId) VALUES(@ManagedDatabase,2,@ServerA,@CompanyA,@BranchA,N'branch_a_online',N'BRANCH_A_ONLINE',1,1,N'company-a/branch-a',@Template,@OperationalRelease);
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId,CurrentReleaseId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'wrong_release_online',N'WRONG_RELEASE_ONLINE',1,1,N'company-a/wrong',@Template,@ControlRelease); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51817,N'Release khac template cua database khong bi chan.',1;
INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerB,@CompanyA,@BranchA,N'branch_a_online',N'BRANCH_A_ONLINE',1,1,N'company-a/branch-a-copy',@Template);
INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId,Status) VALUES(NEWID(),2,@ServerB,@CompanyA,@BranchA,N'branch_a_active_online',N'BRANCH_A_ACTIVE_ONLINE',1,1,N'company-a/branch-a-active',@Template,2);
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId,Status) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'branch_a_second_online',N'BRANCH_A_SECOND_ONLINE',1,1,N'company-a/branch-a-second',@Template,2); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51828,N'Hai Operational database active cung Branch khong bi chan.',1;
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'branch_a_online',N'BRANCH_A_ONLINE',1,1,N'duplicate',@Template); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51806,N'Duplicate database cung Server khong bi chan.',1;
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,SqlLoginName,NormalizedSqlLoginName,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'missing_secret_online',N'MISSING_SECRET_ONLINE',2,N'login_missing',N'LOGIN_MISSING',1,N'missing-secret',@Template); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51807,N'SQL Login khong co SecretReference khong bi chan.',1;
INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,SqlLoginName,NormalizedSqlLoginName,SecretReferenceId,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'sql_login_online',N'SQL_LOGIN_ONLINE',2,N'login_test',N'LOGIN_TEST',@Secret,1,N'sql-login',@Template);
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,SqlLoginName,NormalizedSqlLoginName,SecretReferenceId,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'bad_login_online',N'BAD_LOGIN_ONLINE',2,N'bad-login',N'BAD-LOGIN',@Secret,1,N'bad-login',@Template); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51821,N'Ten SQL login khong an toan bi chap nhan.',1;
SET @Rejected=0;
BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),1,@ServerA,N'not_ttsmart',N'NOT_TTSMART',1,1,N'control-plane',@ControlTemplate); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51822,N'Database ControlPlane reserved name khong bi chan.',1;

DECLARE @InvalidName nvarchar(128);
DECLARE InvalidNames CURSOR LOCAL FAST_FORWARD FOR SELECT NameValue FROM (VALUES(N'é_online'),(N'Á_online'),(N'Ａ_online'),(N''),(N'bad-name_online'),(N'UPPER_ONLINE')) AS ValuesToTest(NameValue);
OPEN InvalidNames; FETCH NEXT FROM InvalidNames INTO @InvalidName;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @Rejected=0;
 BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,@InvalidName,UPPER(@InvalidName),1,1,N'invalid-name',@Template); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
 IF @Rejected=0 THROW 51808,N'Ten database khong ASCII hop le bi chap nhan.',1;
 FETCH NEXT FROM InvalidNames INTO @InvalidName;
END;
CLOSE InvalidNames; DEALLOCATE InvalidNames;

DECLARE @InvalidNamespace nvarchar(200);
DECLARE @NamespaceOrdinal int=0;
DECLARE InvalidNamespaces CURSOR LOCAL FAST_FORWARD FOR SELECT NamespaceValue FROM (VALUES(N'.'),(N'..'),(N'./x'),(N'../x'),(N'a/./b'),(N'a/../b'),(N'/absolute'),(N'a//b'),(N'a\\b'),(N'C:/x'),(N'a/ /b'),(N'a/b '),(N'a/b.'),(N'a/trailing. /b'),(N'CON'),(N'a/CON/b'),(N'a/')) AS ValuesToTest(NamespaceValue);
OPEN InvalidNamespaces; FETCH NEXT FROM InvalidNamespaces INTO @InvalidNamespace;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @NamespaceOrdinal=@NamespaceOrdinal+1;
 SET @Rejected=0;
 BEGIN TRY INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId) VALUES(NEWID(),2,@ServerA,@CompanyA,@BranchA,N'ns'+CONVERT(nvarchar(32),@NamespaceOrdinal)+N'_online',N'NS'+CONVERT(nvarchar(32),@NamespaceOrdinal)+N'_ONLINE',1,1,@InvalidNamespace,@Template); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
 IF @Rejected=0 THROW 51818,N'Storage namespace khong an toan bi chap nhan.',1;
 FETCH NEXT FROM InvalidNamespaces INTO @InvalidNamespace;
END;
CLOSE InvalidNamespaces; DEALLOCATE InvalidNamespaces;

INSERT dbo.ProvisioningJobs(ProvisioningJobId,ManagedDatabaseId,OperationType,IdempotencyKey,Status,TargetReleaseId,TargetChecksum) VALUES(@Job,@ManagedDatabase,1,N'job-controlplane-test',0,@OperationalRelease,@Checksum);
SET @Rejected=0;
BEGIN TRY INSERT dbo.ProvisioningJobs(ProvisioningJobId,ManagedDatabaseId,OperationType,IdempotencyKey,Status) VALUES(NEWID(),@ManagedDatabase,2,N'job-other-operation-same-target',0); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51819,N'Hai operation active tren cung database dich khong bi chan.',1;
DECLARE @ClaimOne TABLE(ProvisioningJobId uniqueidentifier,LeaseToken uniqueidentifier,LeaseUntilUtc datetime2(7));
EXECUTE AS USER=N'ControlPlaneProvisioningWorkerUser';
INSERT @ClaimOne EXEC dbo.ClaimNextProvisioningJob @LeaseOwner=N'test-worker-1',@LeaseSeconds=60;
REVERT;
DECLARE @ActiveLease uniqueidentifier=(SELECT LeaseToken FROM @ClaimOne);
IF NOT EXISTS(SELECT 1 FROM dbo.ProvisioningJobs WHERE ProvisioningJobId=@Job AND Status=1 AND LeaseOwner=N'test-worker-1' AND LeaseToken=@ActiveLease AND LeaseUntilUtc>SYSUTCDATETIME() AND RetryCount=1) THROW 51809,N'Claim provisioning lease khong dung.',1;
SET @Rejected=0;
EXECUTE AS USER=N'ControlPlaneProvisioningWorkerUser';
BEGIN TRY UPDATE dbo.ProvisioningJobs SET Status=2 WHERE ProvisioningJobId=@Job; END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
REVERT;
IF @Rejected=0 OR NOT EXISTS(SELECT 1 FROM dbo.ProvisioningJobs WHERE ProvisioningJobId=@Job AND Status=1) THROW 51833,N'Provisioning worker sua truc tiep job hoac workflow bi bo qua.',1;
EXECUTE AS USER=N'ControlPlaneProvisioningWorkerUser';
EXEC dbo.CompleteProvisioningJob @ProvisioningJobId=@Job,@LeaseToken=@ActiveLease;
REVERT;
SET @Rejected=0;
BEGIN TRY INSERT dbo.ProvisioningJobs(ProvisioningJobId,ManagedDatabaseId,OperationType,IdempotencyKey,Status) VALUES(NEWID(),@ManagedDatabase,2,N'job-controlplane-test',0); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51810,N'Idempotency provisioning khong bi chan.',1;

/* Kiem tra trigger cuoi cung truoc rollback: loi trigger se danh dau transaction nay khong the ghi tiep. */
SET @Rejected=0;
BEGIN TRY INSERT dbo.ProvisioningJobs(ProvisioningJobId,ManagedDatabaseId,OperationType,IdempotencyKey,Status,TargetReleaseId,TargetChecksum) VALUES(NEWID(),@ManagedDatabase,2,N'job-wrong-checksum',0,@OperationalRelease,@OtherChecksum); END TRY BEGIN CATCH SET @Rejected=1; END CATCH;
IF @Rejected=0 THROW 51823,N'Target checksum khong khop release khong bi chan.',1;

IF XACT_STATE()<>0 ROLLBACK TRANSACTION;

/* Release da phat hanh giu nguyen template, ma va checksum. */
DECLARE @RCompany uniqueidentifier=NEWID(),@RServer uniqueidentifier=NEWID(),@RTemplate uniqueidentifier=NEWID(),@RRelease uniqueidentifier=NEWID(),@RRejected bit,@RChecksum char(64)=REPLICATE('D',64);
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@RCompany,N'ISO_RELEASE',N'ISO_RELEASE',N'Isolated release',N'Isolated release');
INSERT dbo.DatabaseServers(DatabaseServerId,ServerCode,NormalizedServerCode,HostAlias,DeploymentMode) VALUES(@RServer,N'ISO_RELEASE',N'ISO_RELEASE',N'local-release',1);
INSERT dbo.DatabaseTemplates(TemplateId,TemplateCode,NormalizedTemplateCode,DatabaseKind) VALUES(@RTemplate,N'ISO_RELEASE',N'ISO_RELEASE',N'Operational');
INSERT dbo.DatabaseReleases(DatabaseReleaseId,TemplateId,ReleaseCode,ScriptChecksum,ReleasedAtUtc) VALUES(@RRelease,@RTemplate,N'v1',@RChecksum,SYSUTCDATETIME());
SET @RRejected=0;
BEGIN TRY UPDATE dbo.DatabaseReleases SET ScriptChecksum=REPLICATE('E',64) WHERE DatabaseReleaseId=@RRelease; END TRY BEGIN CATCH SET @RRejected=1; END CATCH;
IF @RRejected=0 OR EXISTS(SELECT 1 FROM dbo.DatabaseReleases WHERE DatabaseReleaseId=@RRelease AND ScriptChecksum<>@RChecksum) THROW 51829,N'Release da phat hanh bi sua hoac ghi mot phan.',1;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;

/* Lease cu duoc kiem tra trong transaction tach rieng; loi complete phai chi duoc rollback, khong de lai du lieu test. */
DECLARE @PCompany uniqueidentifier=NEWID(),@PBranch uniqueidentifier=NEWID(),@PServer uniqueidentifier=NEWID(),@PTemplate uniqueidentifier=NEWID(),@PRelease uniqueidentifier=NEWID(),@PDatabase uniqueidentifier=NEWID(),@PJob uniqueidentifier=NEWID(),@PChecksum char(64)=REPLICATE('C',64),@PRejected bit;
BEGIN TRANSACTION;
INSERT dbo.Companies(CompanyId,CompanyCode,NormalizedCompanyCode,LegalName,DisplayName) VALUES(@PCompany,N'ISO_PROVISION',N'ISO_PROVISION',N'Isolated provision',N'Isolated provision');
INSERT dbo.Branches(BranchId,CompanyId,BranchCode,NormalizedBranchCode,Name) VALUES(@PBranch,@PCompany,N'ISO',N'ISO',N'Isolated provision branch');
INSERT dbo.DatabaseServers(DatabaseServerId,ServerCode,NormalizedServerCode,HostAlias,DeploymentMode) VALUES(@PServer,N'ISO_PROVISION',N'ISO_PROVISION',N'local-provision',1);
INSERT dbo.DatabaseTemplates(TemplateId,TemplateCode,NormalizedTemplateCode,DatabaseKind) VALUES(@PTemplate,N'ISO_OPERATIONAL',N'ISO_OPERATIONAL',N'Operational');
INSERT dbo.DatabaseReleases(DatabaseReleaseId,TemplateId,ReleaseCode,ScriptChecksum,Status,ReleasedAtUtc) VALUES(@PRelease,@PTemplate,N'v1',@PChecksum,2,SYSUTCDATETIME());
INSERT dbo.ManagedDatabases(ManagedDatabaseId,DatabaseType,DatabaseServerId,CompanyId,BranchId,DatabaseName,NormalizedDatabaseName,AuthenticationType,DeploymentMode,StorageNamespace,TemplateId,CurrentReleaseId) VALUES(@PDatabase,2,@PServer,@PCompany,@PBranch,N'iso_provision_online',N'ISO_PROVISION_ONLINE',1,1,N'iso/provision',@PTemplate,@PRelease);
INSERT dbo.ProvisioningJobs(ProvisioningJobId,ManagedDatabaseId,OperationType,IdempotencyKey,Status,TargetReleaseId,TargetChecksum) VALUES(@PJob,@PDatabase,1,N'iso-provision-job',0,@PRelease,@PChecksum);
DECLARE @PClaimOne TABLE(ProvisioningJobId uniqueidentifier,LeaseToken uniqueidentifier,LeaseUntilUtc datetime2(7));
INSERT @PClaimOne EXEC dbo.ClaimNextProvisioningJob @LeaseOwner=N'iso-worker-1',@LeaseSeconds=1;
DECLARE @POldLease uniqueidentifier=(SELECT LeaseToken FROM @PClaimOne);
WAITFOR DELAY '00:00:02';
DECLARE @PClaimTwo TABLE(ProvisioningJobId uniqueidentifier,LeaseToken uniqueidentifier,LeaseUntilUtc datetime2(7));
INSERT @PClaimTwo EXEC dbo.ClaimNextProvisioningJob @LeaseOwner=N'iso-worker-2',@LeaseSeconds=60;
SET @PRejected=0;
BEGIN TRY EXEC dbo.CompleteProvisioningJob @ProvisioningJobId=@PJob,@LeaseToken=@POldLease; END TRY BEGIN CATCH SET @PRejected=1; END CATCH;
IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
IF @PRejected=0 THROW 51820,N'Worker cu complete sau khi lease chuyen khong bi chan.',1;
SELECT N'ControlPlane constraint tests passed (transaction rolled back)' AS Result;
