# Đường cơ sở Legacy

## Ghi nhận

- Path: `D:\TTSmartEcomWeb`
- Branch: `TTSmartEcom_Deploy`
- Commit: `c836c8122e5d0e28628235b8e0f44c1c718efb91`
- Survey mode: kiểm tra source chỉ đọc; không cài package, không chạy server, build, test, database, seed, migration hoặc gọi dịch vụ ngoài.
- Initial `git status --short`: 58 entries (các thay đổi có sẵn từ trước; không thuộc task migration này).
- Initial status fingerprint SHA-256: `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d` (tính trên 58 dòng `git status --short` sau khi bỏ khoảng trắng cuối dòng, nối bằng LF và không thêm LF cuối; không lặp lại toàn bộ status để tránh nhầm với công việc không liên quan).
- Final branch/commit/status check: branch và commit không đổi; trạng thái dirty có sẵn vẫn được giữ nguyên. Task này không sửa file legacy nào.

## Số liệu kiểm kê

- 201 mounted HTTP handlers.
- 402 effective method/URL forms vì middleware loại bỏ `/api` trong khi các path không có prefix vẫn được hỗ trợ.
- 13 khai báo route bổ sung trong `components/drink.js` chưa được mount; không phải runtime endpoint.
- 21 MongoDB collections được suy ra.
- 42 mẫu route của FE và 144 mẫu route của AD.

## Ghi chú target/repository

Target repository đang ở `main` với origin `https://github.com/DungVu187/TTSmartEcomWeb_v2.git`. Visibility trên GitHub chưa được xác minh vì không có `gh`. Không thực hiện commit, push hoặc deployment.

## Ranh giới bằng chứng

Đường cơ sở này ghi metadata và số liệu của source, không ghi nhận hành vi production. Không đọc hoặc sao chép giá trị secret, thông tin xác thực database, upload, log hay dữ liệu production. Mọi trường chưa có bằng chứng trực tiếp từ source vẫn là câu hỏi mở.

## Khảo sát kiến trúc dữ liệu đích ngày 2026-08-14

- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, `git status --short` có 58 entry và fingerprint SHA-256 `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Phạm vi đọc: model Mongoose, component/router, controller/service liên quan và document/repository V2; không đọc `.env`, dữ liệu, upload, log, backup hoặc secret.
- Không kết nối MongoDB, không chạy server/script migration/seed và không sửa file nào trong worktree legacy.
- Kết quả khảo sát được ghi tại `docs/migration/MONGODB_TO_SQLSERVER_DISCOVERY_AND_TARGET_ARCHITECTURE.md`; đây là thiết kế cho giai đoạn sau, không đưa SQL Server vào runtime Đợt 1.
- Sau khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, `git status --short` vẫn có 58 entry và fingerprint SHA-256 vẫn là `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`; worktree legacy không bị khảo sát làm thay đổi.

## Khảo sát discovery MongoDB → SQL Server mở rộng ngày 2026-08-14

- Trước và sau khảo sát sâu model/repository/service/controller/route/test/fixture: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`.
- `git status --short` giữ nguyên 58 entry; fingerprint SHA-256 trước/sau giữ nguyên `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Không kết nối database, không đọc secret/dữ liệu/upload/log và không sửa worktree legacy.
- Báo cáo authoritative cho discovery này: `docs/migration/MONGODB_TO_SQLSERVER_DISCOVERY_AND_TARGET_ARCHITECTURE.md`.

## Profile MongoDB local `Ecom` được phê duyệt ngày 2026-08-14

- Chủ dự án cho phép đọc dữ liệu database `Ecom` để lập hướng thiết kế SQL Server.
- Nguồn được xác định là MongoDB 8.0.26 local tại `127.0.0.1:27017`; không đọc `.env`, không kết nối production và không xuất document/PII/credential.
- Chỉ chạy command/aggregate/read-only để lấy count, storage, index, BSON type, missing/null, duplicate, orphan và array distribution; không ghi database hoặc worktree legacy.
- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, 58 status entry, fingerprint `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Kết quả được ghi tại `docs/migration/MONGODB_ECOM_DATA_PROFILE_AND_SQLSERVER_DECISIONS.md`.
- Sau khảo sát: branch/commit giữ nguyên, `git status --short` vẫn có 58 entry và fingerprint vẫn là `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`; database vẫn có 19 collection/1.503 document và `autologintokens` vẫn có 0 document. Khảo sát không làm thay đổi worktree legacy hoặc dữ liệu `Ecom`.

## Khảo sát lại `[TTSmart]` như database bán hàng đầy đủ ngày 2026-08-14

- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, 58 status entry, fingerprint `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Phạm vi đọc: `AGENTS.md`, model, route, controller/service, validator/helper và consumer FE/AD của Product, Order, IpOrder, EpOrder, StorageHistory, User, Station, Manage, ActivityLog, Voice, Telegram và Zalo.
- MongoDB local `Ecom` được profile lại read-only; không đọc `.env`, secret, nội dung PII cụ thể, upload hoặc backup, không ghi database và không chạy test có khả năng xóa dữ liệu.
- Kết quả: `TTSMART_CODE_AND_DATA_DISCOVERY.md` và `SQLSERVER_TTSMART_SCHEMA_DESIGN.md`; ownership cũ đã được đánh dấu là bị thay thế.
- Sau khảo sát: branch/commit/status/fingerprint legacy giữ nguyên; khảo sát không sửa file hoặc dữ liệu trong repository legacy.

## Khảo sát tổng thể kiến trúc dữ liệu ngày 2026-08-15

- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, `git status --short` có 58 entry và fingerprint SHA-256 `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Phạm vi được chủ dự án cho phép: khảo sát read-only source legacy, MongoDB local `Ecom`, SQL Server local `[ttsmart.com.vn]` và `[TTSmart]`, cùng code/tài liệu V2 để đề xuất kiến trúc dữ liệu dài hạn.
- Không đọc `.env` hoặc xuất secret/PII; không sửa source legacy, không chạy seed/migration và không ghi MongoDB/SQL Server trong quá trình khảo sát.
- Sau khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, `git status --short` vẫn có 58 entry và fingerprint vẫn là `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`; worktree legacy không bị thay đổi.
- MongoDB `Ecom` vẫn được khảo sát read-only; không chạy insert/update/delete, seed hoặc migration. SQL Server chỉ được đọc metadata, aggregate an toàn và kiểm tra tính nhất quán; không có DDL/DML được thực thi trong lượt này.
- Đối chiếu xác nhận các blocker trước Đợt 2 gồm ID công khai 24-hex, version Mongo bắt đầu từ 0, mapping subdocument có `SourcePath`, kiểu số theo API contract, auth local, ledger tồn idempotent, metadata file và template `_online` không hardcode tên database.

## Khảo sát đối chiếu baseline SQL Server v1 ngày 2026-08-15

### Git legacy trước khảo sát

- Branch: `TTSmartEcom_Deploy`.
- Commit: `c836c8122e5d0e28628235b8e0f44c1c718efb91`.
- `git status --short`: 58 entry đang tồn tại, fingerprint SHA-256 `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d` theo quy ước đã ghi ở đầu tài liệu. Không đưa chi tiết từng dòng status vào đây để không nhầm chúng là thay đổi của khảo sát.
- Phạm vi: chỉ đọc source legacy và code/tài liệu V2; không đọc `.env`, giá trị secret/token/password, upload, log, dump hoặc backup; không kết nối MongoDB/SQL Server, không chạy server, test, seed hay migration.

### Phát hiện có bằng chứng

- `be/index.js` mount 13 router nghiệp vụ không prefix và middleware chỉ loại `/api` trước khi định tuyến. Do đó mọi API consumer FE/AD dùng các đường dẫn như `/products`, `/orders`, `/iporders`, `/eporders`, `/users`, `/stations`, `/manages`, `/voice-vocabs`, `/histories`, `/activity-logs`; schema SQL không được kéo theo việc đổi public ID, method hoặc path. Các API client tương ứng nằm tại `fe/src/api/*.js` và `ad/src/api/*.js`.
- `models/product.js` cho thấy `Product.code` là sparse unique, có thể thiếu/rỗng sau khi trim; `variant[].price` và `variant[].importPrice` là chuỗi, còn `earn`, hai lượng tồn và `purchaseCount` là Number. `vat` cũng là chuỗi. Vì vậy baseline operational cần giữ raw value có thể không parse được bên cạnh giá decimal đã parse, đồng thời không ép ProductCode thành bắt buộc hay tạo giá/snapshot giả khi migrate.
- Cùng model Product, `variant`, `documents` và `reviews` là subdocument Mongoose có `_id`; product document còn đổi URL ảnh bằng cách chỉ giữ suffix từ `/images/`, `/station/` hoặc `/section-images/`. Chuyển sang quan hệ phải giữ source identity/`SourcePath` cho các phần tử lồng nhau và giữ URL legacy qua alias/file metadata, không lưu đường dẫn vật lý tuyệt đối.
- `models/order.js` lưu `cartItems[].productId` dạng String và `variantIndex` theo vị trí mảng; không có snapshot sản phẩm, đơn giá hoặc VAT trên dòng. `models/iporder.js` và `models/eporder.js` cũng dùng `productList[].productId` dạng String, giá/tổng/VAT dạng String và cho phép product ID thiếu. DDL cần cho phép dòng legacy mồ côi với tham chiếu legacy, giữ thứ tự/variant index và để giá snapshot nullable; số decimal chỉ nên được tạo khi parse xác định.
- `models/iporder.js`/`models/eporder.js` tính `total` bằng `parseFloat` sau khi xóa dấu chấm và đổi dấu phẩy, rồi lưu lại String. Đây là hành vi parse có tính locale của legacy; migration phải ghi nhận lỗi parse vào `MigrationIssues`, không sửa lặng lẽ hoặc dùng tổng migration làm business total.
- `models/user.js` gộp cart, address, order template và station vào User; các phần tử mặc định có `_id` của Mongoose. `controllers/userOrderTemplates.js` thao tác template theo index và trả index mới sau khi thêm. Các bảng Cart/Address/Template cần `SortOrder` và `SourcePath`/mapping để bảo toàn thứ tự API, dù khóa quan hệ mới dùng GUID.
- `models/user.js` có bcrypt hash trong `password`, nhưng cũng có `resetOtp` và `logInString`; `middlewares/auth.js` đọc cookie JWT tên `authToken`, hiệu lực mặc định 12 giờ, và hiện có `ADMIN_FULL_ACCESS = true`. Không chuyển giá trị OTP/autologin plaintext sang SQL; chỉ có thể chuyển password hash và metadata an toàn. Bất kỳ thay đổi nào đối với quyền admin hoặc session phải được theo dõi như thay đổi tương thích/bảo mật, không suy ra từ schema.
- `models/manage.js` có các section `section1`…`section11`, mảng `productId`, localized text `vi/zh/en`, và default storefront `displayPartners`, `showSidebar`, `showQuickCategories` đều là true. Một số subdocument cố ý `_id: false`; vì vậy khi relationalize phải dùng đường dẫn nguồn và thứ tự thay vì giả định mọi child có ObjectId.
- `models/voicevocab.js` lưu voice vocabulary trong một document với child `_id: false`; các group/alias/pattern có thể lặp lại. `models/telegram.js` lưu recipient và `notifyTypes` dạng mảng. Baseline phải cho phép duplicate legacy voice, bảo toàn `SourcePath`/`SortOrder`, và dùng subscription theo event thay vì rút gọn thành một cờ thông báo.
- `models/zalo.js` chứa trực tiếp app secret và access/refresh token trong Mongo; đây là bằng chứng phải chuyển sang `SecretReferences` ngoài SQL, không copy giá trị vào ControlPlane/Operational. `models/activitylog.js` có TTL 90 ngày; retention của audit/archive SQL là quyết định cần đặt rõ, không được ngầm coi log legacy là lưu vĩnh viễn.
- `controllers/orderCreation.js`, `services/inventory.js`, `services/productVariantOperations.js` dùng `findOneAndUpdate`/`$inc`; order phát Socket.IO `order_created`, `order_updated`, `order_cancelled`, `order_deleted` theo `controllers/orderCreation.js` và `controllers/orderLifecycle.js`. Khi có runtime SQL về sau, sequence/stock ledger/idempotency và sự kiện phải được đối soát transactionally; baseline DDL đơn thuần chưa là bằng chứng tương đương hành vi.
- `docs/migration/MONGODB_MODEL_MAP.md` V2 xác nhận 21 collection source và 19 collection từng quan sát ở Mongo local, trong đó `autologintokens`/`chatmessages` xuất hiện trong data nhưng không có model source hiện tại. Không đưa hai collection này vào migration mặc định; cần quyết định retention/quarantine riêng và chỉ dùng dữ liệu tổng hợp trong kiểm thử schema.

### Git legacy sau khảo sát

- Branch: `TTSmartEcom_Deploy`.
- Commit: `c836c8122e5d0e28628235b8e0f44c1c718efb91`.
- `git status --short`: vẫn 58 entry, khớp trạng thái đã ghi trước khảo sát. Không có file nào trong `D:\TTSmartEcomWeb` bị sửa bởi công việc này.

## Khảo sát mapping field-level và runner SQL v1 ngày 2026-08-15

- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`; `git status --short` có 58 entry, giữ fingerprint SHA-256 `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d` theo quy ước đầu tài liệu.
- Phạm vi chỉ đọc: 15 file model Mongoose legacy, metadata Git legacy, DDL/runner/verification SQL v1 và tài liệu V2 liên quan. Không đọc `.env`, giá trị secret, upload, log, dump, backup hoặc document dữ liệu MongoDB; không chạy server, database, seed, migration hay test.
- Kết quả field-level và các gap runner/verification được ghi tại `docs/migration/MONGODB_TO_SQLSERVER_MAPPING_V1.md`. Không sửa bất kỳ file nào trong worktree legacy.
- Sau khảo sát: branch và commit không đổi; `git status --short` vẫn có 58 entry, fingerprint vẫn là `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`. Worktree legacy được bảo toàn.

## Kiểm tra ranh giới legacy trước cutover MongoDB → SQL Server ngày 2026-08-17

- Trước và sau kiểm tra: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`.
- `git status --short` vẫn có 58 entry. Không đọc hoặc sửa `.env`, secret, document MongoDB, upload, dump, log hay backup trong repository legacy.
- Worktree legacy không bị thay đổi. Dữ liệu MongoDB `Ecom` được profile qua kết nối read-only từ V2 theo quyền cutover riêng, không ghi MongoDB.

## Khảo sát file nguồn phục vụ migration ngày 2026-08-17

- Trước khảo sát đã ghi nhận branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, 58 entry trong `git status --short` và fingerprint SHA-256 `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Phạm vi chỉ đọc: ba thư mục upload `be\upload\images`, `be\upload\documents` và `be\upload\invoices`, cùng hai bản sao upload đã xác định. Chỉ tính số lượng, dung lượng và checksum tổng hợp; không đưa tên file, nội dung file, PII, secret hoặc credential vào tài liệu/log.
- Sau khảo sát: branch `TTSmartEcom_Deploy`, commit `c836c8122e5d0e28628235b8e0f44c1c718efb91`, `git status --short` vẫn có 58 entry, fingerprint SHA-256 vẫn là `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Không có file nào trong `D:\TTSmartEcomWeb` bị sửa, di chuyển hoặc xóa bởi khảo sát/copy migration.
