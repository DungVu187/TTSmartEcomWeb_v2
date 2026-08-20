# Khảo sát code và dữ liệu cho database `[TTSmart]`

## 1. Kết luận chính

`[TTSmart]` là database bán hàng đầy đủ của công ty TTSmart, không phải database chỉ chứa Station, storefront và cấu hình. Kết luận ownership cũ đặt Product/Customer vào `[ttsmart.com.vn]` và chứng từ vào database chi nhánh không còn áp dụng cho dữ liệu hiện tại của TTSmart.

Database tổng `[ttsmart.com.vn]` vẫn là control plane: Company, Branch, tài khoản/quyền cấp nền tảng, feature, database registry, provisioning và audit hệ thống. Database `[TTSmart]` phải sở hữu catalog bán hàng, khách hàng, giỏ hàng, đơn bán, nhập/xuất, tồn kho, Station, storefront và cấu hình nghiệp vụ riêng của TTSmart.

Mỗi chi nhánh được coi là một đơn vị vận hành độc lập: có tồn kho và chứng từ riêng, có thể có catalog riêng, không yêu cầu thay đổi giữa các chi nhánh xuất hiện tức thời, đồng thời ưu tiên khả năng cài local và cô lập dữ liệu. Vì vậy Product và dữ liệu bán hàng authoritative phải nằm trong database vận hành, không nằm ở database tổng.

Khảo sát này chưa tạo `[TTSmart]`, chưa chạy DDL, chưa copy dữ liệu và chưa thay đổi runtime ASP.NET Core/MongoDB của Đợt 1.

## 2. Phạm vi và an toàn

- Source code legacy: `D:\TTSmartEcomWeb`, chỉ đọc.
- MongoDB được phê duyệt: database local `Ecom` tại `127.0.0.1:27017`, chỉ đọc.
- Không đọc `.env`, không in connection string, password, token, OTP, payload provider hoặc dữ liệu cá nhân cụ thể.
- Profile chỉ xuất count, kiểu BSON, tỷ lệ missing/null, tổng số dòng và thống kê quan hệ.
- Không insert/update/delete, không tạo index, không chạy seed/migration và không đọc hàng loạt thư mục upload.

Legacy được khảo sát trên branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`. Worktree legacy có sẵn 58 status entry; khảo sát không làm thay đổi branch, commit hoặc status fingerprint.

## 3. Bằng chứng từ code

Legacy là hệ thống bán hàng và quản lý kho gồm storefront, admin và API. Các route đang được mount trực tiếp tại `/products`, `/orders`, `/iporders`, `/eporders`, `/histories`, `/users`, `/carts`, `/stations`, `/manages`, `/chips`, `/activity-logs`, `/voice-vocabs`, `/telegram` và `/zalo`.

| Nghiệp vụ | Model/collection | Hành vi được code chứng minh | Ownership mới |
|---|---|---|---|
| Catalog | `Product`/`products`, `Brand`/`brands`, `Type`/`types`, `Section`/`sections`, `Chip`/`chips` | Tạo/sửa/xóa/tìm kiếm sản phẩm, variant, mã, giá, VAT, nội dung kỹ thuật và thuộc tính | `[TTSmart]` |
| Tồn hiện tại | `products.variant` | Mỗi variant giữ `quantityForSale` và `quantityInStorage`; mutation chống âm và có optimistic conflict handling | `[TTSmart]` |
| Đơn bán | `Order`/`orders`, `Counter`/`counters` | Đặt hàng khách/admin, giữ chỗ tồn bán, hoàn thành trừ tồn vật lý, hủy/hoàn tác, thanh toán và trạng thái | `[TTSmart]` |
| Nhập kho | `IpOrder`/`iporders` | Header/line, tiến độ nhập, áp tồn từng dòng/toàn phiếu, ảnh chứng từ và tổng tiền | `[TTSmart]` |
| Xuất kho | `EpOrder`/`eporders` | Header/line, tiến độ xuất, snapshot giá nhập/lợi nhuận mới, áp tồn và trường hợp bỏ qua cập nhật tồn | `[TTSmart]` |
| Lịch sử kho | `StorageHistory`/`storagehistories` | Tra cứu theo ngày/người/chứng từ/chiều nhập xuất; dữ liệu cũ thiếu `source` vẫn được frontend dùng | `[TTSmart]` |
| Người dùng bán hàng | `User`/`users` | Đăng nhập, role/permission, hồ sơ, địa chỉ, giỏ hàng, mẫu đơn và phân Station | Identity nền tảng ở `[ttsmart.com.vn]`; dữ liệu vận hành/projection ở `[TTSmart]` |
| Station | `Station`/`stations` | Mã mời, đăng ký public, danh sách Product được xem và phân Station cho khách | `[TTSmart]` |
| Storefront | `Manage`/`manages` | Footer, giới thiệu, chính sách ba ngôn ngữ, nhóm trang chủ, ảnh đối tác và danh sách Product | `[TTSmart]` |
| Audit | `ActivityLog`/`activitylogs` | Log thay đổi Product/User/Station/config; TTL MongoDB 90 ngày | `[TTSmart]` cho audit nghiệp vụ |
| Voice | `VoiceVocab`/`voicevocabs` | Stopword, alias brand/type/intent và code pattern phục vụ tìm Product | `[TTSmart]` |
| Tích hợp | `TelegramConfig`, `ZaloConfig` | Cấu hình thông báo đơn; Zalo model có field secret/token | Metadata ở `[TTSmart]`, secret ở secret store |

### 3.1. Invariant cần giữ

- `Product.code` duy nhất khi có giá trị; Product chưa có code vẫn hợp lệ.
- Product phải còn ít nhất một variant. Code hiện chỉ cho xóa variant cuối cùng, tồn của variant phải bằng 0.
- `quantityForSale`, `quantityInStorage` và `purchaseCount` không được âm.
- Tạo đơn bán trừ `quantityForSale`; hoàn thành đơn trừ `quantityInStorage` và tăng `purchaseCount`; hủy/đổi trạng thái có hoàn tác tương ứng.
- Nhập/xuất có tiến độ từng dòng và `stockAppliedQuantity`; không được áp tồn hai lần.
- Đơn hoàn thành hoặc đã hủy bị khóa mutation nội dung.
- Sales Order tra cứu Product/Variant hiện tại khi trình bày; phần lớn line không có snapshot tên hoặc giá.
- Station giới hạn Product mà customer được xem; Station không phải Branch.
- Storefront có đúng ba locale đang được code hỗ trợ: `vi`, `zh`, `en`.
- API cũ phụ thuộc cả field bị thiếu, null, string-number và extra legacy field; migration không được chuẩn hóa bằng cách làm mất giá trị gốc.

## 4. Snapshot MongoDB đã xác minh lại

Profile read-only hiện tại vẫn có 19 collection và 1.503 document.

| Collection | Document | Vai trò đích |
|---|---:|---|
| `activitylogs` | 383 | `ActivityLogs`, `ActivityLogDetails` |
| `autologintokens` | 0 | Không có row cần chuyển; không tạo token mới |
| `brands` | 29 | `Brands` |
| `chatmessages` | 3 | `ArchivedChatMessages`, hạn chế quyền truy cập |
| `chips` | 1 | `ProductOptions` |
| `counters` | 1 | `NumberSequences` |
| `eporders` | 24 | `ExportOrders`, `ExportOrderItems`, `ExportOrderFiles` |
| `iporders` | 124 | `ImportOrders`, `ImportOrderItems`, `ImportOrderFiles` |
| `manages` | 1 | Nhóm bảng storefront/policy/home category |
| `orders` | 37 | `SalesOrders`, `SalesOrderItems`, `SalesOrderFiles` |
| `products` | 316 | Nhóm catalog, variant, file, review và tồn |
| `sections` | 1 | `Categories`, `CategoryValues` |
| `stations` | 5 | `Stations`, `StationProducts` |
| `storagehistories` | 528 | `LegacyStockHistories`; không dùng để dựng lại tồn mở sổ |
| `telegramconfigs` | 1 | `Integrations`, `NotificationRecipients` |
| `types` | 31 | `ProductTypes` |
| `users` | 16 | Identity trung tâm và dữ liệu vận hành TTSmart |
| `voicevocabs` | 1 | Nhóm bảng voice vocabulary |
| `zaloconfigs` | 1 | `Integrations` và secret reference |

Bốn model đồ uống suy ra từ source (`drinks`, `drinktoppings`, `drinkbills`, `drinkowelists`) không có collection trong snapshot và router không được mount. Không tạo bảng core chỉ để phản ánh code dormant này.

## 5. Thống kê ảnh hưởng trực tiếp tới schema

### 5.1. Product và tồn

- 316 Product và 316 Variant; mọi Product hiện có đúng một Variant.
- 301 Product có code, 15 Product thiếu code; không có code trùng sau trim/lowercase.
- Có 6 nhóm tên chuẩn hóa trùng, gồm 13 Product; không được unique theo tên hoặc tự merge.
- 153 giá bán và 153 giá nhập là chuỗi số hợp lệ; 163 giá bán và 163 giá nhập là chuỗi rỗng. Chuỗi rỗng phải thành `NULL`, không thành 0.
- 250 Product thiếu/rỗng VAT; 66 giá trị VAT còn lại parse được theo format hiện có. Phải giữ raw VAT để đối chiếu migration.
- 28 Product dùng brand chưa có trong lookup, 51 dùng type chưa có trong lookup và 46 dùng section/value chưa có trong lookup. Lookup SQL phải lấy hợp của bảng lookup và giá trị thật trên Product; không loại Product.
- Tổng `quantityForSale` là 9.765; tổng `quantityInStorage` là 9.826; 7 Product có hai số tồn khác nhau.
- Có 215 tham chiếu ảnh variant, 2 document item, 7 ảnh category, 4 ảnh Station, 13 ảnh storefront và 17 ảnh phiếu nhập; tổng cộng 258 tham chiếu file trong database. Chưa xác minh file vật lý.

### 5.2. Đơn bán

- 37 header, 52 line và 2 draft không có line.
- 52/52 line resolve được Product/Variant hiện tại.
- Chỉ 1/52 line có field `price`; không line nào lưu `productName`.
- Header giữ `total`, nhưng không thể phục hồi chính xác giá lịch sử của từng line từ dữ liệu nguồn.
- 5 đơn ở trạng thái `Completed`; 1 đơn Completed thiếu `completedAt`.
- 28 đơn có `state = Cancelled`, 9 đơn còn `Processing`; chỉ 1 đơn có `payment = true`.
- 33/37 đơn khớp một User hiện tại theo phone, nhưng chỉ 6/37 khớp User role customer; 4/37 không còn User tương ứng.

### 5.3. Nhập và xuất

| Chỉ tiêu | Nhập | Xuất |
|---|---:|---:|
| Header | 124 | 24 |
| Line | 2.071 | 277 |
| Line orphan Product | 17 | 1 |
| Header total sai so với line | 0 | 0 |
| Phiếu đóng thiếu `completedAt` | 36 | 4 |
| Ảnh tham chiếu | 17 | 0 |

Toàn bộ 2.348 line nhập/xuất có unit không rỗng, gồm 4 giá trị unit chuẩn hóa khác nhau. Giá line đều parse được và không có tiến độ vượt quantity.

### 5.4. Lịch sử kho

- 528 dòng; 56 dòng tham chiếu Product không còn tồn tại.
- 515 dòng thiếu `source`; 23 dòng thiếu `orderId`/`orderName` usable.
- Chỉ 89/146 Product có history hiện hữu khớp đồng thời cả hai số tồn hiện tại.
- Không được rebuild opening stock từ `storagehistories`. Opening balance phải lấy trực tiếp từ Product Variant sau khi owner đối soát.
- Toàn bộ 528 dòng vẫn phải được giữ. Dòng orphan giữ `LegacyProductObjectId`, `ProductName` snapshot và trạng thái lỗi; `ProductId` SQL được để null.

### 5.5. User và storefront

- 16 User: 1 superadmin, 4 admin, 8 staff và 3 customer.
- 16/16 có password hash bcrypt; 9 User còn `logInString` legacy.
- User không có `createdAt`/`updatedAt`; không được dùng thời điểm migration làm thời điểm tạo tài khoản.
- Có 2 địa chỉ, 1 cart line, 10 mẫu đơn với 27 line và 6 tham chiếu Station; các Product reference này không orphan.
- Có 5 Station, 188 Station–Product reference và không orphan; 3 Station thiếu raw `allowPublicSignup`, trong khi compatibility default của code là `true`.
- `manages` có 4 policy, 9 home-category item, 11 section và 18 Product reference; không orphan.

### 5.6. Audit, chat và integration

- Có 383 ActivityLog với 444 detail; 128 log tham chiếu Product đã bị xóa. TTL 90 ngày nghĩa là log cũ đã hết hạn trước snapshot không thể được khôi phục từ database này.
- Có 3 `chatmessages` chứa PII nhưng không tìm thấy model/consumer trong code hiện tại. Để đáp ứng bảo toàn snapshot, giữ ở vùng archive có quyền truy cập hạn chế thay vì bỏ.
- `autologintokens` rỗng. Chín `logInString` trong User là token legacy và không nên đưa sang SQL; cần revoke khi cutover.
- Snapshot Zalo có 0 field secret/token không rỗng. Schema SQL vẫn không được có cột plaintext secret/token.
- Voice vocabulary hiện có 31 stopword, 24 brand word, 24 type word, 24 brand alias group, 13 type alias group, 4 intent alias group và 5 code map.
- Singleton `chips` hiện có 0 option value; vẫn cần mapping để API có thể thêm Color/Shape/Frame/ButtonCount sau cutover.

## 6. Định nghĩa “bảo toàn toàn bộ”

Mục tiêu khả thi là bảo toàn toàn bộ dữ liệu đang có trong snapshot MongoDB được phê duyệt và toàn bộ file vật lý còn tồn tại tại thời điểm cutover. Không thể tái tạo dữ liệu đã bị xóa trước snapshot, log đã hết TTL, Product đã bị xóa hoặc giá lịch sử chưa từng được lưu.

Migration phải thỏa các nguyên tắc sau:

1. Mọi collection và field quan sát được phải có trạng thái `Mapped`, `Archived`, `SecretStore`, `Empty` hoặc `Blocked`; không có trạng thái bỏ qua ngầm.
2. Mọi document/subdocument được chuyển phải giữ `LegacyObjectId` hoặc mapping tương đương.
3. Dữ liệu orphan vẫn được insert vào bảng chứng từ/lịch sử với FK nullable, ID legacy và `DataStatus`; không tạo Product giả.
4. Giá trị raw quan trọng như VAT, tổng tiền chuỗi và extra legacy field được giữ cạnh giá trị đã parse khi cần đối soát.
5. Thời điểm nguồn bị thiếu vẫn để `NULL`; `MigratedAtUtc` là cột riêng.
6. Sales Order line không có giá lịch sử phải để `UnitPrice = NULL`; header `Total` và reference legacy vẫn được giữ. Không lấy giá hiện tại rồi ghi giả thành giá lịch sử.
7. Password hash bcrypt được chuyển bằng quy trình identity riêng; autologin token/OTP không được copy và phải bị revoke.
8. Secret provider được đưa vào secret manager, không lưu trong SQL.
9. File database chỉ lưu reference; file vật lý phải có migration manifest riêng gồm source path logic, target path, kích thước và checksum.
10. Migration preflight phải fail nếu phát hiện collection/field mới chưa có mapping.

## 7. Ảnh hưởng tới database tổng đã tạo

`[ttsmart.com.vn]` hiện đã có các bảng catalog/customer từ migration `004` theo ownership cũ. Database chưa có seed dữ liệu nghiệp vụ, nên chưa xảy ra mất mát hoặc trùng dữ liệu. Theo nguyên tắc mới, các bảng này không còn là nguồn authoritative: Product/Customer TTSmart thuộc `[TTSmart]`, còn Product/Customer của chi nhánh thuộc `[{BranchCode}_online]` tương ứng. Cần chọn cách loại bỏ kỹ thuật bằng forward migration có guard hoặc rebuild database tổng local trước khi nạp dữ liệu.

Không drop hoặc sửa migration `004` trong công việc khảo sát này.

## 8. Kết luận khảo sát

Code và snapshot đủ để thiết kế `[TTSmart]` thành database bán hàng đầy đủ và chuyển toàn bộ dữ liệu hiện có mà không âm thầm bỏ record. Những trường hợp không thể tạo FK hoặc phục hồi semantic lịch sử đã được nhận diện rõ và cần được bảo toàn dưới dạng legacy evidence/migration issue.

Thiết kế bảng đề xuất nằm tại `SQLSERVER_TTSMART_SCHEMA_DESIGN.md`.

## Cập nhật triển khai DDL ngày 2026-08-14

Theo quyền riêng cho Đợt 2, DDL đã được tạo và chạy trên SQL Server local cho `[TTSmart]`; xem `SQLSERVER_TTSMART_DDL_IMPLEMENTATION.md`. Không có migration dữ liệu MongoDB, seed, copy file, thay runtime hoặc truy cập SQL Server production trong lần triển khai này. Các số liệu discovery ở trên vẫn chỉ là bằng chứng khảo sát read-only và chưa phải dữ liệu đã chuyển đổi.
