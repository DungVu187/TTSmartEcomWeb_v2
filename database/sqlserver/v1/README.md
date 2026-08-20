# SQL Server baseline v1

Kiểm thử tổng hợp chỉ sử dụng hai database test đúng tên được cấp phép. Lệnh này chạy recreate, first-run đồng thời, chạy lặp idempotent (không làm đổi schema), layout/checksum migration, constraint, fingerprint mutation, verify, DBCC và kiểm tra không còn dữ liệu nghiệp vụ:

```powershell
.\database\sqlserver\v1\Test-SqlServerV1Baseline.ps1
```

Fingerprint là dấu vân tay SHA-256 của cấu trúc baseline: bảng/cột, constraint/index, trigger/module, role/user, membership, permission và option database. Kiểm thử mutation khôi phục trạng thái trong `finally`; nó không thay thế kiểm thử contract hay migration MongoDB.

Baseline v1 chỉ chạy trên hai database test local được cấp phép: `TTSmart_Control_V1_Test` và `TTSmart_Operational_V1_Test`. Không dùng script này cho `[ttsmart.com.vn]`, `[TTSmart]`, database branch hiện hữu hay SQL Server production.

Mỗi runner dùng Windows Authentication, kết nối trực tiếp database đích (`sqlcmd -d`), lấy application lock, tính SHA-256 của từng migration và truyền checksum vào `SchemaVersions`. Nếu cùng số migration có checksum khác, migration dừng thay vì bỏ qua drift.

```powershell
.\database\sqlserver\v1\control-plane\Run-ControlPlaneBaseline.ps1
.\database\sqlserver\v1\operational\Run-OperationalBaseline.ps1
```

Chỉ khi cần tạo lại database test đúng tên được phép:

```powershell
.\database\sqlserver\v1\control-plane\Run-ControlPlaneBaseline.ps1 -Recreate
.\database\sqlserver\v1\operational\Run-OperationalBaseline.ps1 -Recreate
```

`-Recreate` chỉ cho phép đúng hai database test và dừng nếu phát hiện dòng ngoài metadata `SchemaVersions`/`DatabaseInfo`. Có thể lấy schema fingerprint (dấu vân tay schema) để đối chiếu trước/sau lần chạy lại:

```powershell
.\database\sqlserver\v1\verification\Get-SchemaFingerprint.ps1 -DatabaseName TTSmart_Control_V1_Test
.\database\sqlserver\v1\verification\Get-SchemaFingerprint.ps1 -DatabaseName TTSmart_Operational_V1_Test
```

`ControlPlane v1` chỉ có control-plane. `Operational v1` là schema tự chủ dùng chung cho `[TTSmart]` và branch `_online`, với local identity, metadata file, catalog, chứng từ, tồn kho, storefront, voice, integration, audit và migration metadata. Không có migration/seed MongoDB trong baseline.
