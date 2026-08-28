# Phương án schema SQL Server cho database `[TTSmart]`

> **Trạng thái lịch sử:** phương án `[TTSmart]` là database bán hàng đầy đủ và làm template Branch đã bị thay thế ngày 2026-08-24. Đích mới coi `[TTSmart]` là Company DB của TTSmart; Product Master/dữ liệu dùng chung ở đây, còn giao dịch và tồn kho riêng nằm tại Branch DB. Không áp dụng nguyên trạng thiết kế này để tạo schema mới. Xem `../architecture/SQLSERVER_TARGET_ARCHITECTURE.md`.

## 1. Mục tiêu

`[TTSmart]` là database bán hàng đầy đủ của công ty TTSmart. Schema phải chạy được toàn bộ nghiệp vụ quan sát được của web cũ, nhận được toàn bộ dữ liệu hiện có trong MongoDB `Ecom` và làm nền cho template database chi nhánh sau này.

Thiết kế này chưa phải DDL đã duyệt. Chưa tạo database, chưa tạo bảng và chưa copy dữ liệu.

## 2. Ranh giới database

```text
[ttsmart.com.vn]
├── Company, Branch và database registry
├── tài khoản, password hash, role và permission cấp nền tảng
├── feature, quota, provisioning và audit hệ thống
└── không phải nguồn authoritative cho giao dịch bán hàng TTSmart

[TTSmart]
├── Product, Customer và dữ liệu bán hàng TTSmart
├── tồn kho, nhập, xuất và đơn bán
├── Station, storefront, voice và integration metadata
└── audit nghiệp vụ và bằng chứng migration legacy

[{BranchCode}_online]
└── dùng lại sales core sau này, không thuộc phạm vi thiết kế vật lý hiện tại
```

Không tạo foreign key xuyên database. `CompanyId`, `PlatformUserId` hoặc ID nền tảng trong `[TTSmart]` là logical reference được application kiểm tra.

Nguyên tắc vận hành đã chốt:

- mỗi chi nhánh có tồn kho và chứng từ riêng;
- mỗi database bán hàng có thể có catalog riêng;
- không yêu cầu đồng bộ thay đổi giữa các chi nhánh theo thời gian thực;
- ưu tiên cài local, tự vận hành và cô lập dữ liệu.

Do đó `[TTSmart]` và mỗi `[{BranchCode}_online]` phải tự chứa đủ dữ liệu phục vụ nghiệp vụ của mình. Database tổng không sở hữu Product master và ứng dụng bán hàng không được phụ thuộc vào query xuyên database tổng.

## 3. Nguyên tắc chung

- Tất cả bảng dùng schema `dbo`.
- Tên bảng PascalCase, số nhiều, dùng từ cơ bản và dễ tìm.
- ID mới dùng `uniqueidentifier`.
- Document/subdocument MongoDB giữ `LegacyObjectId char(24)` hoặc entry trong `LegacyIds`.
- Tiền dùng `decimal(19,4)`; phần trăm dùng decimal phù hợp.
- Thời gian dùng `datetime2(7)` theo UTC.
- Không dùng `money`, `float`, `real`, `text`, `ntext` hoặc `image`.
- Giá rỗng ở MongoDB thành `NULL`, không thành 0.
- Bảng mutable có `Version bigint`, `UpdatedAtUtc` và `rowversion`; thêm `IsDeleted` khi cần giữ lịch sử.
- Không gán thời gian migration vào `CreatedAtUtc` khi source không có thời gian tạo; dùng `MigratedAtUtc` riêng.
- Không dùng xóa cascade cho chứng từ, audit, lịch sử kho hoặc mapping migration.
- Dữ liệu lỗi vẫn được giữ bằng FK nullable, ID legacy và `DataStatus`.

## 4. Danh sách bảng đề xuất

| Nhóm | Bảng |
|---|---|
| Hệ thống/migration | `DatabaseInfo`, `SchemaVersions`, `MigrationRuns`, `MigrationIssues`, `LegacyIds`, `NumberSequences` |
| Catalog | `Brands`, `ProductTypes`, `Categories`, `CategoryValues`, `Units`, `ProductOptions`, `Products`, `ProductVariants`, `ProductFiles`, `ProductReviews` |
| Người dùng/khách hàng | `Users`, `Customers`, `CustomerContacts`, `CustomerAddresses`, `Carts`, `CartItems`, `OrderTemplates`, `OrderTemplateItems` |
| Bán hàng | `SalesOrders`, `SalesOrderItems`, `SalesOrderFiles` |
| Kho | `Stocks`, `StockMovements`, `LegacyStockHistories` |
| Nhập kho | `ImportOrders`, `ImportOrderItems`, `ImportOrderFiles` |
| Xuất kho | `ExportOrders`, `ExportOrderItems`, `ExportOrderFiles` |
| Station/storefront | `Stations`, `StationProducts`, `UserStations`, `StorefrontSettings`, `StorefrontImages`, `StorefrontSections`, `StorefrontSectionProducts`, `HomeCategories`, `Policies`, `PolicySections` |
| Voice/integration | `VoiceWords`, `VoiceAliases`, `VoiceAliasValues`, `Integrations`, `NotificationRecipients` |
| Audit/archive | `ActivityLogs`, `ActivityLogDetails`, `ArchivedChatMessages` |

Tên `Users` trong `[TTSmart]` chỉ là projection vận hành, không chứa password. Password hash, login và permission authoritative vẫn thuộc `[ttsmart.com.vn]`.

## 5. Nhóm hệ thống và migration

### 5.1. `dbo.DatabaseInfo`

Một dòng nhận dạng database:

- `DatabaseId uniqueidentifier`
- `CompanyId uniqueidentifier`: logical reference tới database tổng.
- `DatabaseCode nvarchar(50)`: giá trị cố định `TTSMART`.
- `DatabaseName sysname`: `TTSmart`.
- `SchemaVersion int`
- `CreatedAtUtc datetime2(7)`
- `UpdatedAtUtc datetime2(7)`
- `RowVersion rowversion`

### 5.2. `dbo.SchemaVersions`

- `SchemaVersionId uniqueidentifier`
- `MigrationNumber int`
- `MigrationName nvarchar(260)`
- `ScriptChecksum char(64)`
- `AppliedAtUtc datetime2(7)`
- `AppliedBy nvarchar(128)`

`MigrationNumber` và `MigrationName` phải duy nhất.

### 5.3. `dbo.MigrationRuns`

- `MigrationRunId uniqueidentifier`
- `SourceSystem nvarchar(50)`
- `SourceDatabase nvarchar(128)`
- `TargetDatabase nvarchar(128)`
- `Mode nvarchar(20)`: `Profile`, `DryRun`, `Migrate`, `Verify`.
- `Status nvarchar(20)`
- `StartedAtUtc`, `FinishedAtUtc`
- `ReadCount`, `InsertedCount`, `UpdatedCount`, `SkippedCount`, `IssueCount`
- `ToolVersion nvarchar(50)`

Không lưu connection string.

### 5.4. `dbo.MigrationIssues`

- `MigrationIssueId uniqueidentifier`
- `MigrationRunId uniqueidentifier`
- `SourceCollection nvarchar(128)`
- `LegacyObjectId char(24)`
- `IssueType nvarchar(50)`
- `SafeDescription nvarchar(1000)`
- `ResolutionStatus nvarchar(20)`
- `CreatedAtUtc`, `ResolvedAtUtc`

Không lưu raw document hoặc PII không cần thiết.

### 5.5. `dbo.LegacyIds`

- `LegacyId uniqueidentifier`
- `SourceCollection nvarchar(128)`
- `LegacyObjectId char(24)`
- `TargetTable sysname`
- `TargetId uniqueidentifier`
- `MigrationRunId uniqueidentifier`
- `CreatedAtUtc datetime2(7)`

Unique theo `(SourceCollection, LegacyObjectId, TargetTable)` để migration chạy lại không nhân đôi.

### 5.6. `dbo.NumberSequences`

Thay `counters` MongoDB:

- `SequenceId uniqueidentifier`
- `SequenceCode nvarchar(50)`
- `CurrentValue bigint`
- `Prefix nvarchar(20)`
- `UpdatedAtUtc datetime2(7)`
- `RowVersion rowversion`

`SequenceCode` duy nhất.

## 6. Catalog sản phẩm

### 6.1. `dbo.Brands`

- `BrandId uniqueidentifier`
- `LegacyObjectId char(24)`
- `Name nvarchar(200)`
- `NormalizedName nvarchar(200)`
- `Source nvarchar(20)`: `Lookup` hoặc `Product`.
- `Version`, `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted`, `RowVersion`

Unique filtered theo `NormalizedName` khi chưa xóa. Khi migration, lấy hợp của `brands` và brand thực tế trên Product để không mất 28 Product có lookup thiếu.

### 6.2. `dbo.ProductTypes`

- `ProductTypeId uniqueidentifier`
- `LegacyObjectId char(24)`
- `Name nvarchar(200)`
- `NormalizedName nvarchar(200)`
- `Icon nvarchar(120)`
- `Source nvarchar(20)`
- Các trường version/audit/xóa mềm

Lấy hợp của `types` và type thực tế trên Product.

### 6.3. `dbo.Categories`

Tương ứng `sections.Section[].name`:

- `CategoryId uniqueidentifier`
- `LegacyObjectId char(24)` nullable vì section item không phải lúc nào có `_id` ổn định.
- `Name nvarchar(200)`
- `NormalizedName nvarchar(200)`
- `ImageUrl nvarchar(1000)`
- `SortOrder int`
- `Source nvarchar(20)`
- Các trường version/audit/xóa mềm

### 6.4. `dbo.CategoryValues`

Tương ứng `sections.Section[].value[]` và `Product.value`:

- `CategoryValueId uniqueidentifier`
- `CategoryId uniqueidentifier`
- `Name nvarchar(200)`
- `NormalizedName nvarchar(200)`
- `SortOrder int`
- `Source nvarchar(20)`
- Các trường version/audit/xóa mềm

Unique theo `(CategoryId, NormalizedName)`.

### 6.5. `dbo.Units`

- `UnitId uniqueidentifier`
- `Name nvarchar(100)`
- `NormalizedName nvarchar(100)`
- `DecimalScale tinyint`
- Các trường version/audit/xóa mềm

Snapshot hiện có 4 unit chuẩn hóa trong 2.348 line nhập/xuất. Line chứng từ vẫn giữ `UnitName` snapshot, không chỉ giữ FK.

### 6.6. `dbo.ProductOptions`

Thay singleton `chips`:

- `ProductOptionId uniqueidentifier`
- `OptionType nvarchar(30)`: `Color`, `Shape`, `Frame`, `ButtonCount`.
- `Value nvarchar(200)`
- `NormalizedValue nvarchar(200)`
- `SortOrder int`
- Các trường version/audit/xóa mềm

### 6.7. `dbo.Products`

- `ProductId uniqueidentifier`
- `LegacyObjectId char(24)` unique.
- `ProductTypeId uniqueidentifier`
- `BrandId uniqueidentifier`
- `CategoryId uniqueidentifier`
- `CategoryValueId uniqueidentifier`
- `Code nvarchar(100)` nullable.
- `NormalizedCode nvarchar(100)` nullable.
- `Name nvarchar(300)`
- `SearchName nvarchar(300)`
- `IsVisible bit`
- `IsAdjusted bit`
- `VatRate decimal(5,2)` nullable.
- `LegacyVat nvarchar(20)` nullable.
- `Warranty nvarchar(1000)`
- `Solution nvarchar(max)`
- `Description nvarchar(max)`
- `Features nvarchar(max)`
- `OperatingMethod nvarchar(max)`
- `Advantages nvarchar(max)`
- `Specifications nvarchar(max)`
- `PurchaseCount int`
- `TotalRating decimal(19,4)`
- `ReviewCount int`
- `AverageRating decimal(9,4)`
- `CreatedAtUtc`, `UpdatedAtUtc`, `Version`, `IsDeleted`, `RowVersion`

Index/constraint chính:

- Unique filtered trên `NormalizedCode` khi code khác null và chưa xóa.
- Không unique theo Name.
- Check số đếm/rating không âm.
- Composite FK bảo đảm `CategoryValueId` thuộc đúng `CategoryId`.

### 6.8. `dbo.ProductVariants`

- `ProductVariantId uniqueidentifier`
- `LegacyObjectId char(24)`
- `ProductId uniqueidentifier`
- `Position int`
- `SalePrice decimal(19,4)` nullable.
- `ImportPrice decimal(19,4)` nullable.
- `ProfitPercent decimal(9,4)`
- `ImageUrl nvarchar(1000)`
- `Color nvarchar(200)`
- `Shape nvarchar(200)`
- `ButtonCount nvarchar(100)`
- `Frame nvarchar(200)`
- `Note nvarchar(2000)`
- `CreatedAtUtc`, `UpdatedAtUtc`, `Version`, `IsDeleted`, `RowVersion`

Unique theo `(ProductId, Position)` và `(ProductId, LegacyObjectId)` khi có ID legacy. Product hoạt động phải có ít nhất một Variant; quy tắc này được application/service enforce.

### 6.9. `dbo.ProductFiles`

Gộp `infoDoc` và `documents[]`:

- `ProductFileId uniqueidentifier`
- `LegacyObjectId char(24)` nullable.
- `ProductId uniqueidentifier`
- `FileType nvarchar(30)`: `Manual`, `DataSheet`, `Catalog`, `Other`, `Document`.
- `Label nvarchar(200)`
- `Url nvarchar(1000)`
- `SourceType nvarchar(50)`
- `SortOrder int`
- `CreatedAtUtc`, `UpdatedAtUtc`, `IsDeleted`

### 6.10. `dbo.ProductReviews`

- `ProductReviewId uniqueidentifier`
- `LegacyObjectId char(24)`
- `ProductId uniqueidentifier`
- `ReviewerEmail nvarchar(320)`
- `Comment nvarchar(4000)`
- `Rating tinyint`
- `CreatedAtUtc datetime2(7)`
- `IsDeleted bit`

Check `Rating BETWEEN 1 AND 5`. Snapshot hiện không có review nhưng code đang hỗ trợ CRUD nên bảng vẫn cần cho parity.

## 7. User và khách hàng

### 7.1. `dbo.Users`

Projection vận hành của User nền tảng:

- `UserId uniqueidentifier`: dùng cùng ID với `[ttsmart.com.vn].dbo.Users` khi migration identity hoàn tất.
- `LegacyObjectId char(24)` unique.
- `Name nvarchar(200)`
- `Phone nvarchar(32)`
- `Email nvarchar(320)`
- `RoleCode nvarchar(30)` snapshot.
- `IsActive bit`
- `SourceCreatedAtUtc`, `SourceUpdatedAtUtc` nullable.
- `MigratedAtUtc datetime2(7)`
- `Version`, `IsDeleted`, `RowVersion`

Không có password, OTP, token hoặc permission authoritative.

### 7.2. `dbo.Customers`

- `CustomerId uniqueidentifier`
- `UserId uniqueidentifier` nullable, logical reference tới User projection.
- `LegacyUserObjectId char(24)` nullable.
- `Name nvarchar(200)`
- `Phone nvarchar(32)`
- `NormalizedPhone nvarchar(32)`
- `Email nvarchar(320)`
- `Status nvarchar(20)`
- Các trường version/audit/xóa mềm

Ba User role customer có thể map trực tiếp. Bốn số điện thoại trên Sales Order không còn User chỉ giữ snapshot ở đơn; không tự tạo Customer nếu owner chưa duyệt.

### 7.3. `dbo.CustomerContacts`

- `CustomerContactId`, `CustomerId`
- `ContactType nvarchar(20)`
- `Value nvarchar(320)`
- `NormalizedValue nvarchar(320)`
- `IsPrimary`, `IsVerified`
- Các trường version/audit/xóa mềm

### 7.4. `dbo.CustomerAddresses`

- `CustomerAddressId`, `LegacyObjectId`, `CustomerId`
- `Label`, `ReceiverName`, `ReceiverPhone`, `AddressDetail`
- `IsDefault`
- Các trường version/audit/xóa mềm

### 7.5. `dbo.Carts` và `dbo.CartItems`

`Carts`:

- `CartId`, `LegacyUserObjectId`, `UserId`
- `CreatedAtUtc`, `UpdatedAtUtc`, `Version`, `RowVersion`

`CartItems`:

- `CartItemId`, `LegacyObjectId`, `CartId`
- `ProductId`, `ProductVariantId`
- `LegacyProductObjectId`, `LegacyVariantIndex`
- `Quantity`, `IsSelected`, `SortOrder`
- Các trường version/audit

### 7.6. `dbo.OrderTemplates` và `dbo.OrderTemplateItems`

`OrderTemplates`:

- `OrderTemplateId`, `LegacyObjectId`, `UserId`
- `DisplayName`, `Note`, `SortOrder`
- Các trường version/audit/xóa mềm

`OrderTemplateItems`:

- `OrderTemplateItemId`, `LegacyObjectId`, `OrderTemplateId`
- `ProductId`, `LegacyProductObjectId`
- `Quantity`, `SortOrder`
- Các trường version/audit

## 8. Đơn bán

### 8.1. `dbo.SalesOrders`

- `SalesOrderId uniqueidentifier`
- `LegacyObjectId char(24)` unique.
- `OrderCode nvarchar(50)` unique.
- `CustomerId uniqueidentifier` nullable.
- `CustomerName nvarchar(200)` nullable.
- `CustomerPhone nvarchar(32)`
- `Total decimal(19,4)`
- `Status nvarchar(20)`: `Processing`, `Delivering`, `Completed`.
- `State nvarchar(20)`: `Processing`, `Cancelled`.
- `IsPaid bit`
- `CompletedAtUtc datetime2(7)` nullable.
- `CompletedAtMissing bit`
- `LegacyOrderName nvarchar(300)` nullable: giữ extra field có ở 3 document.
- `CreatedAtUtc`, `UpdatedAtUtc`, `Version`, `RowVersion`

Không xóa cứng đơn đã migrate. Index theo `OrderCode`, `CustomerPhone`, `(Status, State, CreatedAtUtc)` và `CompletedAtUtc`.

### 8.2. `dbo.SalesOrderItems`

- `SalesOrderItemId uniqueidentifier`
- `LegacyObjectId char(24)` unique trong đơn.
- `SalesOrderId uniqueidentifier`
- `ProductId uniqueidentifier` nullable.
- `ProductVariantId uniqueidentifier` nullable.
- `LegacyProductObjectId char(24)`
- `LegacyVariantIndex int`
- `Quantity int`
- `UnitPrice decimal(19,4)` nullable.
- `LegacyPrice nvarchar(100)` nullable.
- `LegacyUnit nvarchar(100)` nullable.
- `LegacyNote nvarchar(2000)` nullable.
- `LegacyStatus bit` nullable.
- `SortOrder int`
- `DataStatus nvarchar(20)`

51/52 line không có giá nguồn nên `UnitPrice` phải null. API tương thích có thể tiếp tục join ProductVariant hiện tại để hiển thị, nhưng không được gọi giá hiện tại là giá lịch sử.

### 8.3. `dbo.SalesOrderFiles`

- `SalesOrderFileId`, `SalesOrderId`
- `Url`, `SortOrder`, `CreatedAtUtc`, `IsDeleted`

Snapshot hiện không có file nhưng code hỗ trợ ảnh đơn.

## 9. Tồn kho

### 9.1. `dbo.Stocks`

Nguồn authoritative cho số tồn mở sổ và hiện tại:

- `StockId uniqueidentifier`
- `ProductVariantId uniqueidentifier` unique.
- `QuantityForSale int`
- `QuantityInStorage int`
- `AsOfUtc datetime2(7)`
- `Version bigint`
- `UpdatedAtUtc datetime2(7)`
- `RowVersion rowversion`

Check cả hai quantity không âm. Migration lấy trực tiếp 9.765 và 9.826 từ Product Variant, không cộng lại history.

### 9.2. `dbo.StockMovements`

Dùng cho mutation SQL mới sau cutover:

- `StockMovementId uniqueidentifier`
- `ProductVariantId uniqueidentifier`
- `SourceType nvarchar(30)`
- `SourceId uniqueidentifier` nullable.
- `SourceItemId uniqueidentifier` nullable.
- `QuantityForSaleChange int`
- `QuantityInStorageChange int`
- `PurchaseCountChange int`
- `Note nvarchar(2000)`
- `ActorUserId uniqueidentifier` nullable.
- `OccurredAtUtc datetime2(7)`
- `CreatedAtUtc datetime2(7)`

Bảng append-only. Unique idempotency theo nguồn khi source ID có giá trị.

### 9.3. `dbo.LegacyStockHistories`

Giữ nguyên bằng chứng của 528 `storagehistories`:

- `LegacyStockHistoryId uniqueidentifier`
- `LegacyObjectId char(24)` unique.
- `ProductId uniqueidentifier` nullable.
- `LegacyProductObjectId char(24)`
- `ProductName nvarchar(300)`
- `Quantity int`
- `UserName nvarchar(200)`
- `LegacyOrderId nvarchar(100)`
- `OrderName nvarchar(300)`
- `Note nvarchar(2000)`
- `IsAiScan bit`
- `Source nvarchar(50)` nullable.
- `DataStatus nvarchar(20)`: `Valid`, `Orphan`, `MissingSource` hoặc kết hợp được quy định bằng bảng mã/application.
- `CreatedAtUtc`, `UpdatedAtUtc`
- `MigratedAtUtc`

Bảng không làm ledger authoritative và không ép FK Product bắt buộc.

## 10. Phiếu nhập

### 10.1. `dbo.ImportOrders`

- `ImportOrderId`, `LegacyObjectId`
- `OrderName`, `Note`
- `ActorUserId` nullable, chỉ map khi chắc chắn.
- `UserName` snapshot bắt buộc.
- `Total decimal(19,4)`
- `LegacyTotal nvarchar(100)`
- `IsCompleted bit`
- `CompletedAtUtc` nullable.
- `CompletedAtMissing bit`
- `CreatedAtUtc`, `UpdatedAtUtc`, `Version`, `RowVersion`

### 10.2. `dbo.ImportOrderItems`

- `ImportOrderItemId`, `LegacyObjectId`, `ImportOrderId`
- `ProductId` nullable.
- `LegacyProductObjectId`
- `UnitId` nullable và `UnitName` snapshot.
- `UnitPrice decimal(19,4)` và `LegacyPrice`.
- `VatRate decimal(5,2)` nullable và `LegacyVat`.
- `Quantity`, `ReceivedQuantity`, `StockAppliedQuantity` nullable.
- `IsCompleted`, `Note`, `SortOrder`, `DataStatus`

Orphan vẫn được insert với `ProductId = NULL`.

### 10.3. `dbo.ImportOrderFiles`

- `ImportOrderFileId`, `ImportOrderId`
- `Url`, `SortOrder`, `CreatedAtUtc`, `IsDeleted`

## 11. Phiếu xuất

### 11.1. `dbo.ExportOrders`

Cấu trúc tương tự `ImportOrders`, đổi ID thành `ExportOrderId`.

### 11.2. `dbo.ExportOrderItems`

- `ExportOrderItemId`, `LegacyObjectId`, `ExportOrderId`
- `ProductId` nullable và `LegacyProductObjectId`
- `UnitId` nullable, `UnitName` snapshot.
- `UnitPrice`, `LegacyPrice`
- `ImportPriceSnapshot` nullable.
- `ProfitPercent` nullable.
- `VatRate`, `LegacyVat`
- `Quantity`, `ExportedQuantity`, `StockAppliedQuantity` nullable.
- `StockUpdateSkipped bit`
- `IsCompleted`, `Note`, `SortOrder`, `DataStatus`

### 11.3. `dbo.ExportOrderFiles`

Cấu trúc tương tự `ImportOrderFiles`.

## 12. Station và phân quyền Product

### 12.1. `dbo.Stations`

- `StationId`, `LegacyObjectId`
- `StationCode`, `NormalizedStationCode`
- `Name`, `ImageUrl`, `Location`
- `AllowPublicSignup bit`
- `DefaultApplied bit`: đánh dấu ba record đã áp compatibility default `true` vì source thiếu field.
- `Version`, `CreatedAtUtc` nullable, `UpdatedAtUtc` nullable, `MigratedAtUtc`, `IsDeleted`, `RowVersion`

Unique theo `NormalizedStationCode`.

### 12.2. `dbo.StationProducts`

- `StationProductId`, `StationId`, `ProductId`
- `LegacyProductObjectId`
- `SortOrder`
- Các trường version/audit/xóa mềm

Unique theo `(StationId, ProductId)` khi chưa xóa.

### 12.3. `dbo.UserStations`

- `UserStationId`, `UserId`, `StationId`
- `LegacyStationObjectId`
- `AssignedAtUtc` nullable.
- `MigratedAtUtc`
- `IsDeleted`

## 13. Storefront

### 13.1. `dbo.StorefrontSettings`

Một dòng cho storefront hiện tại:

- `StorefrontSettingId`
- `DisplayPartners`
- `FooterLogo`, `FooterDescription`, `FooterAddress`, `FooterPhone`, `FooterEmail`
- `NewProductUrl`, `TopPurchaseUrl`, `HighestRatingUrl`
- `IntroductionVi`, `IntroductionEn`, `IntroductionZh`
- `MainPolicy`
- `HomeCategoriesConfigured`, `SidebarTitleVi`, `SidebarTitleEn`, `SidebarTitleZh`
- `ShowSidebar`, `ShowQuickCategories`
- `Version`, `CreatedAtUtc` nullable, `UpdatedAtUtc` nullable, `MigratedAtUtc`, `RowVersion`

### 13.2. `dbo.StorefrontImages`

- `StorefrontImageId`, `ImageType`, `Url`, `SortOrder`
- `CreatedAtUtc`, `IsDeleted`

`ImageType` gồm `Overview` và `Partner`; footer/section/home-category image nằm ở bảng sở hữu tương ứng.

### 13.3. `dbo.StorefrontSections`

- `StorefrontSectionId`
- `LegacySectionNumber tinyint`
- `NameVi`, `NameEn`, `NameZh`
- `ImageUrl`, `Link`, `IsVisible`, `SortOrder`
- Các trường version/audit/xóa mềm

### 13.4. `dbo.StorefrontSectionProducts`

- `StorefrontSectionProductId`, `StorefrontSectionId`, `ProductId`
- `LegacyProductObjectId`, `SortOrder`
- Các trường version/audit/xóa mềm

### 13.5. `dbo.HomeCategories`

- `HomeCategoryId`
- `LegacyKey nvarchar(100)`
- `LabelVi`, `LabelEn`, `LabelZh`
- `CategoryType`, `Link`, `Icon`, `ImageUrl`
- `ShowSidebar`, `ShowQuick`, `SortOrder`
- Các trường version/audit/xóa mềm

### 13.6. `dbo.Policies`

- `PolicyId`
- `PolicyKey nvarchar(30)` unique: `purchase`, `warranty`, `shipping`, `privacy`.
- `TitleVi`, `TitleEn`, `TitleZh`
- `SummaryVi`, `SummaryEn`, `SummaryZh`
- `SourceUpdatedAtUtc`
- Các trường version/audit/xóa mềm

### 13.7. `dbo.PolicySections`

- `PolicySectionId`, `PolicyId`, `SortOrder`
- `TitleVi`, `TitleEn`, `TitleZh`
- `ContentVi`, `ContentEn`, `ContentZh`
- Các trường version/audit/xóa mềm

## 14. Voice vocabulary

### 14.1. `dbo.VoiceWords`

- `VoiceWordId`
- `WordType nvarchar(20)`: `Stopword`, `Brand`, `ProductType`.
- `Value nvarchar(300)`
- `NormalizedValue nvarchar(300)`
- `SortOrder`, `IsDeleted`

### 14.2. `dbo.VoiceAliases`

- `VoiceAliasId`
- `AliasType nvarchar(20)`: `Brand`, `ProductType`, `Intent`, `Code`.
- `Name`, `Keyword`, `Label`, `BrandName`, `ProductTypeName`, `Code`, `Compact`
- `SortOrder`, `IsDeleted`

### 14.3. `dbo.VoiceAliasValues`

- `VoiceAliasValueId`, `VoiceAliasId`
- `ValueType nvarchar(20)`: `Alias` hoặc `Pattern`.
- `Value nvarchar(500)`
- `SortOrder`

## 15. Integration

### 15.1. `dbo.Integrations`

- `IntegrationId`
- `ProviderCode nvarchar(30)`: `Zalo`, `Telegram`.
- `IsEnabled`
- `PublicAppId`, `PublicAccountId`, `PublicRecipientId` nullable.
- `SecretReferenceId uniqueidentifier` nullable, logical reference tới secret manager/control plane.
- `Version`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`

Không có cột `SecretKey`, `AccessToken`, `RefreshToken` hoặc bot token.

### 15.2. `dbo.NotificationRecipients`

- `NotificationRecipientId`, `IntegrationId`, `LegacyObjectId`
- `Label`, `RecipientReference`, `RecipientType`
- `IsEnabled`
- `NotifyNewOrder bit`
- `SortOrder`
- Các trường version/audit/xóa mềm

`RecipientReference` là dữ liệu nhạy cảm; cần mã hóa at rest hoặc đưa vào secret store tùy threat model được duyệt.

## 16. Audit và archive

### 16.1. `dbo.ActivityLogs`

- `ActivityLogId`, `LegacyObjectId`
- `ActorUserId` nullable.
- `UserName` snapshot.
- `Action`
- `ProductId` nullable.
- `LegacyProductObjectId` nullable.
- `ProductName` snapshot.
- `CreatedAtUtc`, `UpdatedAtUtc`, `MigratedAtUtc`

### 16.2. `dbo.ActivityLogDetails`

- `ActivityLogDetailId`, `LegacyObjectId`, `ActivityLogId`
- `FieldName`, `OldValue`, `NewValue`, `SortOrder`

Giữ chuỗi legacy để không mất bằng chứng; audit mới sau cutover nên dùng format đã redaction và có actor/entity ID rõ ràng.

### 16.3. `dbo.ArchivedChatMessages`

- `ArchivedChatMessageId`, `LegacyObjectId`
- `SessionId`, `SenderName`, `SenderPhone`, `SenderRole`, `Message`
- `CreatedAtUtc`, `UpdatedAtUtc`, `ArchivedAtUtc`
- `RetentionStatus`

Bảng chứa PII, không thuộc API core và phải có quyền truy cập/retention riêng. Mục đích là không làm mất 3 document dormant hiện hữu.

## 17. Mapping identity cần duyệt

Collection `users` chứa cả identity và dữ liệu bán hàng. Khuyến nghị tách như sau:

| Field/nhóm | Database đích |
|---|---|
| Phone/email/name, password hash, role, permission | `[ttsmart.com.vn]` |
| Projection User để hiển thị/chứng từ | `[TTSmart].dbo.Users` |
| Customer profile/address | `[TTSmart].dbo.Customers` và bảng con |
| Cart | `[TTSmart].dbo.Carts`, `CartItems` |
| Order template | `[TTSmart].dbo.OrderTemplates`, `OrderTemplateItems` |
| Station assignment | `[TTSmart].dbo.UserStations` |
| `logInString`, reset OTP | Không copy; revoke |

Migration `[TTSmart]` không thể được tuyên bố bảo toàn đầy đủ User cho đến khi phần identity ở `[ttsmart.com.vn]` cũng được mapping và xác minh.

## 18. Quy trình migration không mất dữ liệu

1. Lập mapping manifest cho mọi collection, field và subdocument.
2. Preflight fail nếu xuất hiện collection/field chưa mapping.
3. Tạo backup MongoDB được mã hóa và kiểm tra restore ở môi trường cô lập; không commit backup.
4. Tạo `[TTSmart]` và schema đã duyệt.
5. Dry-run toàn bộ, không ghi dữ liệu nghiệp vụ.
6. Chuyển lookup, Product, Variant, file reference và Stocks.
7. Chuyển User projection, Customer, Cart, template và Station assignment.
8. Chuyển Sales/Import/Export header và line, giữ toàn bộ ID legacy và orphan.
9. Chuyển Station/storefront/voice/integration metadata.
10. Chuyển ActivityLog, LegacyStockHistory và ArchivedChatMessage.
11. Copy file vật lý bằng manifest/checksum riêng.
12. Đối chiếu count, tổng tiền, tổng quantity, quan hệ và checksum.
13. Trong cửa sổ cutover, ngừng ghi web cũ; chạy full upsert cuối cho collection thiếu timestamp và delta cho collection có timestamp.
14. Chỉ chuyển ứng dụng sang SQL sau khi đối chiếu đạt và có rollback plan.

Migration phải idempotent theo `LegacyIds`; không dùng thời gian cập nhật đơn thuần cho User, Station hoặc Manage vì các document này thiếu timestamp.

## 19. Tiêu chí đối chiếu tối thiểu

- 316 Product, 316 Variant và đúng hai tổng tồn 9.765/9.826.
- 37 Sales Order, 52 line và 2 draft rỗng.
- 124 Import Order, 2.071 line, 17 orphan được giữ.
- 24 Export Order, 277 line, 1 orphan được giữ.
- 528 Legacy Stock History, 56 orphan và 515 missing-source được giữ.
- 16 User projection, 2 địa chỉ, 1 cart line, 10 template/27 line.
- 5 Station/188 Product reference.
- 1 storefront, 11 section, 18 section Product reference, 4 policy và 9 home category.
- 383 ActivityLog, 444 detail và 128 Product reference đã orphan được giữ.
- 3 ArchivedChatMessage.
- 258 database file reference; file vật lý phải được kiểm tra tồn tại/checksum riêng.
- Không có password rõ, SQL password, connection string, OTP hoặc provider token trong `[TTSmart]`.

## 20. Quyết định còn cần duyệt

1. Dùng forward migration có guard hay rebuild database tổng local để loại các bảng Product/Customer đang rỗng khỏi ownership/DDL?
2. Có duyệt mô hình identity trung tâm + `Users` projection trong `[TTSmart]` hay muốn toàn bộ auth nằm trong database bán hàng?
3. Có tạo Customer từ bốn số điện thoại trên Sales Order không còn User hay chỉ giữ snapshot trên đơn?
4. `ArchivedChatMessages` giữ trong SQL bao lâu và ai được xem?
5. Có revoke toàn bộ chín `logInString` tại cutover không?
6. Chính sách retention của ActivityLog SQL là bao lâu?
7. 258 file reference sẽ được copy sang storage nào và RPO/RTO ra sao?
8. Supplier, nhiều warehouse và payment transaction chưa có model/dữ liệu hiện tại; có đưa vào đợt mở rộng sau parity không?

## 21. Khuyến nghị chốt

Nên duyệt schema theo hai lớp:

- `SalesCore`: catalog, customer, cart, sales, import, export, stock và audit; sau này tái sử dụng cho `[{BranchCode}_online]`.
- `TTSmartExtensions`: Station, storefront, voice và integration riêng TTSmart.

`[TTSmart]` chứa cả hai lớp. Cách này giữ nó là database bán hàng bình thường, đồng thời tránh làm template chi nhánh phải mang theo các module riêng của website TTSmart.

## Cập nhật triển khai DDL ngày 2026-08-14

Theo quyền riêng của Đợt 2, thiết kế này đã được materialize trên SQL Server local qua chín migration `001`–`009`. Schema có 54 bảng `dbo`; các ràng buộc product-variant pair, tiến độ nhập/xuất và index truy vấn chính được đặt ở migration 009. Xem danh sách script và kết quả metadata đã chạy tại `SQLSERVER_TTSMART_DDL_IMPLEMENTATION.md`.

Chưa có dữ liệu MongoDB, file, password hash, token hay secret nào được sao chép. Những quyết định identity, retention, file migration, cutover và extension vẫn là câu hỏi mở, không được suy diễn là đã triển khai runtime.
