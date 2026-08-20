# DDL đã triển khai cho database tổng `[ttsmart.com.vn]`

> Ghi chú ownership ngày 2026-08-14: migration `004` đã tạo catalog/customer theo giả định cũ. Preflight xác minh mười bảng đều rỗng, không có dependency ngoài danh sách; forward migration `008_RemoveSalesTables.sql` đã loại chúng theo thứ tự an toàn. Product/customer TTSmart nay thuộc `[TTSmart]`; database tổng không còn ownership nghiệp vụ bán hàng.

## Phạm vi thực hiện ngày 2026-08-14

Theo quyền riêng cho Đợt 2, database tổng `[ttsmart.com.vn]` đã được tạo trên `DESKTOP-5O6VV3J\SQLEXPRESS` bằng Windows Authentication. Đây là schema control-plane và master data dùng chung. Không có database `[TTSmart]`, database `[{BranchCode}_online]`, bảng đồng bộ, bảng migration dữ liệu, Entity Framework Core hay thay đổi runtime Đợt 1 nào được tạo trong công việc này.

DDL có tại `database/sqlserver/ttsmart.com.vn/` và được quản lý bằng tám migration sau `000_CreateDatabase.sql`:

| Nhóm | Migration | Bảng |
|---|---|---|
| Core | 001 | `SchemaVersions`, `Companies`, `Branches`, `SecretReferences`, `BranchDatabases` |
| Identity và quyền | 002 | `Users`, `UserLogins`, `UserPasswords`, `CompanyUsers`, `BranchUsers`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles` |
| Tính năng | 003 | `Features`, `CompanyFeatureSettings`, `BranchFeatureSettings` |
| Catalog chung | 004 | `Brands`, `ProductTypes`, `Categories`, `Units`, `Products`, `ProductCategories`, `ProductVariants`, `Customers`, `CustomerContacts`, `CustomerAddresses` |
| AI | 005 | `AiBalances`, `AiTransactions`, `AiUsageLogs` |
| Provisioning và audit | 006 | `ProvisioningJobs`, `ProvisioningSteps`, `AuditLogs` |
| Ranh giới company | 007 | bổ sung khóa ngoại cùng-company cho catalog, branch membership và AI usage |
| Ownership | 008 | loại 10 bảng catalog/customer rỗng khỏi database tổng |

Tổng schema vật lý sau migration 008 là 23 bảng trong duy nhất `dbo`.

## Quyết định thiết kế quan trọng

- Tất cả định danh nghiệp vụ là `uniqueidentifier`; bản ghi có thể thay đổi dùng `Version bigint`, `CreatedAtUtc`, `UpdatedAtUtc`, và `rowversion`. Bản ghi master/membership/catalog dùng `IsDeleted` khi cần giữ lịch sử. Ledger AI, audit và lịch sử schema là append-only.
- Mọi thời điểm dùng `datetime2(7)` theo UTC. Giá AI và giá catalog dùng `decimal(19,4)`; schema không dùng `money`, `float`, `real`, `text` hay `ntext`.
- `CompanyCode` là duy nhất toàn hệ thống; `BranchCode` là duy nhất trong `CompanyId`. Các mã login, feature, role và catalog có cột normalized và index duy nhất phù hợp.
- `BranchDatabases` chỉ lưu `ServerAlias`, `DatabaseName`, `SqlLoginName` do hệ thống sinh, `SecretReferenceId`, trạng thái provisioning, phiên bản schema và thời điểm health-check. Bảng không có cột password SQL, connection string hay secret value. `DatabaseName` bắt buộc hậu tố `_online` và chỉ chấp nhận chữ, số, dấu gạch dưới.
- `ProvisioningJobs` và `ProvisioningSteps` dùng tám trạng thái: `Pending`, `Creating`, `Migrating`, `Seeding`, `Validating`, `Active`, `Failed`, `Disabled`. Mã lỗi và chi tiết lỗi đều giới hạn, chỉ được ghi nội dung đã redaction.
- `CompanyUsers` xác lập user thuộc công ty; `BranchUsers` bắt buộc tham chiếu đồng thời branch, company membership và user tương ứng. Migration 007 bảo đảm không thể gán branch, product/category/brand/unit hoặc AI usage chéo company qua khóa ngoại.
- `UserRoles` có đúng một membership company hoặc branch. SQL không thể biểu diễn CHECK phụ thuộc vào dòng `Roles.ScopeType`; application phải kiểm tra role company chỉ được gán cho `CompanyUsers`, còn role branch chỉ được gán cho `BranchUsers`.
- JSON chỉ dùng cho cấu hình feature và chi tiết audit đã lọc, đều có `ISJSON` check. Không có payload provider, prompt AI, token hay file nhị phân trong schema này.

## Kết quả xác minh đã chạy

Các truy vấn metadata trên database local đã cho kết quả:

| Hạng mục | Kết quả |
|---|---:|
| Bảng người dùng | 23 |
| Foreign key | 35 |
| Check constraint | 92 |
| Unique constraint | 18 |
| Index (gồm PK/unique/nonclustered) | 70 |
| Migration đã ghi nhận | 001–008 |
| Cột có tên connection string, database/SQL password, secret value, access token, API key | 0 |
| Cột có kiểu bị cấm | 0 |

Đã chạy lại toàn bộ script sau khi tạo và kết quả là tám migration hiện hữu được bỏ qua; số migration vẫn là 8, số bảng vẫn là 23. Không seed dữ liệu nghiệp vụ.

## Giới hạn và phần chưa xác minh

- Chưa có provisioning service; do đó secret manager, việc sinh SQL Login và việc tạo database branch chỉ mới có schema quản lý, chưa được gọi.
- `[TTSmart]` đã được tạo bằng DDL độc lập; chưa tạo template branch hay bất kỳ database branch thực tế nào.
- Chưa thực hiện chuyển đổi MongoDB sang SQL Server, chưa xác minh mapping dữ liệu production và chưa kiểm thử tải/index plan thực tế.
- `SchemaVersions.ScriptChecksum` được để `NULL`: không đưa self-hash sai lệch vào DDL. Khi có migration runner chính thức, runner phải tính checksum artifact và ghi trước khi áp dụng migration mới.
