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

## Đối chiếu cập nhật legacy ngày 2026-08-21

### Git legacy trước khảo sát

- Branch: `TTSmartEcom_Deploy`.
- Commit: `f33bd76af084c25e22ab04d84a36c132a0ea5062` (`migrate storefront from CRA to Vite`).
- `git status --short`: 23 entry: 19 file đã sửa ở admin/backend, 3 file untracked cho xử lý `transactionDate`, và thư mục untracked `fe/fe/`.
- Phạm vi chỉ đọc: metadata Git, diff commit kể từ baseline `c836c8122e5d0e28628235b8e0f44c1c718efb91`, các diff working-tree liên quan IpOrder/EpOrder/StorageHistory, và frontend Vite. Không đọc `.env`, secret, upload, log, dump, backup hay dữ liệu database; không sửa worktree legacy.

### Phát hiện có bằng chứng

- Commit `f33bd76` chuyển storefront khách hàng từ CRA sang Vite, đổi build output `fe/build` thành `fe/dist`, dùng `import.meta.env.VITE_BACK_END`, thêm proxy `/api`, và backend legacy chấp nhận cả route không prefix lẫn `/api`. V2 đã có storefront Vite cùng `package.json`, `src/App.jsx`, `src/main.jsx` và `src/api/httpClient.js` tương ứng; đây không phải lý do để chuyển JSX sang TSX.
- Working tree legacy bổ sung `transactionDate` cho `iporders` và `eporders`; payload `POST /iporders/orders`, `POST /eporders/orders`, `PUT /iporders/orders/:id` và `PUT /eporders/orders/:id` chấp nhận trường này, reject giá trị không parse được với 400, và response/list sử dụng ngày này thay cho `completedAt` khi lọc `byCompletedDate=true`.
- Các `storagehistories` sinh từ thao tác import/export mang `transactionDate` từ header đơn. Khi đổi ngày header, lịch sử cùng `orderId` được cập nhật. Query lịch sử chính chuyển lọc/sort ngày sang `transactionDate`, nhưng dữ liệu cũ thiếu/null vẫn fallback `createdAt`.
- Sửa trực tiếp `quantityRe` của dòng nhập với `quantityAdjustment=true` sinh event nguồn `import_quantity_adjustment`, note chứa before/after, giữ cả thay đổi có delta âm trong filter chiều nhập. Model `StorageHistory` bổ sung `quantityBefore`, `quantityAfter` và source mới.
- `fe/fe/` untracked có 100 file, cùng danh sách với `D:\TTSmartEcomWeb_v2\fe` nhưng khác nội dung ở 14 file. Đây là bản sao chưa được quản lý trong legacy, không phải nguồn thay thế có thể tự động nhập vào V2.

### Git legacy sau khảo sát

- Branch và commit không đổi: `TTSmartEcom_Deploy` / `f33bd76af084c25e22ab04d84a36c132a0ea5062`.
- `git status --short` vẫn đúng 23 entry như trước khảo sát.
- Khảo sát không làm thay đổi file nào trong `D:\TTSmartEcomWeb`.

### Git legacy sau khảo sát route ngày 2026-08-21

- Trước và sau khảo sát: branch `TTSmartEcom_Deploy`, commit `f33bd76af084c25e22ab04d84a36c132a0ea5062`.
- `git status --short` trước/sau đều có 22 entry, fingerprint SHA-256 `46af965c65e33c5b08f085ea13508b8ae758df2b8e2ab2091b3e0985397dcdbd`.
- Phạm vi chỉ đọc component Đơn nhập/Đơn xuất và các field `createdAt`, `transactionDate`, `completedAt`; worktree legacy không bị chỉnh sửa.

## Đối chiếu audit Đợt 2 ngày 2026-08-24

- Trước khảo sát: branch `TTSmartEcom_Deploy`, commit `f33bd76af084c25e22ab04d84a36c132a0ea5062`; `git status --short` có 22 entry với fingerprint SHA-256 `46af965c65e33c5b08f085ea13508b8ae758df2b8e2ab2091b3e0985397dcdbd`.
- Phạm vi chỉ đọc: model và validation IpOrder/EpOrder để xác nhận kiểu `quantity`, `quantityRe`, `quantityEx`, `stockAppliedQuantity` và hành vi `transactionDate`. Không đọc `.env`, secret, upload, log, dump, backup hoặc dữ liệu MongoDB; không chạy server, test, seed hay migration trong legacy.
- Kết quả: `quantity` của dòng nhập/xuất được validation là số nguyên; tiến độ nhập cho phép số hữu hạn không âm, còn tiến độ xuất được validation là số nguyên. Đây là bằng chứng contract cho việc domain hiện giữ `quantity` dạng số nguyên, nhưng không cho phép adapter SQL đổi aggregate `decimal(19,6)` sang `double` mà không có kiểm thử biên độ chính xác.
- Sau khảo sát: branch, commit, số entry và fingerprint không đổi. Không có file nào trong `D:\TTSmartEcomWeb` bị sửa bởi lượt audit.

## Đối chiếu Phase 3B ngày 2026-08-24

- Trước và sau lượt làm việc chỉ đọc phục vụ Phase 3B: branch `TTSmartEcom_Deploy`, commit `f33bd76af084c25e22ab04d84a36c132a0ea5062`.
- `git status --short` có cùng 22 entry trước/sau, gồm các thay đổi inventory order hiện hữu và ba file untracked liên quan ngày giao dịch.
- Không khảo sát hay sửa source/data legacy để suy ra Company schema; mọi DDL/test Phase 3B chỉ nằm tại V2 và database test được cấp phép.
## Khảo sát chức năng lốp V1 ngày 2026-08-27

### Git legacy trước khảo sát

- Branch: `TTSmartEcom_Deploy`.
- Commit: `73b6967d65e1ca2ac32e9cf7484772e70c51c140`.
- `git status --short`: sạch, không có entry.
- Phạm vi chỉ đọc: source tại `D:\TTSmartEcomWeb` liên quan đến chức năng lốp; không đọc `.env`, secret, upload, log, dump, backup hoặc dữ liệu production; không chạy server, build, test, database, seed hay migration và không sửa worktree legacy.

### Phát hiện có bằng chứng

- Chức năng được đưa vào qua hai commit ngày 2026-08-27: `e37dc2ad64e548c8a5e57e74a111347d4ab1e031` (`feat: add tire order management`) và `73b6967d65e1ca2ac32e9cf7484772e70c51c140` (`feat: enhance tire and vehicle lifecycle management`). So với commit `f33bd76af084c25e22ab04d84a36c132a0ea5062`, lát cắt này thêm model `Vehicle`, `TireOrder`, route/controller/service chuyên biệt, giao diện admin, permission và test; đồng thời mở rộng `StorageHistory`, bảo vệ Product/Variant đang được đơn lốp tham chiếu và bổ sung `transactionDate` cho đơn nhập/xuất.
- Backend mount 25 handler thuộc ba nhóm: 5 handler `/vehicles`, 18 handler `/tire-orders` và 2 handler `/tire-lifecycles`. Middleware tương thích hiện hữu tiếp tục cho phép cả URL không prefix và URL có `/api`, tương ứng 50 dạng method/URL. Tất cả route đều dùng xác thực admin/staff và permission; `tireorder` có `view/create/edit/delete`, còn `tirelifecycle` có permission `view` riêng.
- `Vehicle` quản lý biển số chuẩn hóa/duy nhất, tên xe, ghi chú, trạng thái hoạt động, loại xe 10 hoặc 12 bánh, timestamp và optimistic concurrency. Xóa xe là xóa mềm qua `isActive = false`; test cho phép tạo xe mới dùng lại biển số của xe đã ngừng và chặn đưa xe không hoạt động vào đơn mới.
- `TireOrder` lưu header đơn, ngày giao dịch, trạng thái `processing/completed`, xóa mềm, danh sách xe nhúng và từng lốp gán theo vị trí. Mỗi assignment giữ tham chiếu Product/Variant cùng snapshot mã, tên, thương hiệu, giá xuất, thuộc tính sản phẩm/variant, ngày thay, thời điểm lốp cũ ngưng hoạt động, ghi chú và trạng thái đã áp dụng tồn. Model tự tính tổng xe, tổng lốp, tổng giá xuất và số lốp đã áp dụng tồn.
- Sơ đồ vị trí được khóa bằng identifier ổn định: xe 10 bánh có 10 vị trí, xe 12 bánh có thêm hai vị trí cầu trước thứ hai. Model/service chặn trùng xe trong một đơn, trùng vị trí, vị trí ngoài sơ đồ, số lượng không khớp số vị trí và vượt sức chứa. Chỉ Product có phân loại chính xác `Lốp xe` được tìm/chọn; Product hoặc Variant đã được đơn lốp tham chiếu bị chặn xóa.
- Luồng đơn hỗ trợ tạo/sửa metadata, thêm/xóa xe, thêm/sửa/xóa lốp, di chuyển vị trí, xác nhận thay thế lốp đang chiếm vị trí, hoàn thành, hủy hoàn thành, xóa mềm và xem lịch sử theo xe trong đơn. Mutation dùng `expectedVersion`/Mongoose optimistic concurrency và trả `409 VERSION_CONFLICT` khi dữ liệu đã đổi.
- Hoàn thành đơn xác minh lại Product/Variant và mốc thời gian vòng đời, gom lốp theo xe + Product + Variant, trừ đồng thời `quantityForSale` và `quantityInStorage`, ghi `StorageHistory` có `inventoryOperationId`, rồi khóa trạng thái đơn. Nếu bước sau thất bại, code xóa lịch sử operation và bù tồn; hủy hoàn thành làm chiều ngược lại. Xóa mềm đơn đã hoàn thành cố ý không hoàn tồn và vẫn giữ dữ liệu để tái dựng lịch sử lốp.
- Vòng đời lốp không có collection riêng. Service đọc toàn bộ đơn `completed`, nhóm theo `vehicleId + slotId`, sắp theo ngày lắp và suy ra `active/ended`, ngày kết thúc, đơn lắp và đơn thay thế. API hỗ trợ lọc xe/biển số, mã/tên lốp, loại xe, số vị trí, trạng thái và khoảng ngày; trang chi tiết trả toàn bộ chuỗi tại một vị trí. Đơn xóa mềm vẫn tham gia chuỗi và được đánh dấu `installOrderDeleted`/`replacementOrderDeleted`.
- Admin có ba route `/tire-orders`, `/tire-orders/:id`, `/tire-lifecycles`; sidebar gom dưới “Quản lý phụ tùng xe”. Giao diện gồm danh sách/lọc/phân trang đơn, quản lý xe ngay trong chi tiết, sơ đồ chassis tương tác 10/12 bánh, chọn nhiều vị trí, cảnh báo lốp cũ đang hoạt động, ngày thay/ngưng hoạt động, trừ/hoàn tồn và bảng/timeline vòng đời có liên kết tới đơn liên quan. Storefront khách hàng không có consumer của domain này.
- Bằng chứng test gồm `tire_slots.test.js`, `tireorder_model.test.js`, `tireorder_integration.test.js` và permission catalog test. Integration test bao phủ xóa mềm xe/đơn, quyền vòng đời riêng, lọc đúng loại `Lốp xe`, trừ/hoàn tồn, layout 10/12 bánh, lịch sử nhiều lần thay và liên kết đơn đã xóa. Không chạy test trong lượt khảo sát vì suite integration hardcode MongoDB local `mongodb://localhost:27017/test` và thực hiện `deleteMany({})`; yêu cầu hiện tại chỉ cho khảo sát source, chưa cho phép tác động database test.

### Giới hạn và điểm cần giữ khi đưa sang Đợt 2

- V2 hiện chưa có code, DDL hay tài liệu mapping chứa `Vehicle`, `TireOrder`, `tireorder`, `tirelifecycle` hoặc `tire_order`; kiểm kê 21 collection và ma trận API hiện tại vì vậy chưa bao phủ hai collection/model mới cùng 25 handler này.
- Theo kiến trúc ba tầng đã chốt, Vehicle, TireOrder, assignment, stock operation, `StorageHistory` và vòng đời chi tiết thuộc Branch Operational DB; Product/Variant nguồn tham chiếu thuộc Company DB. Thiết kế SQL phải dùng logical reference đã được application xác minh và snapshot nghiệp vụ, không tạo foreign key hoặc transaction xuyên database.
- `tireLifecycleService` hiện tải mọi đơn hoàn thành và dựng toàn bộ timeline trong bộ nhớ cho mỗi request; đây là hành vi baseline nhưng là rủi ro hiệu năng cần đo và thiết kế read model/query SQL phù hợp, không sao chép máy móc.
- `tireOrderActivity.js` và catalog action cho đơn lốp tồn tại nhưng không có lời gọi ghi activity trong route/service hiện tại; integration test còn khẳng định không có `ActivityLog` cho `tire_order`. Vehicle chỉ ghi activity khi tạo, chưa ghi khi sửa/ngừng/xóa mềm. Không được tuyên bố audit đầy đủ nếu chưa có quyết định tương thích và bổ sung kiểm thử.
- Source/schema vẫn khai báo `tire_order_delete_revert`, nhưng hành vi mới xóa mềm đơn hoàn thành mà không hoàn tồn nên không phát sinh source này. Cần bảo toàn khả năng đọc dữ liệu cũ nhưng không suy ra đây là event runtime còn được phát.
- API list vòng đời nhận query `limit` từ frontend nhưng backend cố định 10 item/trang. Đây là contract quan sát được cần ghi nhận rõ khi lập ma trận, không tự đổi trong migration persistence.
- Giá xuất snapshot vẫn là String legacy và tổng tiền được suy ra bằng cách loại ký tự không phải chữ số/dấu âm. Migration SQL phải giữ raw value, chỉ materialize `decimal(19,4)` khi parse xác định và ghi issue cho giá trị không parse được; không dùng phép parse hiện tại làm bằng chứng dữ liệu tiền đã chuẩn hóa.
- Chưa xác minh runtime, hiệu năng, rollback khi process chết giữa các bước hoặc hành vi trên dữ liệu thật. Khảo sát source không phải bằng chứng tương đương SQL Server hay sẵn sàng cutover.

### Git legacy sau khảo sát

- Branch: `TTSmartEcom_Deploy`.
- Commit: `73b6967d65e1ca2ac32e9cf7484772e70c51c140`.
- `git status --short`: sạch, không có entry; `git diff --check` không báo lỗi.
- Không có file hoặc dữ liệu nào trong `D:\TTSmartEcomWeb` bị sửa bởi lượt khảo sát.

## Profile MongoDB local `Ecom` cho chức năng lốp ngày 2026-08-27

- Trước khi đọc MongoDB: legacy ở branch `TTSmartEcom_Deploy`, commit `73b6967d65e1ca2ac32e9cf7484772e70c51c140`, worktree sạch.
- Chủ dự án cho phép khảo sát hai collection mới. Chỉ chạy read-only command/query tổng hợp trên MongoDB 8.0.26 local `127.0.0.1:27017/Ecom`, không đọc `.env`, không xuất document/ObjectId/biển số/tên người và không ghi database.
- Snapshot đầu lượt: 21 collection/1.670 document; `vehicles=7`, `tireorders=6`. Profile xác nhận 7 vehicle entry, 4 assignment, 3 inventory adjustment và 21 `storagehistories` nguồn lốp.
- Kết quả chi tiết được ghi tại `docs/migration/MONGODB_ECOM_TIRE_PROFILE_2026-08-27.md`; model map và mapping field-level đã đánh dấu hai collection `Blocked` vì chưa có Branch schema/mapper/dry-run/reconcile.
- Trong khi profile đang chạy, worktree legacy xuất hiện thay đổi đồng thời ở `be/package.json`, `be/package-lock.json`, `be/tests/tireorder_integration.test.js` để chuyển integration test sang `mongodb-memory-server`, sau đó xuất hiện thêm năm file test frontend lốp untracked: `ad/src/api/tireOrderAdministrationApi.test.js`, `ad/src/components/tireorder/TireChassis.test.jsx`, `tireSlots.test.js`, `tirelifecycles.test.jsx`, `tireorders.test.jsx`. Các lệnh profile không sửa file; các thay đổi này được giữ nguyên và không được tính là kết quả khảo sát.
- Sau lượt đọc: branch/commit không đổi; `git status --short` có tám entry đồng thời nêu trên. Snapshot MongoDB cuối lượt vẫn 21 collection/1.670 document; `vehicles=7`, `tireorders=6`, không đổi.
