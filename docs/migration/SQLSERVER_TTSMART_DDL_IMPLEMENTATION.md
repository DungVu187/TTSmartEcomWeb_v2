# DDL đã triển khai cho `[TTSmart]`

## Phạm vi

Ngày 2026-08-14, database `[TTSmart]` đã được tạo trên `DESKTOP-5O6VV3J\SQLEXPRESS` bằng Windows Authentication. DDL nằm tại `database/sqlserver/TTSmart/`; không có seed hay sao chép dữ liệu MongoDB, không tạo database branch và không thay runtime MongoDB.

| Migration | Nhóm bảng |
|---|---|
| 001 | system, lịch sử schema, migration run/issue, legacy ID, number sequence |
| 002 | catalog Product/Variant, lookup, file và review |
| 003 | User projection, customer, cart và order template |
| 004 | sales order, line và file |
| 005 | stock, stock ledger/history, import và export order |
| 006 | station, product visibility, storefront và policy ba locale |
| 007 | voice vocabulary, integration metadata, notification recipient reference |
| 008 | activity audit và archived chat message |
| 009 | foreign key product-variant pair, check tiến độ tồn và index truy vấn |

Schema có 54 bảng, dùng duy nhất `dbo`. `Users` là projection vận hành; không có password hash, token hoặc permission authoritative. `Integrations` và `NotificationRecipients` chỉ giữ ID tham chiếu secret manager, không giữ secret/recipient value.

## Xác minh đã chạy

| Hạng mục | Kết quả |
|---|---:|
| Migration ghi nhận | 9 |
| Bảng/PK | 54 / 54 |
| Foreign key | 52 |
| Check constraint | 77 |
| Unique constraint | 18 |
| Index | 118 |
| Cột password/token/connection string/secret key/access-refresh token | 0 |
| Kiểu `money`, `smallmoney`, `float`, `real`, `text`, `ntext`, `image` | 0 |

Đã chạy lại toàn bộ script sau khi tạo; 9 migration hiện hữu được bỏ qua, bảng và migration count không đổi.

## Chưa xác minh

- Chưa migration dữ liệu, seed dữ liệu tổng hợp hay smoke test ghi/rollback.
- Chưa tạo template/database chi nhánh, service provisioning, SQL repository hoặc Entity Framework Core.
- Quy tắc một Product active phải còn Variant được application/service thực thi; không dùng trigger để tránh làm sai luồng migration.
- `SchemaVersions.ScriptChecksum` hiện là `NULL` cho đến khi có migration runner tính checksum artifact ngoài file.
