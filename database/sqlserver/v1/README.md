# SQL Server baseline v1

> **Trạng thái lịch sử sau quyết định kiến trúc ngày 2026-08-24:** baseline này vẫn dùng để kiểm tra DDL hai tầng đã có, nhưng kiến trúc đích đã tách Platform DB, Company DB và Branch DB. `Operational v1` chưa phải Company schema hoặc Branch schema đã duyệt và không được dùng để suy ra rằng kiến trúc ba tầng đã được triển khai. Xem `../../../docs/architecture/SQLSERVER_TARGET_ARCHITECTURE.md`.

Kiểm thử tổng hợp chỉ sử dụng hai database test đúng tên được cấp phép. Lệnh này chạy recreate, first-run đồng thời, chạy lặp idempotent (không làm đổi schema), layout/checksum migration, constraint, fingerprint mutation, verify, DBCC và kiểm tra không còn dữ liệu nghiệp vụ:

```powershell
.\database\sqlserver\v1\Test-SqlServerV1Baseline.ps1
```

Fingerprint là dấu vân tay SHA-256 của cấu trúc baseline: bảng/cột, constraint/index, trigger/module, role/user, membership, permission và option database. Kiểm thử mutation khôi phục trạng thái trong `finally`; nó không thay thế kiểm thử contract hay migration MongoDB.

Baseline v1 có ba family test tách biệt: `TTSmart_Control_V1_Test`, `TTSmart_Operational_V1_Test` (lịch sử hai tầng) và `TTSmart_Company_V1_Test` (Company Shared). Không dùng script này cho `[ttsmart.com.vn]`, `[TTSmart]`, database branch hiện hữu hay SQL Server production.

Mỗi runner dùng Windows Authentication, kết nối trực tiếp database đích (`sqlcmd -d`), lấy application lock, tính SHA-256 của từng migration và truyền checksum vào `SchemaVersions`. Nếu cùng số migration có checksum khác, migration dừng thay vì bỏ qua drift.

```powershell
.\database\sqlserver\v1\control-plane\Run-ControlPlaneBaseline.ps1
.\database\sqlserver\v1\operational\Run-OperationalBaseline.ps1
.\database\sqlserver\v1\company\Run-CompanyBaseline.ps1
```

Chỉ khi cần tạo lại database test đúng tên được phép:

```powershell
.\database\sqlserver\v1\control-plane\Run-ControlPlaneBaseline.ps1 -Recreate
.\database\sqlserver\v1\operational\Run-OperationalBaseline.ps1 -Recreate
.\database\sqlserver\v1\company\Run-CompanyBaseline.ps1 -Recreate
```

`-Recreate` chỉ cho phép đúng hai database test và dừng nếu phát hiện dòng ngoài metadata `SchemaVersions`/`DatabaseInfo`. Có thể lấy schema fingerprint (dấu vân tay schema) để đối chiếu trước/sau lần chạy lại:

```powershell
.\database\sqlserver\v1\verification\Get-SchemaFingerprint.ps1 -DatabaseName TTSmart_Control_V1_Test
.\database\sqlserver\v1\verification\Get-SchemaFingerprint.ps1 -DatabaseName TTSmart_Operational_V1_Test
.\database\sqlserver\v1\verification\Get-SchemaFingerprint.ps1 -DatabaseName TTSmart_Company_V1_Test
```

`ControlPlane v1` chỉ có control-plane. `Operational v1` là schema tự chủ dùng chung cho `[TTSmart]` và branch `_online`, với local identity, metadata file, catalog, chứng từ, tồn kho, storefront, voice, integration, audit và migration metadata. Không có migration/seed MongoDB trong baseline.

`Company v1` là family riêng cho Product Master dùng chung cấp Company, catalog/file metadata, CompanySettings, audit Company và migration metadata. Nó không có identity, giao dịch/tồn kho/Station Branch hoặc Outbox/Inbox/sync. Chạy toàn bộ test Company bằng `./database/sqlserver/v1/Test-SqlServerCompanyV1Baseline.ps1`.
