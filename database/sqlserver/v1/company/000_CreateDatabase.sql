SET NOCOUNT ON;
/* Chi dung de tao database kiem thu Company duoc cap phep khi ket noi vao master. */
IF DB_NAME() <> N'master' THROW 59600, N'000_CreateDatabase.sql phai chay tren master.', 1;
DECLARE @lockResult int = -999;
BEGIN TRY
    EXEC @lockResult = sys.sp_getapplock @Resource=N'TTSmart.Company.V1.CreateDatabase', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=60000;
    IF @lockResult < 0 THROW 59601, N'Khong lay duoc khoa tao Company test database.', 1;
    IF DB_ID(N'TTSmart_Company_V1_Test') IS NULL
        CREATE DATABASE [TTSmart_Company_V1_Test] COLLATE Vietnamese_100_CI_AS;
    ALTER DATABASE [TTSmart_Company_V1_Test] SET AUTO_CLOSE OFF;
    ALTER DATABASE [TTSmart_Company_V1_Test] SET AUTO_SHRINK OFF;
    ALTER DATABASE [TTSmart_Company_V1_Test] SET PAGE_VERIFY CHECKSUM;
    ALTER DATABASE [TTSmart_Company_V1_Test] SET RECOVERY SIMPLE;
    ALTER DATABASE [TTSmart_Company_V1_Test] SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE;
    EXEC sys.sp_releaseapplock @Resource=N'TTSmart.Company.V1.CreateDatabase', @LockOwner=N'Session';
END TRY
BEGIN CATCH
    IF @lockResult >= 0 EXEC sys.sp_releaseapplock @Resource=N'TTSmart.Company.V1.CreateDatabase', @LockOwner=N'Session';
    THROW;
END CATCH;
GO
