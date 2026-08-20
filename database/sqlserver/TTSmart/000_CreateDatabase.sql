/* Tạo database bán hàng TTSmart nếu chưa tồn tại; không DROP/ALTER database có sẵn. */
SET NOCOUNT ON;
GO
IF DB_ID(N'TTSmart') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [TTSmart];');
    PRINT N'Đã tạo database [TTSmart].';
END
ELSE
    PRINT N'Database [TTSmart] đã tồn tại; script không thay đổi database hiện có.';
GO
