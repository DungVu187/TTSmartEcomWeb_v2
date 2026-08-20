# Báo cáo migration test MongoDB Ecom sang Operational v1

## Phạm vi và an toàn

Nguồn `Ecom` được truy cập read-only. Đích duy nhất là `TTSmart_Operational_V1_Test`; không chạy DDL/recreate sau khi đã ghi migration, không thay đổi MongoDB, database thật hay runtime ASP.NET. Báo cáo không chứa URI, connection string, PII, token hoặc document thô.

`LegacyRecords` giữ Canonical Extended JSON có redaction với field secret/token/password/OTP; `ContentSha256` vẫn được tính trên dạng Canonical Extended JSON nguồn để phục vụ đối chiếu.

## Profile

Profile trực tiếp phát hiện 19 collection và 1.503 document. Không có document rỗng; toàn bộ `_id` root quan sát được là ObjectId. Chi tiết field path, kiểu BSON, null/missing, mảng và candidate reference/file có tại [MONGODB_ECOM_PROFILE_2026-08-15.md](MONGODB_ECOM_PROFILE_2026-08-15.md).

## Dry-run và migrate

Dry-run cuối có `skipped = 0` ở mọi collection. Bảng dưới là manifest sau migrate lần hai; `Mapped` là document có mapper chuẩn, `Blocked/raw` là document chỉ có disposition Canonical Extended JSON. Mọi document, kể cả document Mapped, vẫn có raw evidence để các field chưa mapper không biến mất âm thầm.

| Collection | Source | Mapped | Blocked/raw | Errors | Skipped |
|---|---:|---:|---:|---:|---:|
| activitylogs | 383 | 0 | 383 | 0 | 0 |
| autologintokens | 0 | 0 | 0 | 0 | 0 |
| brands | 29 | 29 | 0 | 0 | 0 |
| chatmessages | 3 | 0 | 3 | 0 | 0 |
| chips | 1 | 0 | 1 | 0 | 0 |
| counters | 1 | 0 | 1 | 0 | 0 |
| eporders | 24 | 0 | 24 | 0 | 0 |
| iporders | 124 | 0 | 124 | 0 | 0 |
| manages | 1 | 0 | 1 | 0 | 0 |
| orders | 37 | 0 | 37 | 0 | 0 |
| products | 316 | 316 | 0 | 0 | 0 |
| sections | 1 | 1 | 0 | 0 | 0 |
| stations | 5 | 0 | 5 | 0 | 0 |
| storagehistories | 528 | 0 | 528 | 0 | 0 |
| telegramconfigs | 1 | 0 | 1 | 0 | 0 |
| types | 31 | 31 | 0 | 0 | 0 |
| users | 16 | 0 | 16 | 0 | 0 |
| voicevocabs | 1 | 0 | 1 | 0 | 0 |
| zaloconfigs | 1 | 0 | 1 | 0 | 0 |
| **Tổng** | **1.503** | **377** | **1.126** | **0** | **0** |

Lượt migrate thứ hai không tăng các số đếm sau: 316 Product, 316 ProductVariant, 316 Stock, 1.503 LegacyRecords, 1.039 MigrationMappings, 1.880 MigrationIssues và 19 MigrationManifests. Không có fingerprint source LegacyRecords trùng trong cùng run.

## Đối soát

`reconcile` đã đọc lại toàn bộ source và kiểm tập hợp source key xuất hiện qua Mapping root hoặc LegacyRecords bằng số document MongoDB của từng collection; không có mismatch. Vì mapper lookup/Product đang giữ raw evidence, mọi source key root vẫn có đúng một disposition document-level dù source có thể sinh nhiều mapping subdocument.

Các số SQL sau chỉ là snapshot của dữ liệu đã map chuẩn, không phải xác nhận tương đương nghiệp vụ với nguồn: `QuantityForSale = 9765.000000`, `QuantityInStorage = 9826.000000`, `PurchaseCount = 6`. Tổng tiền/Sales/Import/Export, history, file manifest nguồn và checksum file thật chưa thể đối soát vì các collection đó hiện Blocked/raw.

## Blocked còn lại

- Mapper chuẩn Users/customer/address/cart/template/station assignment; role/permission không được suy diễn.
- Sales, Import, Export, history, Station/storefront/voice/integration và activity/chat vẫn chỉ được bảo toàn raw.
- `chips` có shape phần tử mảng không khớp mapper chuỗi hiện có nên giữ raw.
- Chưa có storage root source/test được phê duyệt để đọc/copy file thật; URL/file candidate chỉ được profile thống kê, không tải nội dung URL ngoài.
- Không có đối soát tổng tiền/quantity nguồn hay manifest/checksum file thật cho các collection Blocked.

Do các mục Blocked này, báo cáo không tuyên bố tương đương nghiệp vụ hoặc khả năng cutover.
