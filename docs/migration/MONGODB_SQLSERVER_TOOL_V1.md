# Công cụ MongoDB sang SQL Server v1

Project `backend/src/TTSmartEcom.MongoSqlMigration` là console tool chỉ dùng trong Đợt 2. Tool không thay runtime ASP.NET và chỉ chấp nhận SQL target `TTSmart_Operational_V1_Test`.

## Chế độ

```powershell
dotnet run --project .\backend\src\TTSmartEcom.MongoSqlMigration -- profile <mongo-uri> <source-database> <sql-connection-string> [collection]
dotnet run --project .\backend\src\TTSmartEcom.MongoSqlMigration -- dry-run <mongo-uri> <source-database> <sql-connection-string> [collection]
dotnet run --project .\backend\src\TTSmartEcom.MongoSqlMigration -- migrate <mongo-uri> <source-database> <sql-connection-string> [collection]
dotnet run --project .\backend\src\TTSmartEcom.MongoSqlMigration -- reconcile <mongo-uri> <source-database> <sql-connection-string> [collection]
```

`profile` nhận thêm `--report <đường-dẫn>` để ghi Markdown chỉ có path/kiểu BSON/số đếm. Không có giá trị document trong terminal hoặc báo cáo.

Không ghi MongoDB. Không đưa URI, password hash, token, PII hoặc Canonical Extended JSON nguyên bản vào console output. SQL connection string chỉ truyền qua môi trường chạy được kiểm soát, không ghi vào source, log hay tài liệu.

## Bảo toàn và idempotency

- Mỗi collection có `MigrationRuns` theo source database/collection; chạy lại tái sử dụng run hiện có, chuyển trạng thái về `Running` rồi ghi kết quả mới.
- `migrate` đọc MongoDB theo batch 100 document. Mỗi batch dùng transaction SQL và savepoint theo document: lỗi mapper sẽ rollback về savepoint, lưu bản Canonical Extended JSON, ghi `MigrationIssues` không chứa payload và tiếp tục các document khác.
- Document được map chuẩn cũng có một `LegacyRecords` Canonical Extended JSON với lý do `StandardDocumentEvidence`; nhờ đó field chưa có mapper không biến mất âm thầm. Document không map được có lý do `UnmappedDocument` hoặc `MappingErrorFallback`.
- Trước khi ghi `LegacyRecords`, các field có tên secret/token/password/OTP/API key/authorization được thay bằng marker SHA-256; checksum nội dung vẫn tính từ Canonical Extended JSON nguồn. Vì vậy tool không đưa plaintext secret vào console hay bản ghi archive.
- `MigrationManifests` ghi count/checksum/disposition theo collection. `MigrationMappings` dùng `SourceKey`, `SourceKeyType`, `SourcePath` và fingerprint thay vì cột ObjectId cố định.
- `reconcile` đếm tập hợp hợp nhất source key đã map chuẩn hoặc được lưu nguyên bản, và dừng nếu tập hợp đó không bằng số document nguồn.

## Mapper có bằng chứng trên Ecom

Hiện mapper chuẩn đã chạy cho `brands`, `types`, `sections` (Category/CategoryValue) và `products` (Product/Variant/Stock). Giá string không parse được chỉ vào cột raw, số chuẩn hoá để `NULL` và trạng thái Variant là `Incomplete`; mapper không tạo giá/quantity/timestamp giả. Những collection khác chưa có quy tắc chuẩn hoá đầy đủ sẽ sinh `DocumentNotMapped` và được giữ raw thay vì bị bỏ qua.

## Fixture tổng hợp

Lệnh dưới đây chỉ ghi vào Operational test và dùng ba document nội bộ: một Product đơn giản được map, một document chưa có mapper và một Product cố ý vượt độ dài cột để xác minh fallback/issue. Chạy hai lần phải giữ nguyên số dòng đích.

```powershell
dotnet run --project .\backend\src\TTSmartEcom.MongoSqlMigration -- fixture <sql-connection-string-test> [fixture-file-root]
```

Kết quả fixture đã xác minh: `source=3`, `standard=1`, `preserved=2`, `errors=1`, `skipped=0`; sau hai lượt vẫn có đúng một Product, một mapping root, ba `LegacyRecords`, một `MigrationIssues` và một manifest. Khi truyền `fixture-file-root`, tool sinh file nguồn tổng hợp, copy vào `<root>/fixture/document.txt`, lưu `StorageKey=fixture/document.txt` (không có absolute path trong SQL), tạo đúng một `Files`/`FileLocations` và đối chiếu SHA-256. Dữ liệu fixture phải được dọn rồi recreate baseline sau kiểm thử.

## Phạm vi chưa xác minh

Đã chạy profile/dry-run/migrate hai lần/reconcile read-only trên `Ecom` được phê duyệt, với chi tiết tại `MONGODB_ECOM_MIGRATION_TEST_REPORT_2026-08-15.md`. Mapper chuẩn theo mọi collection, đối soát tiền/Sales/Import/Export/history/file và copy file local test vẫn còn Blocked; không coi raw preservation là tương đương nghiệp vụ.
