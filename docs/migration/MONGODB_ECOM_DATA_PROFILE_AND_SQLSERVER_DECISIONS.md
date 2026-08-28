# Hồ sơ dữ liệu MongoDB `Ecom` và quyết định thiết kế SQL Server

> **Quyết định hiện hành ngày 2026-08-24:** giữ nguyên số liệu profile, nhưng các kết luận ownership không có Company DB hoặc cho phép Product authoritative độc lập theo Branch đã bị thay thế. Đích mới là Platform DB → Company DB dùng chung Product Master → Branch DB chứa giao dịch/tồn kho. Xem `../architecture/SQLSERVER_TARGET_ARCHITECTURE.md`.

> Cập nhật ownership ngày 2026-08-14: chủ dự án xác nhận `[TTSmart]` là database bán hàng đầy đủ của TTSmart. Các số liệu profile và phát hiện chất lượng dữ liệu trong tài liệu này vẫn có hiệu lực, nhưng kết luận cũ đặt Product/Customer vào `[ttsmart.com.vn]` và chứng từ ra database chi nhánh đã bị thay thế. Thiết kế hiện hành nằm tại `TTSMART_CODE_AND_DATA_DISCOVERY.md` và `SQLSERVER_TTSMART_SCHEMA_DESIGN.md`.

## 1. Phạm vi và an toàn dữ liệu

Hồ sơ này được lập ngày 2026-08-14 sau khi chủ dự án cho phép đọc database `Ecom`. Nguồn được khảo sát là MongoDB 8.0.26 chạy local tại `127.0.0.1:27017`, không sử dụng `.env` và không kết nối production. Chỉ thực hiện command/aggregate/read với application name `TTSmartEcomReadOnlyProfiler`.

Kết quả chỉ ghi thống kê tổng hợp. Không xuất document thô, tên/số điện thoại/nội dung chat, password hash, OTP, autologin token, provider token, secret hoặc dữ liệu upload. Không thực hiện insert/update/delete, seed, migration hay tạo index.

Đây là profile của snapshot local được quan sát, không tự động chứng minh dữ liệu production có cùng phân bố.

## 2. Snapshot vật lý

- 19 collection, 1.503 document.
- Dung lượng BSON logic khoảng 1.187.275 byte; storage khoảng 843.776 byte.
- Source khai báo/suy ra 21 collection, nhưng snapshot thực tế có chênh lệch:
  - không tồn tại `drinks`, `drinktoppings`, `drinkbills`, `drinkowelists`;
  - tồn tại thêm `autologintokens` và `chatmessages`, nhưng không tìm thấy model/consumer tương ứng trong source hiện tại.

| Collection | Document | Index ngoài `_id` | Quyết định sơ bộ |
|---|---:|---|---|
| `activitylogs` | 383 | TTL `createdAt`, 90 ngày | archive legacy có chọn lọc; audit SQL mới thiết kế lại |
| `autologintokens` | 0 | unique `token` | không migrate; loại collection cũ sau khi xác minh retention |
| `brands` | 29 | không | chuyển Company catalog |
| `chatmessages` | 3 | `sessionId` | PII dormant; quarantine/archive hoặc xóa theo retention, không đưa Core |
| `chips` | 1 | không | chuẩn hóa thành attribute definition/value |
| `counters` | 1 | unique `id` | thay bằng sequence/allocator transactional |
| `eporders` | 24 | không | chuyển ExportIssue header/line |
| `iporders` | 124 | không | chuyển ImportReceipt header/line |
| `manages` | 1 | không | module storefront riêng TTSmart |
| `orders` | 37 | unique `orderCode` | chuyển SalesOrder header/line |
| `products` | 316 | `nameUnsigned`; sparse unique `code` | Product Master cấp Company; stock tách khỏi Product |
| `sections` | 1 | không | chuẩn hóa Category/Attribute |
| `stations` | 5 | không | module Trạm riêng TTSmart; thêm unique code |
| `storagehistories` | 528 | không | lưu bằng chứng legacy; không dùng làm ledger authoritative |
| `telegramconfigs` | 1 | không | integration tùy chọn; secret reference |
| `types` | 31 | không | ProductType cấp Company |
| `users` | 16 | unique `phone` | tách Platform identity, employee và customer profile |
| `voicevocabs` | 1 | không | feature AI Voice tùy chọn cấp Company |
| `zaloconfigs` | 1 | không | integration tùy chọn; không copy secret/token |

## 3. Chất lượng dữ liệu có ảnh hưởng trực tiếp tới DDL

### 3.1 Product và catalog

- 316 Product và đúng 316 Variant; mỗi Product hiện có đúng một Variant.
- 301 Product có code, không có duplicate sau khi trim/lowercase; 15 Product chưa có code.
- Tên Product có 6 nhóm trùng chuẩn hóa, bao gồm 13 document; không được đặt unique theo tên hoặc tự động merge.
- `nameUnsigned` chỉ có ở 52/316 Product; 264 Product thiếu. SQL phải sinh search key từ tên thay vì migrate nguyên cache này.
- Giá bán và giá nhập của Variant:
  - 153 giá là chuỗi số nguyên hợp lệ;
  - 163 giá là chuỗi rỗng;
  - không có giá âm trong snapshot.
- Giá rỗng phải chuyển thành `NULL`, không chuyển thành 0.
- VAT Product không đồng nhất: 214 missing/null, 36 chuỗi rỗng, 48 giá trị `8%`, 17 giá trị `10%`, một giá trị `10`. Cần parser versioned và giữ `LegacyRawVat` trong staging.
- Lookup hiện có không bao phủ toàn bộ string đang lưu trên Product:
  - 28 Product dùng brand không có trong `brands`;
  - 51 Product dùng type không có trong `types`;
  - 46 Product dùng section/value không khớp `sections`.
- Khi migrate phải tạo catalog từ hợp của lookup và giá trị thực tế trên Product, đánh dấu item phát hiện từ legacy để owner rà soát. Không quarantine Product chỉ vì lookup cũ thiếu.

### 3.2 Tồn kho và lịch sử

- `quantityForSale` và `quantityInStorage` đều là số nguyên, không âm trong toàn bộ 316 Variant.
- Tổng `quantityForSale` là 9.765; tổng `quantityInStorage` là 9.826.
- 309 Product có hai số tồn bằng nhau; 7 Product có tồn kho vật lý lớn hơn tồn bán; không có Product có tồn bán lớn hơn tồn vật lý.
- `storagehistories` có 528 document nhưng 515 document không có `source`.
- Có 56 history tham chiếu Product không còn tồn tại; tổng signed quantity của nhóm orphan là 12.205.
- Trong 146 Product còn tồn tại có history, chỉ 89 Product có tổng history khớp tồn hiện tại; 57 Product không khớp cả tồn bán lẫn tồn vật lý.

Kết luận: không được rebuild opening stock từ `storagehistories`. Nguồn mở sổ SQL phải là số tồn hiện tại đã được owner đối soát. `storagehistories` được chuyển sang vùng legacy evidence; các dòng orphan phải giữ với `LegacyProductObjectId` và migration exception/tombstone, không được bỏ im lặng.

### 3.3 Đơn bán, nhập và xuất

| Chỉ tiêu | Sales Order | Import Order | Export Order |
|---|---:|---:|---:|
| Header | 37 | 124 | 24 |
| Line | 52 | 2.071 | 277 |
| Line orphan Product | 0 | 17 | 1 |
| Header total đối chiếu được | không đủ snapshot giá line | 124 | 24 |
| Header total sai so với line | chưa xác định | 0 | 0 |
| Closed thiếu `completedAt` | 1 | 36 | 4 |

- Toàn bộ `productId` trong import/export snapshot này là BSON string.
- Giá import/export là chuỗi số nguyên; quantity/progress là số nguyên, không âm và không có progress vượt quantity.
- 18 line import/export orphan phải vào `MigrationExceptions` hoặc map tới Product tombstone. Không được xóa line, không tự tạo Product kinh doanh hoạt động từ một reference không đủ dữ liệu.
- Sales Order có hai draft rỗng; toàn bộ 52 line đều resolve được Product/Variant hiện tại.
- Không Sales Order nào persist Station attribution. Dữ liệu cũ phải để `AttributionStatus = Unknown`; không suy diễn từ membership hiện tại của User.
- Trạng thái `completedAt` thiếu phải giữ null kèm cờ migration; không bịa thời gian hoàn tất.

### 3.4 User, Station và storefront

- 16 User: 1 `superadmin`, 4 `admin`, 8 `staff`, 3 `customer`.
- 16/16 password có dạng bcrypt tương thích; 9 User còn `logInString`. Không copy `logInString` sang SQL; buộc rotate/revoke autologin trong cutover.
- User legacy không có `createdAt`/`updatedAt`; không dùng thời điểm migration làm thời điểm tạo tài khoản. Dùng `MigratedAt` riêng và để business timestamp là null/unknown.
- 5 Station có code khác nhau sau chuẩn hóa nhưng chưa có unique index. Có 188 Station–Product reference và không có orphan.
- Ba trong năm Station thiếu raw field `allowPublicSignup`; compatibility default hiện tại là `true`. Migration phải ghi rõ default đã áp dụng.
- Station và Manage đều không có timestamps trong snapshot.
- `manages` là singleton chứa 18 Product reference, không orphan, bốn policy và chín home-category item.

Kết luận: Product Master và customer identity đang được dùng chung trong phạm vi TTSmart; Station chỉ là kênh/điểm giới thiệu sản phẩm, không phải Branch. Đây là bằng chứng ủng hộ Company-scoped Product/Customer master và một Station extension riêng.

### 3.5 Provider, chat và collection dormant

- Zalo document có các field credential nhưng ba field nhạy cảm được kiểm tra đều rỗng trong snapshot. Dù vậy schema SQL không được có cột plaintext tương ứng.
- Telegram có một recipient/chat id; đây là dữ liệu nhạy cảm và chỉ được chuyển nếu integration tiếp tục được dùng.
- `chatmessages` có ba document chứa tên, số điện thoại và nội dung; source hiện tại không có model/consumer. Không đưa vào Core SQL. Owner phải chọn retention archive hoặc xóa có kiểm soát.
- `autologintokens` rỗng và không có consumer source hiện tại; không cần tạo bảng SQL từ collection này.
- Bốn collection đồ uống không tồn tại trong snapshot và router không được mount; không tạo DDL.

## 4. Phương án kiến trúc lịch sử ngày 2026-08-14

Toàn bộ mục 4 dưới đây được giữ để truy vết quá trình ra quyết định, không còn là kiến trúc được khuyến nghị sau ngày 2026-08-24.

```text
[ttsmart.com.vn]
├── công ty, chi nhánh, database registry
├── identity, membership, role, permission
├── Product/Customer master theo CompanyId
├── feature, quota, support session, platform audit
└── provisioning/schema-version/outbox

[TTSmart]
├── Station/public link/Station–Product
├── storefront/policy/home section
├── Customer–Station/order attribution
└── cấu hình/module đặc thù TTSmart

[{BranchCode}_online]
├── Product/Variant projection từ [ttsmart.com.vn]
├── warehouse/location/stock ledger/balance
├── sales/import/export transaction
├── business audit
└── outbox/inbox/idempotency
```

### 4.1 Quyết định lịch sử không tạo `CompanyDb` — đã bị thay thế

Ngày 2026-08-14, chủ dự án từng chốt ba loại database vật lý: `[ttsmart.com.vn]` là database tổng, `[TTSmart]` là database riêng và `[{BranchCode}_online]` là các database chi nhánh cùng template. Quyết định này đã bị thay thế ngày 2026-08-24 bởi Company DB riêng sở hữu Product Master dùng chung và Branch DB sở hữu giao dịch/tồn kho.

Quy tắc ownership lịch sử:

- `[ttsmart.com.vn]` sở hữu Company/Branch, identity, Product/Variant/Customer master, feature/quota và database registry;
- `[TTSmart]` sở hữu Station/storefront/provider/module riêng TTSmart;
- BranchDb sở hữu giá chi nhánh, tồn kho và chứng từ;
- BranchDb có `BranchProductVariants` chứa external `PlatformProductVariantId`, SKU/name/UOM snapshot cần cho vận hành;
- không tạo FK xuyên database; đồng bộ bằng outbox/inbox và idempotency.

### 4.2 `[ttsmart.com.vn]`

Nhóm bảng đề xuất:

- `Users`, `UserPasswords`, `UserLogins`, `UserSessions`, `SuperAdmins`;
- `Companies`, `Branches`, `TtsmartDatabase`, `BranchDatabases`;
- `DatabaseProvisioningJobs`, `DatabaseSchemaVersions`;
- `CompanyUsers`, `BranchUsers`, `Roles`, `Permissions`, `RolePermissions`, `CompanyUserRoles`, `BranchUserRoles`;
- `Features`, `CompanyFeatures`, `BranchFeatures`, `CompanyFeatureSettings`, `BranchFeatureSettings`;
- `AiBalances`, `AiTransactions`, `AiReservations`, `AiUsageLogs`;
- `SupportSessions`, `SupportPermissions`, `SystemAuditLogs`, `SecurityLogs`;
- `Products`, `ProductVariants`, `ProductCodes`, `Brands`, `ProductTypes`, `Categories` theo `CompanyId`;
- `AttributeDefinitions`, `AttributeValues`, `UnitsOfMeasure`, `PriceLists`, `PriceListItems`;
- `Customers`, `CustomerContacts`, `CustomerAddresses`, `CustomerConsents` theo `CompanyId`;
- `OutboxMessages`, `ProcessedRequests`, `MigrationRuns`, `MigrationErrors`.

Super Admin là system principal riêng, không phải role nghiệp vụ có thể cấp cho nhân viên.

### 4.3 `[TTSmart]`

Database này không chứa Core của công ty khách và không thay thế BranchDb. Nó chỉ chứa module/cấu hình riêng TTSmart:

- `tts_station.Stations`, `StationPublicLinks`, `StationProducts`;
- `CustomerStationMemberships`, `StationOrderAttributions`;
- `storefront.StorefrontSettings`, `Policies`, `HomeSections`, `HomeSectionProducts`;
- `voice.Vocabularies`, `Aliases`, `CodePatterns` nếu AI Voice tiếp tục là feature.
- `integration.ProviderSettings` chỉ giữ metadata và `SecretReference`;
- cart/order-template của website TTSmart nếu nghiệp vụ còn dùng;
- `AuditLogs`, `OutboxMessages`, `InboxMessages`, `ProcessedRequests`.

### 4.4 `BranchDbTemplate`

Catalog projection và kho:

- `DatabaseMetadata`, `SchemaMigrations`;
- `BranchProductVariants` tham chiếu external `PlatformProductVariantId`;
- `BranchPriceOverrides`, `CustomerBranchProfiles`;
- `Warehouses`, `StockLocations`;
- `StockBalances`, `StockReservations`;
- `StockMovements`, `StockMovementLines`, `StockMovementReversals`.

Giao dịch:

- `SalesOrders`, `SalesOrderLines`, `SalesOrderStatusHistory`;
- `ImportReceipts`, `ImportReceiptLines`;
- `ExportIssues`, `ExportIssueLines`;
- `SalesChannelAttributions` dùng channel generic; Station detail nằm trong extension TTSmart;
- `BusinessAuditLogs`, `BusinessAuditDetails`;
- `OutboxMessages`, `InboxMessages`, `ProcessedRequests`;
- `LegacyStockHistory`, `LegacyReferenceMappings`, `MigrationExceptions` trong thời gian migration/rollback.

Mọi BranchDb phải có cùng `TemplateId` và `SchemaVersion`. Không thêm bảng riêng vào một chi nhánh. Tính năng tùy chọn dùng schema chung và được bật qua `CompanyFeatures`/`BranchFeatures`. Không cần tạo Supplier/PurchaseOrder, lot/serial hoặc accounting ledger chỉ từ dữ liệu hiện tại vì Mongo chưa có bằng chứng cho các nghiệp vụ đó.

## 5. Quy tắc DDL quan trọng

- PK mới dùng `uniqueidentifier`; mỗi bảng migrate có `LegacyMongoId char(24)` hoặc mapping table với filtered unique index.
- Tiền dùng `decimal(19,4)` và currency; giá legacy rỗng thành null.
- Quantity dùng `decimal(19,4)` dù snapshot hiện chỉ có integer, để không khóa thiết kế UOM tương lai.
- Thời gian dùng `datetimeoffset(7)` UTC; không tự sinh business timestamp còn thiếu.
- Mọi bảng mutable quan trọng có `rowversion`.
- Order/receipt/issue line dùng `ProductVariantId` ổn định và giữ SKU/name/price/VAT/UOM snapshot.
- Posted StockMovement và audit là append-only; sửa sai bằng reversal.
- Unique theo normalized code/SKU trong đúng Company scope; Product name không unique.
- Station code/slug unique, token public chỉ lưu hash, có expiry/revoke/rate limit.
- Không có FK vật lý xuyên database; external ID phải được backend kiểm tra scope.
- Provider/database credential chỉ lưu `SecretReference`.

Index tối thiểu cần thiết:

- Product: normalized code/SKU unique, normalized name/search key, type/brand/category;
- Branch projection: external PlatformProductVariantId unique và local SKU unique;
- Stock: `(ProductVariantId, StockLocationId)` unique, movement `(ProductVariantId, PostedAt)`, source document/idempotency unique;
- Sales/import/export: document code unique, status/date, customer/date, line ProductVariantId;
- CompanyUsers/BranchUsers: unique active `(UserId, CompanyId)` và `(UserId, BranchId)`;
- audit/outbox: actor/time, target/time, correlation, status/next-attempt;
- Station: normalized code/slug unique và Station–Product active pair unique.

## 6. Quy tắc migration từ snapshot `Ecom`

1. Tạo immutable staging giữ raw BSON type, raw value cần thiết, source ObjectId, batch và checksum; redact credential/PII không cần thiết.
2. Sinh GUID mapping xác định cho Product, Variant, User, Station và document transaction.
3. Xây catalog từ hợp của lookup collection và giá trị thực tế trên Product; không merge tên trùng tự động.
4. Chuyển 316 Product và 316 Variant vào schema catalog của `[ttsmart.com.vn]` với `CompanyId` TTSmart; 15 Product thiếu code nhận nullable code hoặc migration-generated internal key, không giả làm business SKU.
5. Tạo Branch projection cho chi nhánh TTSmart pilot.
6. Tạo opening StockMovement từ số tồn hiện tại sau đối soát; không replay 528 history để dựng balance.
7. Chuyển history vào `LegacyStockHistory`; giữ 56 orphan bằng legacy reference/tombstone.
8. Chuyển order/import/export header và line; 18 line orphan vào exception có owner xử lý, không silent drop.
9. Chuyển User theo field ownership; giữ bcrypt hash để đăng nhập tương thích, loại `logInString` và rotate session/autologin.
10. Chuyển Station/Manage sang `[TTSmart]`; order cũ có Station attribution unknown.
11. Không migrate `autologintokens`; `chatmessages` chỉ xử lý sau quyết định retention; không tạo bảng drinks.
12. Đối soát count, tiền, trạng thái, line, tồn mở sổ, orphan, media và permission trước pilot cutover.

## 7. Đề xuất lịch sử để owner chốt — đã bị thay thế một phần

Dựa trên dữ liệu quan sát tại thời điểm 2026-08-14, tài liệu từng đề xuất các mặc định dưới đây. Mục 1–2 không còn authoritative sau quyết định Company DB ngày 2026-08-24; các nhận định dữ liệu còn lại vẫn cần được đánh giá theo mapping field-level:

1. Product Master dùng chung trong `[ttsmart.com.vn]` theo `CompanyId`; Branch giữ giá override, projection và tồn kho.
2. Customer Master dùng chung trong `[ttsmart.com.vn]` theo `CompanyId`; Branch giữ profile nghiệp vụ riêng nếu cần.
3. Current Product stock sau đối soát là nguồn opening balance; history cũ chỉ là legacy evidence.
4. Product/Variant vẫn tách thành hai bảng dù hiện tại mỗi Product chỉ có một Variant.
5. Station là extension trong `[TTSmart]`, không phải Branch.
6. Chat cũ không thuộc Core; cần owner chọn archive có mã hóa hoặc xóa theo retention.
7. Quantity dùng decimal và có UOM từ đầu; chưa xây lot/serial khi chưa có yêu cầu.

Các quyết định còn cần nghiệp vụ xác nhận là cách tính `quantityForSale` so với `quantityInStorage`, giá vốn, rule VAT/rounding, mã chứng từ theo Branch/năm, retention audit/chat và RPO/RTO từng database.

## 8. Ranh giới giai đoạn

Profile và kiến trúc này chuẩn bị cho Đợt 2. Không đưa SQL Server, EF Core, migration DDL hay chức năng multi-company vào runtime Đợt 1 cho tới khi có chỉ dẫn thay đổi phạm vi riêng và các quyết định ở trên được chốt.

## 9. Profile bổ sung chức năng lốp ngày 2026-08-27

Snapshot local đã thay đổi sau khi legacy bổ sung hai model mới. Lượt đọc an toàn mới quan sát 21 collection/1.670 document, trong đó `vehicles=7` và `tireorders=6`. Các reference hiện tại và invariant đơn–vị trí–tổng–tồn đều khớp, nhưng có hai Vehicle thiếu `wheelCount`, deletion field missing ở một số đơn và nhiều history/activity trỏ tới đơn đã từng bị xóa khỏi collection.

Hai collection lốp chưa có Branch schema/mapper/dry-run/reconcile trong V2 nên vẫn `Blocked`. Hồ sơ đầy đủ, không chứa PII/ID, nằm tại `MONGODB_ECOM_TIRE_PROFILE_2026-08-27.md`.
