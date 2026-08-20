/* Bootstrap only for the explicitly approved local test database. Connect with sqlcmd -d master. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @LockResult int;
EXEC @LockResult=sys.sp_getapplock @Resource=N'TTSmart.ControlPlane.V1.TestDatabaseBootstrap',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=60000;
IF @LockResult<0 THROW 51000,N'Khong lay duoc khoa bootstrap database test ControlPlane.',1;
BEGIN TRY
    IF DB_ID(N'TTSmart_Control_V1_Test') IS NULL CREATE DATABASE [TTSmart_Control_V1_Test];
    EXEC sys.sp_releaseapplock @Resource=N'TTSmart.ControlPlane.V1.TestDatabaseBootstrap',@LockOwner=N'Session';
END TRY
BEGIN CATCH
    EXEC sys.sp_releaseapplock @Resource=N'TTSmart.ControlPlane.V1.TestDatabaseBootstrap',@LockOwner=N'Session';
    THROW;
END CATCH;
GO
