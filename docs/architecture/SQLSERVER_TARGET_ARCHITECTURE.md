# Kiến trúc đích SQL Server baseline v1

## Ranh giới database

Baseline v1 tách hai vai trò vật lý độc lập:

| Baseline | Database test | Vai trò |
|---|---|---|
| ControlPlane v1 | `TTSmart_Control_V1_Test` | Company, Branch, identity/quyền control-plane, entitlement, AI quota, registry database, provisioning và audit hệ thống |
| Operational v1 | `TTSmart_Operational_V1_Test` | Một schema tự chủ dùng lại cho `[TTSmart]` và mọi `[{ChiNhanh}_online]` |

ControlPlane không có bảng Product, Customer, Cart, chứng từ, Stock hoặc metadata file nghiệp vụ. Operational không phụ thuộc foreign key vào ControlPlane; mỗi database Operational có local identity để có thể vận hành độc lập.

Không có foreign key hay transaction xuyên database. Tham chiếu tới secret manager chỉ là GUID metadata, không mang secret value. File nằm ngoài SQL và được nối bởi `Files`, `FileLocations`, `FileAliases` dùng storage key tương đối.

## Identity và định danh

- GUID là khóa nội bộ.
- `PublicId char(24)` 24 ký tự hexadecimal lowercase là định danh API-compatible của entity Operational có thể lộ `_id`.
- `Version int` của entity nghiệp vụ Operational bắt đầu từ 0; `rowversion` chỉ dùng cho optimistic concurrency SQL.
- Operational test dùng collation `Vietnamese_100_CI_AS` để giữ hành vi tìm kiếm nghiệp vụ không phân biệt hoa/thường và phân biệt dấu; chỉ `PublicId`, checksum cùng khóa kỹ thuật dùng `BIN2` để so sánh chính xác.
- Password trong hai baseline chỉ là application hash; token chỉ lưu SHA-256 hash.

## Triển khai

Các script v1 là baseline mới, không chỉnh sửa prototype trong `database/sqlserver/ttsmart.com.vn/` hoặc `database/sqlserver/TTSmart/`. Runner kết nối trực tiếp database đích bằng Windows Authentication, xác nhận `DatabaseInfo.DatabaseKind`, dùng lock ứng dụng và checksum SHA-256 để chống chạy đồng thời hoặc drift.

Baseline này không thay đổi runtime ASP.NET Core/MongoDB và không chuyển dữ liệu MongoDB.
