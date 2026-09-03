SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabaseName)'
    THROW 60301, N'Script registry đang kết nối sai Control Plane database.', 1;

DECLARE @CompanyId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(CompanyId)');
DECLARE @BranchId uniqueidentifier = TRY_CONVERT(uniqueidentifier, N'$(BranchId)');
IF @CompanyId IS NULL OR @BranchId IS NULL
    THROW 60302, N'CompanyId hoặc BranchId không hợp lệ.', 1;

DECLARE @SuperAdminId uniqueidentifier;
IF (SELECT COUNT_BIG(*) FROM dbo.Users WHERE AccountType = 1 AND Status = 1 AND IsDeleted = 0) <> 1
    THROW 60303, N'Control Plane phải có đúng một SuperAdmin active.', 1;
SELECT @SuperAdminId = UserId FROM dbo.Users WHERE AccountType = 1 AND Status = 1 AND IsDeleted = 0;

BEGIN TRANSACTION;

DECLARE @LockResult int;
EXEC @LockResult = sys.sp_getapplock
    @Resource = N'TTSmart.CompanyBranchSplit.Registry',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @LockResult < 0 THROW 60304, N'Không lấy được application lock registry.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Companies WHERE CompanyId = @CompanyId)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Companies WHERE NormalizedCompanyCode = UPPER(N'$(CompanyCode)'))
        THROW 60305, N'CompanyCode đã thuộc CompanyId khác.', 1;
    INSERT dbo.Companies
        (CompanyId, CompanyCode, NormalizedCompanyCode, LegalName, DisplayName, Status)
    VALUES
        (@CompanyId, N'$(CompanyCode)', UPPER(N'$(CompanyCode)'), N'TTSmart', N'TTSmart', 1);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Branches WHERE BranchId = @BranchId)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Branches WHERE CompanyId = @CompanyId AND NormalizedBranchCode = UPPER(N'$(BranchCode)'))
        THROW 60306, N'BranchCode đã thuộc BranchId khác.', 1;
    INSERT dbo.Branches
        (BranchId, CompanyId, BranchCode, NormalizedBranchCode, Name, IsHeadOffice, Status)
    VALUES
        (@BranchId, @CompanyId, N'$(BranchCode)', UPPER(N'$(BranchCode)'), N'Chi nhánh chính', 1, 1);
END;

DECLARE @CompanyUserId uniqueidentifier;
SELECT @CompanyUserId = CompanyUserId
FROM dbo.CompanyUsers
WHERE CompanyId = @CompanyId AND UserId = @SuperAdminId AND IsDeleted = 0;
IF @CompanyUserId IS NULL
BEGIN
    SET @CompanyUserId = NEWID();
    INSERT dbo.CompanyUsers (CompanyUserId, CompanyId, UserId, UserType, Status)
    VALUES (@CompanyUserId, @CompanyId, @SuperAdminId, 1, 1);
END;

IF NOT EXISTS
(
    SELECT 1 FROM dbo.BranchUsers
    WHERE CompanyId = @CompanyId AND BranchId = @BranchId AND UserId = @SuperAdminId AND IsDeleted = 0
)
BEGIN
    INSERT dbo.BranchUsers
        (BranchUserId, BranchId, UserId, Status, IsPrimaryBranch, CompanyId, CompanyUserId)
    VALUES
        (NEWID(), @BranchId, @SuperAdminId, 1, 1, @CompanyId, @CompanyUserId);
END;

COMMIT TRANSACTION;
