/*
    Database: [ttsmart.com.vn]
    Mục đích: tạo database tổng nếu chưa tồn tại.
    An toàn: không DROP, không ALTER cấu hình database có sẵn và không seed dữ liệu.
*/
SET NOCOUNT ON;
GO

IF DB_ID(N'ttsmart.com.vn') IS NULL
BEGIN
    EXEC(N'CREATE DATABASE [ttsmart.com.vn];');
    PRINT N'Đã tạo database [ttsmart.com.vn].';
END
ELSE
BEGIN
    PRINT N'Database [ttsmart.com.vn] đã tồn tại; script không thay đổi database hiện có.';
END
GO
