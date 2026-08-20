# Mapping MongoDB sang SQL Server baseline v1

## Trạng thái và quy ước

Tài liệu này là manifest thiết kế field-level cho Đợt 2; không phải bằng chứng đã chuyển dữ liệu. Phạm vi đọc nguồn là model Mongoose legacy và DDL baseline v1. Không có document MongoDB, upload, giá trị secret, credential hay PII được sao chép vào tài liệu này.

### Bằng chứng thực thi Ecom ngày 2026-08-15

Profile/migrate test đã chạy trên 1.503 document. Mapper chuẩn hiện có bằng chứng cho `brands`, `types`, `sections` (Category/CategoryValue) và `products` (Product/Variant/Stock). Các dòng `Mapped` còn lại trong manifest này mô tả **rule đích cần triển khai**, không được hiểu là mapper Ecom đã chạy. Cho tới khi mapper tương ứng có dry-run/migrate/reconcile riêng, collection/field đó có trạng thái thực thi `Blocked/raw`: worker giữ Canonical Extended JSON có redaction cùng `MigrationIssues`, không tự suy diễn. Báo cáo count/disposition thực tế: `MONGODB_ECOM_MIGRATION_TEST_REPORT_2026-08-15.md`.

| Trạng thái | Ý nghĩa thực thi |
|---|---|
| `Mapped` | Có đích vật lý baseline v1 **và** quy tắc chuyển, xử lý null/orphan, cùng tiêu chí đối soát được nêu ngay trong dòng; worker vẫn phải ghi `MigrationMappings`. |
| `Archived` | Chỉ vào vùng archive/evidence, không trở thành dữ liệu vận hành. |
| `SecretStore` | Không chép giá trị vào SQL/staging/log; chỉ tạo metadata/reference sau khi secret manager được phê duyệt. |
| `Empty` | Source được quan sát rỗng hoặc không có payload cần chuyển; không tạo bản ghi đích. |
| `Blocked` | Baseline v1 chưa có đủ cột/rule hoặc dữ liệu nguồn chưa đủ; worker phải tạo `MigrationIssues`, không tự suy diễn. |

`Root` của mọi collection có `_id` dùng GUID nội bộ, `PublicId char(24)` bằng ObjectId lowercase-hex, `Version = __v ?? 0`, và một dòng `MigrationMappings` với `SourcePath=''`. Mỗi subdocument có `_id` cũng dùng `PublicId`/mapping riêng; child `_id:false` dùng index zero-based trong `SourcePath`. `createdAt`/`updatedAt` chỉ map khi schema đích có cột tương ứng; missing/null vẫn là null, không dùng thời điểm migration làm thời gian nghiệp vụ. `rowversion` là SQL-only. Chuỗi số chỉ parse bằng parser versioned; raw không parse được phải tạo `MigrationIssues` (không đổi thành `0`). URL/file chỉ là alias hoặc storage key tương đối sau canonicalization, không đọc/chép nội dung file.

## Ma trận collection và field

### `activitylogs`

| Source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `_id` | `ActivityLogs.ActivityLogId`, `PublicId`, mapping root | ObjectId hợp lệ; count root | Mapped |
| `userName` | resolve `Users.DisplayName` → `ActivityLogs.ActorUserId` | Không resolve: `ActorUserId=NULL`, issue `ACTOR_UNRESOLVED`; không tạo User | Mapped |
| `action` | `ActivityLogs.ActionCode` | Required source; missing → issue, không bịa | Mapped |
| `productId` | Không có cột target | Lưu reference và trạng thái resolve trong `SafeDetail` sẽ làm mất cấu trúc; cần cột target | Blocked |
| `productName` | Không có cột target | Không suy diễn từ Product hiện tại | Blocked |
| `details[].field` | `ActivityLogDetails.FieldName` | `SourcePath=details[i]`; giữ thứ tự | Mapped |
| `details[].oldValue`, `details[].newValue` | `ActivityLogDetails.OldValue`, `NewValue` | Redact theo allowlist trước khi persist; count child | Mapped |
| `createdAt` | `ActivityLogs.OccurredAtUtc` | Missing → issue; không substitute migration time | Mapped |
| `updatedAt`, `__v` | Không có cột target | Chưa có policy lưu version/update audit | Blocked |

### Catalog lookup: `brands`, `types`, `chips`, `sections`

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `brands._id` | `Brands.BrandId`, `PublicId`, mapping root | ObjectId/count | Mapped |
| `brands.Brand` | `Brands.Name` | Required theo legacy; missing → issue | Mapped |
| `brands.__v` | `Brands.Version` | missing = 0 | Mapped |
| `types._id` | `ProductTypes.ProductTypeId`, `PublicId`, mapping root | ObjectId/count | Mapped |
| `types.Type` | `ProductTypes.Name` | Required theo legacy | Mapped |
| `types.icon` | `ProductTypes.Icon` | Giữ nguyên chuỗi/null; đối soát count null/non-null | Mapped |
| `types.createdAt`, `types.updatedAt`, `types.__v` | `ProductTypes.SourceCreatedAtUtc`, `SourceUpdatedAtUtc`, `Version` | Ngày hợp lệ giữ UTC/null; `__v` missing = 0 | Mapped |
| `chips._id` | Mapping root chỉ để provenance; không có entity header | Một document sinh nhiều option | Mapped |
| `chips.Color[]` | Mỗi phần tử → `ProductOptions(OptionType='Color', Value)` | `SourcePath=Color[i]`; duplicate legacy cho phép | Mapped |
| `chips.Shapes[]` | `ProductOptions(OptionType='Shape', Value)` | `SourcePath=Shapes[i]` | Mapped |
| `chips.Frames[]` | `ProductOptions(OptionType='Frame', Value)` | `SourcePath=Frames[i]` | Mapped |
| `chips.ButtonCount[]` | `ProductOptions(OptionType='ButtonCount', Value)` | `SourcePath=ButtonCount[i]` | Mapped |
| `chips.__v` | `ProductOptions.Version` | Không có version riêng mỗi phần tử; root version được ghi trong mapping/evidence | Mapped |
| `sections._id` | Mapping root | Một document sinh categories/value; count root | Mapped |
| `sections.Section[i]._id` | `Categories.CategoryId`, `PublicId`, mapping | Nếu thiếu `_id`, cần PublicId compatible mới + issue | Mapped |
| `sections.Section[i].name` | `Categories.Name` | Required theo legacy | Mapped |
| `sections.Section[i].value[j]` | `CategoryValues.Name` dưới Category tương ứng | `SourcePath=Section[i].value[j]`; giữ duplicate nếu source có | Mapped |
| `sections.Section[i].imgUrl` | `Categories.ImageUrl` | Giữ URL/null nguyên trạng; đối soát theo `SourcePath=Section[i]` | Mapped |
| `sections.__v` | `Categories`/`CategoryValues.Version` | Root version không thể đại diện child độc lập; provenance chỉ | Blocked |

### `products`

| Source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `_id`, `__v` | `Products.ProductId`, `PublicId`, `Version`, mapping root | `__v` missing = 0 | Mapped |
| `type`, `brand`, `section`, `value` | resolve lần lượt `ProductTypes`, `Brands`, `Categories`, `CategoryValues` | Không resolve: FK null + issue; worker có thể materialize lookup missing từ giá trị source chỉ khi quyết định catalog được duyệt | Mapped |
| `name` | `Products.Name` | Required; missing → Blocked document | Mapped |
| `nameUnsigned` | `Products.NameUnsigned` | Giữ nguyên chuỗi/null, không tự tái sinh khi migrate | Mapped |
| `display` | `Products.IsVisible` | Boolean giữ nguyên; missing chỉ được dùng default Mongo đã materialize nếu profile chứng minh, đối soát số `true`/`false` | Mapped |
| `code` | `Products.Code` | Trim legacy; rỗng/missing = NULL; đối soát sparse unique, không ép required | Mapped |
| `vat` | `Products.VatRate` + `LegacyVatRaw` | Parse `%`/locale xác định; invalid/ambiguous: raw + issue, rate NULL | Mapped |
| `adjusted` | `Products.IsAdjusted` | Missing dùng legacy default true chỉ khi profile xác nhận missing semantics | Mapped |
| `createdAt`, `updatedAt` | `Products.SourceCreatedAtUtc`, `SourceUpdatedAtUtc` | Preserve null/missing; `CreatedAtUtc`/`UpdatedAtUtc` chỉ dành cho vòng đời SQL | Mapped |
| `variant[i]._id`, `variant[i].price`, `importPrice` | `ProductVariants.ProductVariantId/PublicId`, `Position`, `SalePrice`/`SalePriceRaw`, `ImportPrice`/`ImportPriceRaw` | `SourcePath=variant[i]`; parser versioned; raw luôn giữ, parse lỗi để decimal `NULL` và ghi issue | Mapped |
| `variant[i].earn` | `ProductVariants.Earn`, `EarnRaw` | Parser versioned; lỗi parse giữ raw và issue | Mapped |
| `variant[i].imgUrl` | `ProductVariantFiles.ExternalUrl`, hoặc `FileId` sau file manifest | `SourcePath=variant[i].imgUrl`, `SortOrder=0`; URL không hợp lệ → issue, không đọc nội dung file | Mapped |
| `variant[i].color`, `shape`, `frame` | `ProductVariants.Color`, `Shape`, `Frame` | Chuỗi giữ nguyên, null/missing giữ null; đối soát theo `variant[i]` | Mapped |
| `variant[i].buttonCount` | `ProductVariants.ButtonCount` | Giữ nguyên chuỗi/null, không ép thành số; đối soát theo `variant[i]` | Mapped |
| `variant[i].quantityForSale`, `quantityInStorage` | `Stocks.QuantityForSale`, `QuantityInStorage` sau tạo variant | Opening balance từ source hiện tại; không replay history; sum theo variant | Mapped |
| `variant[i].note` | `ProductVariants.Note` | Giữ nguyên chuỗi/null | Mapped |
| `infoDoc.manual`, `dataSheet`, `catalog`, `others` | `ProductFiles.FileType`, `ExternalUrl`, `SourcePath`, `SortOrder` | Tạo một dòng theo URL hiện diện, có file type cố định theo path; URL lỗi → issue + bản ghi raw trong `LegacyRecords` | Mapped |
| `documents[i]._id`, `label`, `url`, `sourceType` | `ProductFiles.PublicId`, `Label`, `ExternalUrl`, `FileType`, `SourcePath`, `SortOrder` | Giữ path/index; `_id` hợp lệ dùng PublicId, còn lại phát PublicId tương thích và ghi issue/mapping | Mapped |
| `purchaseCount` | `Products.PurchaseCount bigint` | Giữ aggregate nguồn; không suy từ history; null/missing giữ null | Mapped |
| `reviews[i]._id`, `email`, `comment`, `rating`, `createdAt` | `ProductReviews.PublicId`, `ReviewerEmail`, `Comment`, `Rating`, `CreatedAtUtc` | Giữ email/null trong Operational database theo retention hiện hành; không ghi email vào log/báo cáo; đối soát theo path review | Mapped |
| `totalRating`, `reviewCount`, `averageReviews` | `Products.TotalRating`, `ReviewCount bigint`, `AverageReviews` | Giữ aggregate nguồn; không tự tính lại từ review | Mapped |
| `warranty` | `Products.WarrantyInformation` | Chuỗi/null giữ nguyên; đối soát giá trị và document count | Mapped |
| `description` | `Products.Description` | Chuỗi/null giữ nguyên; đối soát giá trị và document count | Mapped |
| `solution`, `features`, `operatingMethod`, `advantages`, `specifications` | Năm cột tương ứng tại `Products` | Giữ từng field/null riêng, không gộp mô tả | Mapped |

### `users`

| Source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `_id`, `__v` | `Users.UserId`, `PublicId`, `Version`, mapping root | missing `__v=0` | Mapped |
| `name`, `email`, `phone` | `Users.DisplayName`, `Email`, `Phone`; Customer mirror khi role customer | Phone canonicalize theo compatibility, nhưng uniqueness/conflict cần issue không merge | Mapped |
| `password` | `UserPasswords.PasswordHash`, `Algorithm='bcrypt'`, `RehashRequired` | Chỉ copy bcrypt hash; không log/return | Mapped |
| `passwordChangedAt` | `UserPasswords.ChangedAtUtc` | missing: cần separate `MigratedAt`, không dùng now làm business time | Mapped |
| `role` | `Roles` + `UserRoles` | Các code legacy; `superadmin` cần quyết định ownership control-plane, không tự cấp local | Blocked |
| `functions[]`, `permissions[]` | `Permissions`, `UserPermissions`/`RolePermissions` | Chưa có canonical permission catalogue/mapping | Blocked |
| `cart[i]._id`, `productId`, `variantIndex`, `quantity`, `status` | `Carts` (một User), `CartItems.PublicId`, `ProductId`, `ProductVariantId`, `Quantity`, `IsSelected`, `SortOrder=i` | Resolve variant theo `productId` + index tại thời điểm nguồn. Invalid/orphan hoặc quantity không dương → issue và không tạo item, đối soát count mapped/issue | Mapped |
| `orderTemplate[i]._id`, `displayName`, `note` | `OrderTemplates.PublicId`, `DisplayName`, `Note`, `SortOrder=i` | Chuỗi/null giữ nguyên; đối soát count và thứ tự template mỗi User | Mapped |
| `orderTemplate[i].products[j]._id`, `productId`, `quantity` | `OrderTemplateItems.ProductId`, `ProductVariantId`, `Quantity`, `SortOrder=j` | Legacy không có variant nên `ProductVariantId=NULL`; product invalid/orphan hoặc quantity không dương → issue, không tạo item | Mapped |
| `station[i]` | `UserStations` after resolve Station | `SourcePath=station[i]`; unresolved → issue, no Branch inference | Mapped |
| `addresses[i]._id`, `receiverName`, `receiverPhone`, `addressDetail`, `isDefault` | `CustomerAddresses.PublicId`, `ReceiverName`, `ReceiverPhone`, `AddressDetail`, `IsDefault` | User must have Customer; missing required destination value → issue | Mapped |
| `addresses[i].label` | Không có cột target | Do not discard semantic label | Blocked |
| `logInString` | Không copy plaintext; revoke all legacy auto-login at cutover | No target row from value | SecretStore |
| `resetOtp`, `resetOtpExpires` | Không copy OTP; revoke pending reset | Hash cannot be derived safely without plaintext rule | SecretStore |

### `orders` và `counters`

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `orders._id`, `__v` | `SalesOrders.SalesOrderId`, `PublicId`, `Version`, mapping root | count root | Mapped |
| `orders.orderCode` | `SalesOrders.OrderCode` | Trim; rỗng/missing giữ `NULL`; đối soát duplicate nguồn thành issue, không đổi mã | Mapped |
| `orders.userPhone`, `userName` | `SalesOrders.CustomerPhoneSnapshot`, `CustomerNameSnapshot`, rồi resolve `Customers.CustomerId` khi khớp duy nhất | Snapshot luôn giữ giá trị nguồn; không resolve thì `CustomerId=NULL` + issue, không tạo Customer giả | Mapped |
| `orders.total` | `SalesOrders.Total` | Number → decimal(19,4); must not recalc lines lacking price | Mapped |
| `orders.status`, `state`, `completedAt` | `SalesOrders.Status`, `State`, `CompletedAtUtc` | Giá trị enum giữ nguyên sau allowlist; null thời gian giữ null; đối soát count từng trạng thái và số timestamp null | Mapped |
| `orders.createdAt`, `updatedAt` | `SalesOrders` chỉ có `CreatedAtUtc`, `UpdatedAtUtc`, không có cặp timestamp nguồn | Không được trả timestamp vòng đời SQL như timestamp MongoDB; cần cột `SourceCreatedAtUtc`/`SourceUpdatedAtUtc` hoặc quyết định contract | Blocked |
| `orders.payment` | `SalesOrders.PaidState` có kiểu chuỗi | Boolean MongoDB không có bảng mã đích đã phê duyệt; không đổi `true/false` thành chuỗi tự đặt | Blocked |
| `orders.cartItems[i]._id`, `productId`, `variantIndex`, `quantity` | `SalesOrderItems.PublicId`, `ProductId`, `ProductVariantId`, `LegacyProductObjectId`, `LegacyVariantIndex`, `Quantity` | Resolve at source snapshot; missing product uses legacy id. Target item needs PublicId but source child may id. | Mapped |
| `orders.cartItems[i]` snapshot fields absent source (`name`, unit price, VAT) | `SalesOrderItems.ProductName`, `ProductCode`, `UnitPrice`, `LineTotal`, `VatRate` | All remain NULL; issue `ORDER_SNAPSHOT_MISSING`, do not read current Product to fabricate | Empty |
| `orders.images[i]` | `SalesOrderFiles.FileId/ExternalUrl` | Canonicalize URL and file manifest; missing file ⇒ external alias/status | Mapped |
| `counters._id`, `id` | Mapping root dùng `MigrationMappings.SourceKey`/`SourceKeyType`; `id` → `NumberSequences.SequenceCode` | Ghi nguyên `_id` hoặc `id` vào `SourceKey`, lần lượt `ObjectId`/`String`; không ép chuỗi thành ObjectId. Một document chỉ được map khi `id` không rỗng và không trùng `SequenceCode`; còn lại tạo issue | Mapped |
| `counters.seq` | `NumberSequences.NextValue` | Parse `bigint`; `seq >= 0` ghi `NextValue=seq+1` vì đích giữ số kế tiếp cấp phát và có ràng buộc `>0`; parse lỗi/âm tạo issue, không thay bằng `1` | Mapped |

### `iporders`, `eporders`, `storagehistories`

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `iporders._id`, `__v` | `ImportOrders.ImportOrderId`, `PublicId`, `Version`, mapping root | count | Mapped |
| `iporders.orderName`, `note`, `userName` | `ImportOrders.OrderName`, `Note`, `ActorName` | Chuỗi/null giữ nguyên; `orderName` không map sang `OrderCode`; đối soát document count và giá trị null | Mapped |
| `iporders.total` | `ImportOrders.Total`, `LegacyTotalRaw` | Luôn lưu raw; chỉ ghi decimal khi parser tiền versioned xác định, lỗi parse → `Total=NULL` + issue | Mapped |
| `iporders.status`, `completedAt` | `ImportOrders.Status`, `CompletedAtUtc`, `RecordOrigin=N'Legacy'`, `DataStatus` | `true` → `Completed`, `false` → `Pending`; `completedAt` hợp lệ giữ nguyên. `true` thiếu/invalid `completedAt` ghi `DataStatus=Incomplete`, được constraint legacy cho phép; status không phải boolean tạo issue | Mapped |
| `iporders.createdAt`, `updatedAt` | `ImportOrders.SourceCreatedAtUtc`, `SourceUpdatedAtUtc` | Preserve null/missing; không ghi vào timestamp vòng đời SQL | Mapped |
| `iporders.productList[i]._id`, `productId`, `price`, `unit`, `quantity`, `quantityRe`, `stockAppliedQuantity`, `note`, `vat`, `status` | `ImportOrderItems.PublicId`, references/legacy ID, `UnitPrice`/`UnitPriceRaw`, `UnitSnapshot`, `ReceivedQuantity`/`QuantityRaw`, `QuantityRe`, `StockAppliedQuantity`, `Note`, `VatRate`/`VatRateRaw`, `LegacyStatus`, `RecordOrigin=N'Legacy'` | Decimal hợp lệ không âm được map độc lập; raw price/VAT/quantity luôn giữ. `AppliedQuantity` là trạng thái SQL, không suy từ legacy. Product/variant không resolve giữ legacy ID và `DataStatus=Orphan`; thiếu/invalid số cần thiết ghi `DataStatus=Incomplete` và issue, không thay `0` | Mapped |
| `iporders.images[i]` | `ImportOrderFiles.FileId/ExternalUrl` | File manifest / URL canonicalization | Mapped |
| `eporders._id`, `__v` | `ExportOrders.ExportOrderId`, `PublicId`, `Version`, mapping root | count | Mapped |
| `eporders.orderName`, `note`, `userName` | `ExportOrders.OrderName`, `Note`, `ActorName` | Chuỗi/null giữ nguyên; `orderName` không map sang `OrderCode`; đối soát document count và giá trị null | Mapped |
| `eporders.total` | `ExportOrders.Total`, `LegacyTotalRaw` | Luôn lưu raw; chỉ ghi decimal khi parser tiền versioned xác định, lỗi parse → `Total=NULL` + issue | Mapped |
| `eporders.status`, `completedAt` | `ExportOrders.Status`, `CompletedAtUtc`, `RecordOrigin=N'Legacy'`, `DataStatus` | `true` → `Completed`, `false` → `Pending`; `completedAt` hợp lệ giữ nguyên. `true` thiếu/invalid `completedAt` ghi `DataStatus=Incomplete`; status không phải boolean tạo issue | Mapped |
| `eporders.createdAt`, `updatedAt` | `ExportOrders.SourceCreatedAtUtc`, `SourceUpdatedAtUtc` | Preserve null/missing; không dùng thời gian SQL thay thời gian nguồn | Mapped |
| `eporders.productList[i]._id`, `productId`, `price`, `unit`, `quantity`, `quantityEx`, `stockAppliedQuantity`, `stockUpdateSkipped`, `note`, `vat`, `status` | `ExportOrderItems.PublicId`, references/legacy ID, `UnitPrice`/`UnitPriceRaw`, `UnitSnapshot`, `ExportedQuantity`/`QuantityRaw`, `QuantityEx`, `StockAppliedQuantity`, `StockUpdateSkipped`, `Note`, `VatRate`/`VatRateRaw`, `LegacyStatus`, `RecordOrigin=N'Legacy'` | Decimal hợp lệ không âm được map độc lập; raw price/VAT/quantity luôn giữ. `AppliedQuantity` là trạng thái SQL, không suy từ legacy. Orphan/invalid tạo `DataStatus` phù hợp và issue, không dùng default để bịa giá trị | Mapped |
| `eporders.productList[i].importPriceSnapshot` | `ExportOrderItems.ImportPriceSnapshot`, `ImportPriceSnapshotRaw` | Decimal hợp lệ ghi snapshot; raw luôn giữ; lỗi parse ghi `NULL` decimal + issue, không đọc `ProductVariants.ImportPrice` hiện tại | Mapped |
| `eporders.productList[i].profitPercent` | `ExportOrderItems.ProfitPercent` | Decimal 0..100 giữ nguyên, null giữ null; đối soát số dòng có/không có giá trị | Mapped |
| `eporders.images[i]` | `ExportOrderFiles.FileId/ExternalUrl` | File manifest / URL canonicalization | Mapped |
| `storagehistories._id` | `LegacyStockHistories.LegacyStockHistoryId`, `PublicId`, mapping root | count root | Mapped |
| `storagehistories.productId`, `quantity`, `createdAt` | `LegacyStockHistories.LegacyProductObjectId`, `Quantity`, `SourceCreatedAtUtc` | Keep orphan id; signed quantity preserved; not a ledger replay | Mapped |
| `storagehistories.note`, `source` | `LegacyStockHistories.Note`, `SourceType` | Chuỗi/null giữ nguyên; đối soát count source type; không dùng làm opening balance | Mapped |
| `storagehistories.productName`, `userName`, `orderId`, `orderName`, `isAIScan`, `updatedAt`, `__v` | `LegacyStockHistories.ProductName`, `UserName`, `LegacyOrderKey` (và `LegacyOrderObjectId` nếu ObjectId hợp lệ), `OrderName`, `IsAiScan`, `SourceUpdatedAtUtc`, `SourceVersion` | Preserve text, boolean, timestamp và version. `orderId` không phải ObjectId chỉ vào `LegacyOrderKey`; không resolve hoặc bịa Product/User/Order hiện tại | Mapped |

### `stations` và `manages`

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `stations._id`, `__v` | `Stations.StationId`, `PublicId`, `Version`, mapping root | `__v` missing = 0; đối soát count | Mapped |
| `stations.stationCode`, `stationName` | `Stations.StationCode`, `Name` | Code uniqueness collision → issue, no rename silently | Mapped |
| `stations.productId[i]` | `StationProducts.ProductId`, `SortOrder=i` | Resolve source ID; orphan → issue, no fake Product | Mapped |
| `stations.allowPublicSignup` | `Stations.AllowPublicSignup` | Preserve boolean/null theo từng Station; không collapse vào cấu hình storefront singleton | Mapped |
| `stations.imgUrl` | `StationFiles.ExternalUrl`, hoặc `FileId` sau file manifest | `SourcePath=imgUrl`, `Label=N'LegacyStationImage'`, thứ tự 0; URL/path invalid → issue | Mapped |
| `stations.location` | `Stations.Location` | Giá trị null giữ null; đối soát document count | Mapped |
| `stations.createdAt`, `updatedAt` | `Stations.SourceCreatedAtUtc`, `SourceUpdatedAtUtc` | Khi field BSON có mặt, preserve null/missing vào cột `Source*`; nếu profile xác nhận field hoàn toàn vắng mặt thì không tạo giá trị | Mapped |
| virtual `stations.inviteCode` | Không phải field BSON để migrate | Tái tính từ `StationCode` ở lớp contract; không tạo PublicKey tự suy diễn | Empty |
| `manages._id`, `__v` | `StorefrontSettings.StorefrontSettingId`, `Version`, mapping root | Một document singleton → một GUID target; `__v` missing = 0; count phải bằng 1 hoặc issue duplicate | Mapped |
| `manages.displayPartners` | `StorefrontSettings.DisplayPartners` | Singleton; count one expected | Mapped |
| `manages.homeCategoryConfig.showSidebar`, `showQuickCategories` | `StorefrontSettings.ShowSidebar`, `ShowQuickCategories` | Preserve explicit false/missing-default distinction via issue/evidence | Mapped |
| `manages.overViewImg[i]` | `StorefrontImages.ExternalUrl`, `ImageType=N'Overview'`, `SortOrder=i` | URL canonicalization/file manifest; duplicate được giữ theo thứ tự | Mapped |
| `manages.partners[i]` | `StorefrontImages.ExternalUrl`, `ImageType=N'Partner'`, `SortOrder=i` | Mỗi phần tử là URL partner theo model legacy; canonicalize URL/file reference, giữ duplicate/thứ tự; URL/path invalid tạo issue | Mapped |
| `manages.footerContent.logo`, `description`, `address`, `phone`, `email` | `StorefrontSettings.FooterLogoUrl`, `FooterDescription`, `FooterAddress`, `FooterPhone`, `FooterEmail` | Preserve từng field/null riêng, canonicalize URL logo; không nén vào nội dung chung | Mapped |
| `manages.introduction` | `StorefrontSettings.IntroductionContent` | Chuỗi/null giữ nguyên; đối soát hash/độ dài nội dung | Mapped |
| `manages.newProductUrl`, `topPurchaseUrl`, `highestRatingUrl`, `mainPolicy` | `StorefrontSettings.NewProductUrl`, `TopPurchaseUrl`, `HighestRatingUrl`, `MainPolicy` | Preserve URL/nội dung/null; URL invalid tạo issue, không thay URL khác | Mapped |
| `manages.introductionTranslations.vi/zh/en` | `StorefrontLocalizedContents(ContentKey=N'Introduction', LanguageCode, Content)` | Locale allowlist `vi`, `zh`, `en`; null giữ null; đối soát từng locale | Mapped |
| `manages.policies[i]._id` and policy field paths | `Policies.PublicId`, `PolicyKey`, `Title`, `Summary`, `SourceUpdatedAtUtc`, `Version`; child → `PolicySections.Title`, `Content`, `SortOrder`, `SourcePath` | `PolicyKey` phải được lấy từ field key/code có trong source và unique; nếu document chỉ có nội dung không có key ổn định, tạo issue và không tự đặt key. Preserve child order/null | Mapped |
| `manages.homeCategoryConfig.configured`, `sidebarTitle`, translations | `StorefrontSettings.HomeCategoryConfigured`, `HomeCategorySidebarTitle`; translation → `StorefrontLocalizedContents(ContentKey=N'HomeCategorySidebarTitle', LanguageCode, Content)` | Preserve explicit `false`/null; locale là key nguồn, trim/allowlist theo contract; locale/value invalid tạo issue | Mapped |
| `manages.homeCategoryConfig.items[i].id`, `label`, `labelTranslations.*`, `type`, `link`, `icon`, `image`, `showSidebar`, `showQuick` | `HomeCategories.LegacyId`, `Name`, `CategoryType`, `LinkUrl`, `IconKey`, `ImageUrl`, `ShowSidebar`, `ShowQuick`, `SortOrder`, `SourcePath`; translation → `StorefrontLocalizedContents(ContentKey=N'HomeCategory:'+id, LanguageCode, Content)` | `id` không rỗng giữ nguyên `LegacyId` và `MigrationMappings.SourceKey` type `String`; `PublicId` được sinh tương thích khi `id` không phải ObjectId. Preserve URL/null/thứ tự; id trống/trùng hay URL invalid tạo issue | Mapped |
| `manages.section1…section11.name`, `nameTranslations.*`, `display`, `image`, `link` | `StorefrontSections.Name`, `IsDisplayed`, `ImageUrl`, `LinkUrl`, `SortOrder`, `SourcePath`; translation → `StorefrontLocalizedContents(ContentKey=N'Section:'+SourcePath, LanguageCode, Title)` | Tên section và source path xác định section; preserve boolean/URL/null. Locale hoặc URL invalid tạo issue, không bỏ field | Mapped |
| `manages.section1…section11.productId[i]` | `StorefrontSectionProducts.ProductId`, `SortOrder=i` | Map sau khi section được tạo; product không resolve tạo issue và không thêm liên kết giả | Mapped |

### `voicevocabs`, `telegramconfigs`, `zaloconfigs`

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `voicevocabs._id`, `__v`, `updatedAt` | `VoiceSettings.PublicId`, `Version`, `SourceUpdatedAtUtc`, mapping root | Single doc expected but duplicates allowed source | Mapped |
| `voicevocabs.createdAt` | `VoiceSettings.SourceCreatedAtUtc` | Preserve null/missing; không dùng `CreatedAtUtc` SQL thay thời gian nguồn | Mapped |
| `voicevocabs.stopwords[i]`, `brands[i]`, `types[i]` | `VoiceWords(Value, WordType, SourcePath, SortOrder, LegacyDuplicateIndex)` | WordType lần lượt `Stopword`, `Brand`, `Type`; giữ duplicate theo index; đối soát count từng nhóm | Mapped |
| `voicevocabs.brandAliases[i].name`, `aliases[j]` | `VoiceAliases(Value, AliasType=N'Brand', SourcePath, SortOrder)`, `VoiceAliasValues` | `name` là Value của nhóm; aliases giữ SourcePath và thứ tự/duplicate; đối soát count parent/child | Mapped |
| `voicevocabs.typeAliases[i].type`, `keyword`, `aliases[j]` | `VoiceAliases(Value, AliasType=N'Type', Keyword, SourcePath, SortOrder)`, `VoiceAliasValues` | Giữ type/keyword/aliases và thứ tự/duplicate; đối soát count parent/child | Mapped |
| `voicevocabs.intentAliases[i].intent`, `label`, `aliases[j]` | `VoiceAliases(Value, AliasType=N'Intent', Label, SourcePath, SortOrder)`, `VoiceAliasValues` | Giữ intent/label/aliases và thứ tự/duplicate; đối soát count parent/child | Mapped |
| `voicevocabs.codeMap[i].code`, `keyword`, `brand`, `type`, `patterns[j]`, `compact` | `VoiceCodeMaps.Code`, `Keyword`, `Brand`, `ProductType`, `Compact`, `SourcePath`, `SortOrder`; `patterns[j]` → `VoiceCodeMapPatterns.Pattern`, `SourcePath`, `SortOrder` | Preserve mọi field/null, thứ tự và duplicate index; code rỗng hoặc pattern rỗng tạo issue, không flatten vào bảng alias/word | Mapped |
| `telegramconfigs._id`, `enabled`, `__v` | `Integrations(ProviderCode='Telegram', IsEnabled, Version)` | One provider row; root mapping | Mapped |
| `telegramconfigs.recipients[i]._id`, `label`, `chatId`, `type`, `enabled` | `NotificationRecipients` | `chatId` is sensitive recipient reference; transfer only after protected reference/retention approval. Target lacks label/type | Blocked |
| `telegramconfigs.recipients[i].notifyTypes[j]` | `NotificationSubscriptions.EventCode` | Requires recipient mapping; preserve values exactly | Blocked |
| `telegramconfigs.createdAt`, `updatedAt` | No target columns | No fabrication | Blocked |
| `zaloconfigs._id`, `createdAt`, `updatedAt`, `__v` | Metadata only `Integrations(ProviderCode='Zalo', Version)` | Do not activate until secret reference exists | Blocked |
| `zaloconfigs.appId`, `secretKey`, `accessToken`, `refreshToken` | Secret manager record; SQL only reference code | Never copy value to SQL/log/doc; rotate/cutover plan required | SecretStore |
| `zaloconfigs.oaId`, `recipientUserId`, `expiresAt` | No safe metadata crosswalk approved | May be sensitive provider identity and expiry; needs provider contract | SecretStore |

### Collections đồ uống và collection chỉ có trong snapshot

| Collection / source path | Đích / chuyển đổi | Null, orphan, đối soát | Trạng thái |
|---|---|---|---|
| `drinks._id`, `drinkName`, `drinkPrice`, `drinkImg`, `toppings[]` | No operational table | Router legacy chưa mount, snapshot profile không có collection; archive/quarantine only if retention approved | Archived |
| `drinktoppings._id`, `toppingNames`, `toppingPrice` | No operational table | Same scope decision | Archived |
| `drinkbills._id`, `detail[i]._id`, `staff`, `drinkImg`, `drink`, `toppings`, `drinkPrice`, `status`, `billTotal`, `billStatus`, `createdAt`, `updatedAt` | No operational table | Same scope decision; do not reinterpret as sales orders | Archived |
| `drinkowelists._id`, `staffID`, `bank` | No operational table | Semantics/units not established | Archived |
| `autologintokens.*` | No plaintext token target | Snapshot observed empty; revoke legacy capability rather than migrate values | Empty |
| `chatmessages.*` | `ArchivedChatMessages` is not a sufficient field-level crosswalk | Source model/retention/PII policy absent; no default migration | Blocked |

## Metadata migration, timestamp và manifest đối soát

| Thành phần nguồn / yêu cầu | Đích baseline v1 | Quy tắc và tiêu chí đối soát | Trạng thái |
|---|---|---|---|
| MongoDB ObjectId 24 ký tự ở root/subdocument có `_id` | `MigrationMappings.SourceKey`, `SourceKeyType=N'ObjectId'`, `SourcePath`, `TargetTable`, `TargetId` | ObjectId lowercase-hex; root `SourcePath=''`, child dùng path zero-based; kiểm tra unique source-target và count child | Mapped |
| Subdocument không có `_id` | `MigrationMappings.SourcePath` | Dùng path đầy đủ như `voicevocabs.codeMap[3].patterns[1]`; giữ index và duplicate index ở các bảng Voice | Mapped |
| Khóa nguồn chuỗi, ví dụ `counters.id` | `MigrationMappings.SourceKey` và `SourceKeyType` | Dùng `String`; ObjectId dùng `ObjectId`, subdocument dùng `SourcePath`; không ép chuỗi thành ObjectId | Mapped |
| Một document tạo nhiều hàng/bảng đích | `MigrationMappings` có `TargetTable`, `TargetId` | Có thể ghi nhiều mapping theo `TargetTable`/`SourcePath`; đối soát fan-out theo manifest | Mapped |
| Nhiều source hợp lệ cùng map một target | Unique hiện tại không cấm cùng `TargetTable`/`TargetId` | Phải ghi mọi source mapping và đối soát số liên kết; test v1 đã minh hoạ trường hợp này | Mapped |
| Manifest theo source database/collection: document count, child count, tổng quantity/tổng tiền, disposition, file count, checksum/version công cụ, thời gian | `MigrationManifests` | Ghi count/tổng/checksum/version tool, không lưu raw PII/secret; số liệu chỉ được điền khi rule đối soát được phê duyệt | Mapped |
| File manifest/checksum cho URL/file reference | `Files`/`FileLocations`/`FileAliases` có metadata file nhưng không có manifest migration per collection | Cần manifest nguồn và đối soát file count/checksum, canonicalization rồi kiểm tra final path dưới root cấu hình | Blocked |
| `SourceCreatedAtUtc`, `SourceUpdatedAtUtc`, `MigratedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc` | Có đầy đủ trên `Users`, `Products`; chỉ một phần ở các entity khác | Timestamp MongoDB luôn vào `Source*`, thời điểm worker vào `MigratedAtUtc`, vòng đời SQL vào `Created/Updated`. Entity thiếu cột source không được mượn timestamp SQL | Blocked |

## Quy tắc chạy migration và đối soát

1. Tạo `MigrationRuns`, dùng immutable source manifest/hash, ghi `MigrationMappings` cho root và từng child target. `SourcePath` phải chính xác (ví dụ `variant[0]`, `details[3]`), một source có thể tạo nhiều target table.
2. Ghi `MigrationIssues` cho BSON type sai, ObjectId/string mixed không resolve, missing required, parse money/VAT lỗi, duplicate business key, URL/path invalid, orphan và field `Blocked`; không in raw PII/secret vào `SafeDetail`.
3. Đối soát tối thiểu theo collection: document/subdocument count, `Mapped + Archived + Empty + Blocked` disposition, ObjectId mapping uniqueness, tổng quantity/stock, parsed money (chỉ khi rule được phê duyệt), number of orphans, file manifest/checksum. `storagehistories` chỉ là evidence, không được dùng để suy opening stock.
4. Không chuyển field `Blocked` bằng cách dùng default SQL, dữ liệu Product hiện tại hay thời điểm migration. Các cột thiếu cần migration schema version mới trước khi data migration.

## Ghi chú lịch sử runner và verification v1

Các finding về re-check sau application lock, test placeholder và độ bao phủ fingerprint ở prototype trước v1 là hồ sơ lịch sử; không mô tả trạng thái runner baseline v1 hiện tại. Bằng chứng DDL/test schema thuộc `SQLSERVER_V1_BASELINE_IMPLEMENTATION.md`. Dù schema test đã được kiểm tra, các dòng `Blocked` ở ma trận này vẫn là blocker của migration dữ liệu vì thiếu cột, rule hoặc manifest field-level; chúng không được hạ thành `Mapped` nhờ kết quả DDL.

## Điều kiện trước migration dữ liệu

- Chốt toàn bộ item `Blocked`, retention archive/chat/drinks, identity role mapping, secret manager và policy file/media.
- Sửa runner/verification gaps ở trên, chạy checksum mismatch, rerun, concurrent runner, constraint trusted/enabled, fingerprint và test synthetic thực sự.
- Chỉ profile/dry-run trên bản sao được phê duyệt bằng principal read-only; không xuất PII/secret. Không chuyển runtime hoặc cutover chỉ vì baseline schema đã materialize.
