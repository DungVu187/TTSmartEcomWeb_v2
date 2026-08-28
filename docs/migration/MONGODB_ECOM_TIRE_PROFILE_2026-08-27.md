# Hồ sơ MongoDB `Ecom` cho chức năng lốp ngày 2026-08-27

## Phạm vi và ranh giới an toàn

- Nguồn: MongoDB 8.0.26 local tại `127.0.0.1:27017`, database `Ecom`.
- Collection chính: `vehicles` và `tireorders`; chỉ đọc projection tổng hợp từ `products`, `users`, `storagehistories` và `activitylogs` để kiểm tra reference/evidence liên quan.
- Chỉ chạy `buildInfo`, `listCollections`, `countDocuments`, `find`, đọc index và tính thống kê trong bộ nhớ với application name `TTSmartEcomReadOnlyProfiler`, `ReadPreference=SecondaryPreferred`.
- Không đọc `.env`, không kết nối production, không chạy insert/update/delete, seed, migration hoặc tạo index. Báo cáo không chứa document, ObjectId, biển số, tên người, tên đơn hoặc payload nghiệp vụ cụ thể.
- Đây là snapshot local tại thời điểm khảo sát, không tự động đại diện production và không phải bằng chứng sẵn sàng migration/cutover.

## Snapshot vật lý

- Database có 21 collection và 1.670 document tại đầu lượt đọc.
- `vehicles`: 7 document.
- `tireorders`: 6 document.
- Hai collection đều tồn tại ngoài snapshot 19 collection/1.503 document ngày 2026-08-14; source hiện suy ra 23 collection sau khi cộng hai model mới.

### Index `vehicles`

| Index | Key | Thuộc tính |
|---|---|---|
| `_id_` | `_id: 1` | mặc định |
| `licensePlateKey_1` | `licensePlateKey: 1` | unique, không sparse |

### Index `tireorders`

| Index | Key |
|---|---|
| `_id_` | `_id: 1` |
| `status_1` | `status: 1` |
| `createdAt_-1` | `createdAt: -1` |
| `status_1_createdAt_-1` | `status: 1, createdAt: -1` |
| `vehicles.assignments.productId_1` | `vehicles.assignments.productId: 1` |
| `isDeleted_1` | `isDeleted: 1` |
| `isDeleted_1_createdAt_-1` | `isDeleted: 1, createdAt: -1` |

## `vehicles`

### Hình dạng và kiểu BSON

- 7/7 `_id` là ObjectId; `licensePlate`, `licensePlateKey`, `name`, `note` đều là String; `isActive` là Boolean; `createdAt`/`updatedAt` là Date; `__v` là Number.
- Không có extra root field ngoài schema quan sát từ source.
- 5 document có `wheelCount` Number: ba giá trị 10 và hai giá trị 12. Hai document active thiếu `wheelCount`; behavior source mặc định chúng thành xe 10 bánh.
- 5 xe active, 2 xe inactive. `__v`: năm document bằng 0, hai document bằng 1.
- Không có biển số/key rỗng, không thiếu timestamp. Không có duplicate `licensePlateKey` vật lý.
- Có một nhóm trùng biển số sau chuẩn hóa nếu tính cả active và inactive, nhưng không có nhóm trùng trong tập active. Đây phù hợp behavior xóa mềm rồi cho phép tạo xe mới dùng lại biển số; migration không được đặt unique trên toàn bộ lịch sử nếu vẫn giữ cả bản ghi inactive.

### Vấn đề migration

- Hai `wheelCount` missing phải giữ trạng thái source missing trong evidence và áp compatibility default 10 bằng rule có version; không được sửa MongoDB nguồn.
- Unique SQL cần theo active normalized plate hoặc dùng key lịch sử tương đương behavior source. Không merge hai xe chỉ vì biển số chuẩn hóa trùng khi một bản ghi đã inactive.

## `tireorders`

### Header và trạng thái

- 6/6 `_id`, `createdBy` là ObjectId; `orderName`, `createdByName`, `note`, `status` là String; `transactionDate`, `createdAt`, `updatedAt` là Date; các tổng và `__v` là Number.
- 3 đơn `completed`, 3 đơn `processing`; inventory tương ứng đúng ba `applied` và ba `idle`.
- Một đơn completed và hai đơn processing có `isDeleted=true`; một đơn processing có `isDeleted=false`; hai đơn completed thiếu field `isDeleted` và phải được hiểu là false theo compatibility behavior.
- Hai đơn thiếu cả bộ deletion metadata do được tạo trước khi field mới được materialize; missing khác với null và phải được bảo toàn trong staging/evidence.
- `__v` quan sát từ 2 đến 5. `Version=0` vẫn phải hợp lệ về thiết kế dù snapshot hai collection hiện không có order version 0.

### Cấu trúc nhúng

- 7 vehicle entry: năm xe 10 bánh, hai xe 12 bánh. Tất cả có `_id`, `vehicleId`, snapshot biển số/tên, `wheelCount`, `note` và mảng `assignments` đúng kiểu BSON; `vehicleNameSnapshot` và `note` hiện đều rỗng nhưng field có tồn tại.
- 4 assignment, tất cả ở `front_left`. Mọi assignment có ObjectId riêng, `productId`/`variantId` ObjectId, `variantIndex` Number, đầy đủ snapshot string, `variantSnapshot`, `performedAt`, `previousTireStoppedAt`, `note` và `stockAppliedQuantity`.
- `previousTireStoppedAt`: hai Date, hai null. `productSpecificationsSnapshot` và `note` của bốn assignment là chuỗi rỗng; các snapshot mã/tên/brand/value/giá đều hiện diện.
- 3 inventory adjustment thuộc ba đơn completed; mọi field reference là ObjectId, `quantity`/`variantIndex` là Number và snapshot biển số là String.
- Hình dạng theo đơn: một đơn có một xe/không lốp; bốn đơn có một xe/một lốp; một đơn có hai xe/không lốp.

### Đối soát invariant và reference

Không phát hiện chênh lệch trong snapshot hiện tại đối với:

- trùng Vehicle trong cùng đơn, trùng slot, slot ngoài layout hoặc vượt 10/12 vị trí;
- `totalVehicles`, `totalTires`, `totalExportPrice`, `stockAppliedTireCount` so với dữ liệu nhúng;
- trạng thái completed/processing so với `inventory.phase` và `stockAppliedQuantity`;
- tổng `inventory.adjustments.quantity` của đơn completed so với assignment;
- ObjectId Vehicle/Product/Variant/User mồ côi hoặc Vehicle inactive đang được assignment hiện tại tham chiếu;
- Product sai type: bốn assignment cùng tham chiếu một Product hiện có type chính xác `Lốp xe` và Variant vẫn resolve được;
- snapshot mã/giá rỗng hoặc giá snapshot không parse được theo parser legacy;
- ngày lốp cũ ngưng hoạt động sau ngày thay, trước ngày lắp lốp cũ hoặc thiếu ngày bắt đầu vòng đời.

### Vòng đời suy ra

- Ba assignment thuộc đơn completed tạo một chuỗi `vehicleId + slotId`.
- Kết quả suy ra theo behavior hiện tại: hai record `ended`, một record `active`; không có mốc thời gian nghịch.
- Assignment thứ tư thuộc đơn processing nên chưa tham gia vòng đời.
- Không có collection vòng đời riêng; mọi record được suy từ đơn completed. Khi sang SQL, không được coi số liệu read model là source document độc lập nếu chưa thiết kế provenance/rebuild.

## Evidence liên quan

### `storagehistories`

Có 21 document nguồn lốp, tất cả có `orderType=tire_order`, `orderId` String, reference Vehicle/Product/Variant ObjectId, `inventoryOperationId` String và ngày giao dịch/tạo kiểu Date:

| Source | Document | Tổng signed quantity | Reference tới đơn không còn trong `tireorders` |
|---|---:|---:|---:|
| `tire_order_complete` | 12 | -18 | 9 |
| `tire_order_revert` | 5 | 5 | 5 |
| `tire_order_delete_revert` | 4 | 10 | 4 |
| Tổng | 21 | -3 | 18 |

- Không có orphan Vehicle/Product và không thiếu `inventoryOperationId` trong 21 dòng.
- 18/21 history tham chiếu đơn không còn tồn tại, kể cả toàn bộ revert/delete-revert. Đây là evidence của behavior trước khi xóa mềm được chốt; migration phải giữ `LegacyOrderKey`, source và operation ID, không drop hoặc tự tạo lại đơn nghiệp vụ.
- `tire_order_delete_revert` có dữ liệu thật dù runtime hiện tại không còn phát event này. Source enum phải tiếp tục được đọc/migrate như legacy evidence.

### `activitylogs`

- Quan sát 106 activity thuộc `entityType=tire_order` và một activity `vehicle:create_vehicle`.
- 14 activity tire-order còn resolve tới sáu đơn hiện tại; 92 activity trỏ tới entity không còn trong `tireorders`.
- Action quan sát gồm create/update order, add/remove vehicle, add/update/delete/move assignment, complete và revert. Không quan sát action xóa đơn.
- Tất cả 107 activity có `createdAt` Date; 72 có `entitySubId` ObjectId, 35 thiếu field này.
- Source hiện tại có helper/catalog action nhưng không còn lời gọi ghi activity cho mutation đơn lốp. Dữ liệu quan sát chứng minh activity lịch sử tồn tại, không chứng minh runtime hiện tại tiếp tục ghi. Mapping phải bảo toàn orphan `entityId` như legacy reference và không tuyên bố audit hiện hành đầy đủ.

## Kết luận cho kiến trúc ba tầng

- `vehicles`, `tireorders`, vehicle entry, assignment, inventory adjustment, stock history và activity chi tiết là dữ liệu Branch Operational.
- Product/Variant là Product Master cấp Company. Assignment Branch phải giữ logical `ProductId`/`ProductVariantId` đã được application resolve cùng toàn bộ snapshot quan sát; không có FK hoặc transaction xuyên Company DB–Branch DB.
- Các tổng persisted cần được migrate và đối soát với tổng tính lại; không bỏ persisted value chỉ vì snapshot hiện khớp.
- Hai collection và toàn bộ field hiện có trạng thái migration `Blocked`: V2 chưa có Branch schema, mapper, dry-run, reconcile hoặc test tương ứng. Không chuyển chúng vào baseline Operational hai tầng hiện hữu chỉ vì bảng gần giống.

## Trạng thái xác minh

- Read-only profile/count/type/index/reference/invariant: đã chạy trên snapshot local được phê duyệt.
- Build/test V1, migration SQL, DDL, dry-run mapper và reconcile: **Not yet verified** trong lượt này.
- MongoDB đầu/cuối lượt đọc đều có 21 collection/1.670 document; `vehicles=7`, `tireorders=6`, không đổi.
