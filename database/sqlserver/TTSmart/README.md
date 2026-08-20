# Script SQL Server cho `[TTSmart]`

Các script tạo database bán hàng của công ty TTSmart. Chúng không tạo database chi nhánh, không seed/copy dữ liệu MongoDB và không thay đổi runtime ASP.NET Core.

Chạy tuần tự bằng Windows Authentication:

```powershell
Get-ChildItem .\database\sqlserver\TTSmart\*.sql |
    Sort-Object Name |
    ForEach-Object { sqlcmd -S 'DESKTOP-5O6VV3J\SQLEXPRESS' -E -b -i $_.FullName }
```

`000_CreateDatabase.sql` chỉ tạo database nếu chưa có. Migration `001`–`009` kiểm tra `dbo.SchemaVersions` và chỉ báo trạng thái khi đã được áp dụng. Không đặt secret, password, token hoặc connection string vào script.

Xem thiết kế và bằng chứng metadata tại `docs/migration/SQLSERVER_TTSMART_DDL_IMPLEMENTATION.md`.
