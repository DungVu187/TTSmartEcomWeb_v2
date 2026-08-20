# Script SQL Server cho `[ttsmart.com.vn]`

Các script này tạo database tổng của TTSmart trên SQL Server. Chúng chỉ tạo metadata control-plane và catalog dùng chung; không tạo `[TTSmart]`, không tạo bất kỳ `[{BranchCode}_online]` nào và không seed dữ liệu nghiệp vụ.

## Thứ tự chạy

Chạy tuần tự các file `.sql` theo tiền tố số bằng Windows Authentication. Ví dụ trên máy local đã được kiểm chứng:

```powershell
Get-ChildItem .\database\sqlserver\ttsmart.com.vn\*.sql |
    Sort-Object Name |
    ForEach-Object { sqlcmd -S 'DESKTOP-5O6VV3J\SQLEXPRESS' -E -b -i $_.FullName }
```

`000_CreateDatabase.sql` chỉ tạo database khi chưa tồn tại. Các migration `001` đến `007` kiểm tra `dbo.SchemaVersions`; khi migration đã có, script chỉ thông báo và không thay đổi schema. Nếu phát hiện trạng thái schema không nhất quán hoặc migration 007 được chạy sau khi đã có dữ liệu cần chuyển đổi, script dừng để yêu cầu kế hoạch chuyển đổi riêng.

Không truyền password SQL, connection string, API key, token hoặc secret vào script. Password nhập từ form tạo branch phải được provisioning service đưa thẳng vào secret manager; SQL này chỉ có `dbo.SecretReferences` để lưu tham chiếu ngoài.

## Kiểm tra metadata

Sau khi chạy, dùng truy vấn metadata sau, không cần đọc dữ liệu nghiệp vụ:

```sql
USE [ttsmart.com.vn];
SELECT name FROM sys.tables WHERE is_ms_shipped = 0 ORDER BY name;
SELECT MigrationNumber, MigrationName, AppliedAtUtc
FROM dbo.SchemaVersions ORDER BY MigrationNumber;
SELECT COUNT(*) AS ForeignKeyCount FROM sys.foreign_keys;
SELECT COUNT(*) AS CheckConstraintCount FROM sys.check_constraints;
```

Thiết kế chi tiết và phạm vi DDL thực tế nằm tại `docs/migration/SQLSERVER_TTSMART_COM_VN_DDL_IMPLEMENTATION.md`.
