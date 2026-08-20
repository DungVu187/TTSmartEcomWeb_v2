# Triển khai SQL Server baseline v1

## Phạm vi đã chạy

Ngày 2026-08-15, hai database test local được cấp phép đã được recreate và xác minh trên `DESKTOP-5O6VV3J\SQLEXPRESS` bằng Windows Authentication:

| Baseline | Database test | Migration | Bảng |
|---|---|---:|---:|
| ControlPlane v1 | `TTSmart_Control_V1_Test` | 6 | 32 |
| Operational v1 | `TTSmart_Operational_V1_Test` | 11 | 76 |

Script nằm tại `database/sqlserver/v1/`. Chúng không sửa `[ttsmart.com.vn]`, `[TTSmart]`, MongoDB `Ecom`, runtime ASP.NET Core hoặc prototype migration cũ.

## Kết quả metadata

| Baseline | PK | FK | Check | Index | Options |
|---|---:|---:|---:|---:|---|
| ControlPlane | 32 | 41 | 91 | 89 | `AUTO_CLOSE OFF`, `AUTO_SHRINK OFF`, `SIMPLE`, `PAGE_VERIFY CHECKSUM`, RCSI OFF, Query Store `READ_WRITE` |
| Operational | 76 | 77 | 155 | 159 | `AUTO_CLOSE OFF`, `AUTO_SHRINK OFF`, `SIMPLE`, `PAGE_VERIFY CHECKSUM`, RCSI OFF, collation `Vietnamese_100_CI_AS` |

`SchemaVersions` của cả hai baseline có `ModuleCode`, số migration, tên, SHA-256 checksum, thời điểm và principal áp dụng. Runner tính SHA-256 artifact, truyền qua sqlcmd với input/output UTF-8 (`-f i:65001,o:65001`), và migration dừng khi checksum cùng số migration khác nhau. Migration dùng application lock, `XACT_ABORT ON`, transaction và không có `USE` hardcode database prototype.

`Get-SchemaFingerprint.ps1` tạo dấu vân tay schema ổn định từ bảng, cột, index, CHECK/FK và trạng thái tin cậy, trigger, module SQL, role/permission, membership, Query Store và option database. Constraint có tên tự sinh bị verify từ chối; fingerprint không phụ thuộc thứ tự metadata.

## Kiểm thử đã chạy

- Recreate hai database test đúng tên được cấp phép; chạy baseline lần đầu và chạy lại idempotent (chạy lặp không làm thay đổi schema).
- Chạy đồng thời hai runner cho từng baseline; cả bốn tiến trình đều kết thúc thành công, không tạo DDL trùng.
- `Test-ControlPlaneConcurrentFirstRun.ps1` xóa đúng database ControlPlane test rồi chạy hai runner đồng thời từ schema trống; kiểm tra cả hai job thành công và có đủ 6 migration.
- `Test-SchemaFingerprint.ps1` tắt rồi bật lại trigger trên từng database test, xác nhận fingerprint thay đổi và trở về giá trị ban đầu.
- `Test-SqlServerV1Baseline.ps1` đã chạy pass: recreate, concurrent first-run cho cả hai baseline, layout/checksum, constraint/security, fingerprint mutation, verify, DBCC và kiểm tra chỉ còn metadata.
- Cố ý truyền SHA-256 sai vào migration `002` của từng baseline; cả hai đều dừng với lỗi checksum mismatch (checksum không khớp).
- `TestControlPlaneConstraints.sql`: role/Company/Branch scope, feature entitlement, audit composite FK, database name ASCII/suffix, auth Windows/SQL Login, secret reference, provisioning lease/idempotency; transaction rollback.
- `TestOperationalConstraints.sql`: PublicId lowercase 24-hex, `Version = 0`, product không code, code 120 ký tự, rating lẻ, quantity thập phân, orphan legacy, progress nhập/xuất, voice duplicate và mapping một source sang nhiều target; transaction rollback.
- `VerifyControlPlane.sql`, `VerifyOperational.sql`: `DBCC CHECKCONSTRAINTS` theo từng bảng Operational (tránh lỗi nội bộ SQL Server khi DBCC trộn hai collation), `DBCC CHECKDB ... WITH PHYSICAL_ONLY`, constraint trusted/enabled, checksum, kiểu dữ liệu cấm, không có heap và metadata cơ bản.

Không seed hay copy MongoDB/file thật. Test dữ liệu nghiệp vụ đã rollback: ControlPlane còn đúng 1 dòng `DatabaseInfo` và 6 dòng `SchemaVersions`; Operational còn đúng 1 dòng `DatabaseInfo` và 11 dòng `SchemaVersions`.

## Phần chưa xác minh

- Chưa chạy migration MongoDB, manifest/checksum file, provider thật, tải đồng thời hay cutover.
- RCSI cố ý giữ OFF vì chưa có kiểm thử transaction tồn kho; đây là quyết định test baseline, không phải cấu hình cho database prototype hiện hữu.
- Baseline Operational là template schema; chưa tạo database branch thực tế hoặc triển khai service provisioning/runtime SQL.
