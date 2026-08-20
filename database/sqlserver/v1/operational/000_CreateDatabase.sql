SET NOCOUNT ON;
/* Chỉ dùng để tạo database kiểm thử được cấp phép. Chạy khi kết nối vào master. */
IF DB_NAME() <> N'master' THROW 58000, N'000_CreateDatabase.sql phải chạy trên master.', 1;
DECLARE @lockResult int = -999;
BEGIN TRY
    EXEC @lockResult = sys.sp_getapplock @Resource=N'TTSmart.Operational.V1.CreateDatabase', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=60000;
    IF @lockResult < 0 THROW 58001, N'Không lấy được khóa tạo Operational test database.', 1;
    IF DB_ID(N'TTSmart_Operational_V1_Test') IS NULL
        CREATE DATABASE [TTSmart_Operational_V1_Test] COLLATE Vietnamese_100_CI_AS;
    ALTER DATABASE [TTSmart_Operational_V1_Test] SET AUTO_CLOSE OFF;
    ALTER DATABASE [TTSmart_Operational_V1_Test] SET AUTO_SHRINK OFF;
    ALTER DATABASE [TTSmart_Operational_V1_Test] SET PAGE_VERIFY CHECKSUM;
    ALTER DATABASE [TTSmart_Operational_V1_Test] SET RECOVERY SIMPLE;
    EXEC sys.sp_releaseapplock @Resource=N'TTSmart.Operational.V1.CreateDatabase', @LockOwner=N'Session';
END TRY
BEGIN CATCH
    IF @lockResult >= 0 EXEC sys.sp_releaseapplock @Resource=N'TTSmart.Operational.V1.CreateDatabase', @LockOwner=N'Session';
    THROW;
END CATCH;
GO
