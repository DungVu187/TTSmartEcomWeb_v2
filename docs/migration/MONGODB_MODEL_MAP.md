# Bản đồ model MongoDB

## Trạng thái

21 collections dưới đây được suy ra từ các khai báo và cách sử dụng model Mongoose trong legacy. V2 hiện có class document/scaffold class-map tương ứng tại `backend/src/TTSmartEcom.Infrastructure.MongoDb/Persistence/Documents`. Fixture BSON tổng hợp và integration MongoDB biệt lập đã bao phủ một số lát cắt được ghi rõ trong bảng; chúng chưa bao phủ đủ 21 collection, mọi field hoặc mọi đường ghi. Không kết nối database production.

Profile read-only được chủ dự án cho phép trên MongoDB local `Ecom` ngày 2026-08-14 quan sát 19 collection: bốn collection drinks không tồn tại, trong khi `autologintokens` và `chatmessages` tồn tại ngoài inventory source hiện tại. Thống kê, index vật lý và quyết định migration nằm tại `MONGODB_ECOM_DATA_PROFILE_AND_SQLSERVER_DECISIONS.md`; chênh lệch này không thay đổi yêu cầu tương thích source Đợt 1.

| Collection chỉ phát hiện trong snapshot `Ecom` | Quan sát | Quyết định |
|---|---|---|
| `autologintokens` | 0 document; unique index `token`; không tìm thấy model/consumer source hiện tại | không migrate sang SQL; xác minh retention trước khi loại collection cũ |
| `chatmessages` | 3 document; có PII/nội dung; index `sessionId`; không tìm thấy model/consumer source hiện tại | không đưa Core; archive/quarantine hoặc xóa theo quyết định retention |

| Collection | Model/source legacy | Consumer chính | Trạng thái mapping V2 |
|---|---|---|---|
| `activitylogs` | `models/activitylog.js` | route activity, audit log mutation | Ánh xạ document + repository đọc/ghi; fixture BSON ghi và integration query Mongo chọn lọc đã đạt |
| `brands` | `models/chip.js` | route chip/product/type | Ánh xạ document + repository; chưa có fixture riêng cho collection |
| `chips` | `models/chip.js` | route thuộc tính chip | Ánh xạ document + repository; chưa có fixture riêng cho collection |
| `sections` | `models/chip.js` | route section/value/image | Ánh xạ document + repository; fixture round-trip giữ `Section`, `_id` nhúng và `imgUrl` đã đạt |
| `drinks` | `models/drink.js` | drink router chưa mount | Document scaffold; runtime route chưa mount |
| `drinktoppings` | `models/drink.js` | drink router chưa mount | Document scaffold; runtime route chưa mount |
| `drinkbills` | `models/drink.js` | drink router chưa mount | Document scaffold; runtime route chưa mount |
| `drinkowelists` | `models/drink.js` | bề mặt legacy chỉ có model | Document scaffold; repository/fixture chưa có |
| `eporders` | `models/eporder.js` | module order export | Ánh xạ document + repository; fixture đọc ObjectId/string hỗn hợp và ghi `productId` dạng string đã đạt |
| `iporders` | `models/iporder.js` | module order import | Ánh xạ document + repository; fixture ObjectId/string hỗn hợp và integration aggregation Mongo chọn lọc đã đạt |
| `manages` | `models/manage.js` | module quản lý storefront | Ánh xạ document + repository; fixture policy giữ `translations.vi/zh/en` và quy tắc timestamp đã đạt |
| `counters` | `models/order.js` | cấp mã order và mutex mutation Super Admin | Ánh xạ document + repository order/guard; integration tranh chấp guard Mongo chọn lọc đã đạt, fixture cấp mã order còn thiếu |
| `orders` | `models/order.js` | module sales order | Ánh xạ document + repository; fixture giữ `images`, `__v` và extra element đã đạt; đường ghi Mongo rộng còn thiếu |
| `products` | `models/product.js` | product/cart/order/inventory | Ánh xạ document + repository product/order/cart; fixture round-trip variant/extra element/null-missing và lookup aggregation chọn lọc đã đạt |
| `types` | `models/producttype.js` | product/chip type | Ánh xạ document + repository; chưa có fixture riêng cho collection |
| `stations` | `models/station.js` | station/user/order/product | Ánh xạ document + repository; integration ActivityLog resolve reference station chọn lọc đã đạt, fixture document/đường ghi còn thiếu |
| `storagehistories` | `models/storagehistory.js` | module stock/order/history | Ánh xạ document + repository history; chưa có fixture/đường ghi integration riêng |
| `telegramconfigs` | `models/telegram.js` | service/settings Telegram | Ánh xạ document + repository settings; chưa có fixture persistence/provider thật |
| `users` | `models/user.js` | auth/profile/cart/order/admin | Ánh xạ document + repository identity/profile/cart; fixture giữ `_id` cart nhúng đã đạt, đường ghi user Mongo rộng còn thiếu |
| `voicevocabs` | `models/voicevocab.js` | route voice/runtime startup | Ánh xạ document + repository vocabulary; chưa có fixture/đường ghi integration riêng |
| `zaloconfigs` | `models/zalo.js` | service/settings Zalo | Ánh xạ document + repository settings/token; chưa có fixture persistence hoặc OAuth provider thật |

## Bằng chứng mapping bắt buộc

Với mỗi collection, ghi lại chính xác tên field BSON, kiểu dữ liệu, giá trị bắt buộc/mặc định, document nhúng, tham chiếu ObjectId, cách xử lý ngày, ràng buộc unique/index, hook pre/post, cập nhật atomic, pipeline aggregation, hành vi regex/tìm kiếm và ngữ nghĩa null so với missing. Chỉ dùng fixture tổng hợp. Không suy ra SQL schema trong Đợt 1.

## Khoảng trống tương thích đã biết

- `product.documents[]` hiện nhận `_id` từ API, giữ ObjectId hợp lệ khi cập nhật và chỉ sinh ObjectId cho phần tử mới không có `_id`; `_id` sai định dạng bị từ chối. Regression contract/BSON builder đã đạt.
- Mutation address/order-template dùng compare-and-exchange theo snapshot mảng và `__v` legacy khi có, thử lại khi xung đột; order-template bám `_id` ổn định qua các lần thử để tránh cập nhật nhầm phần tử khi index đổi. Unit test bao phủ xung đột/thử lại và việc không làm mất mutation đồng thời; endpoint Mongo dương tính rộng vẫn còn thiếu.
- Bản ghi mutex Super Admin có `_id = "__ttsmart_v2_superadmin_mutation_guard"` trong `counters`. Chỉ xóa thủ công sau khi đã chắc chắn không còn owner hoạt động; chưa có runbook/diễn tập xử lý guard mồ côi.
- Integration hiện chỉ dùng database MongoDB biệt lập và dữ liệu tổng hợp cho các lát cắt đã nêu. Không có bằng chứng kết nối production, coverage đủ 21 collection hoặc mọi đường ghi.

## Ghi chú baseline SQL Server v1 ngày 2026-08-15

Baseline SQL Server v1 không thay thế mapping BSON/runtimes MongoDB hiện tại. Mapping thiết kế cho Đợt 2 được ghi riêng tại `MONGODB_TO_SQLSERVER_MAPPING_V1.md`; nó dùng `PublicId` 24-hex và `MigrationMappings.SourcePath` để giữ ObjectId/subdocument, orphan và một source sinh nhiều target. Chưa có document MongoDB nào được chuyển.
