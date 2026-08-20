# Trạng thái migration

## Baseline SQL Server v1 ngày 2026-08-15

Đợt 2 đang thực hiện. Baseline test-only tại `database/sqlserver/v1/`: `TTSmart_Control_V1_Test` (32 bảng, 6 migration, 32 PK/41 FK/91 CHECK/40 UQ/89 index) và `TTSmart_Operational_V1_Test` (76 bảng, 11 migration, 76 PK/77 FK/155 CHECK/70 UQ/159 index). `Test-SqlServerV1Baseline.ps1` đã chạy: recreate, concurrent first-run, chạy lặp idempotent, layout/checksum, constraint/security, fingerprint mutation, verify/DBCC và kiểm tra dữ liệu còn lại. SQL module của cả hai baseline đã được kiểm tra UTF-8 theo collation nhị phân; runner đọc SQL bằng `sqlcmd -f i:65001,o:65001`. Không sửa `[ttsmart.com.vn]` (23 bảng/8 migration), `[TTSmart]` (54 bảng/9 migration) hoặc MongoDB `Ecom`. Chi tiết: `SQLSERVER_V1_BASELINE_IMPLEMENTATION.md`.

Baseline v1 hiện được giữ ổn định cho bước migration trên dữ liệu tổng hợp. Đây không phải xác nhận cutover: chưa profile/dry-run bản sao MongoDB được phê duyệt và chưa đổi runtime ASP.NET Core sang SQL Server.

## Công cụ migration MongoDB → Operational test

Console `TTSmartEcom.MongoSqlMigration` có các chế độ `profile`, `dry-run`, `migrate`, `reconcile` và `fixture`; nó chỉ chấp nhận SQL target `TTSmart_Operational_V1_Test`, MongoDB chỉ được mở qua API đọc và không in URI/payload/PII. Migration theo batch 100 document, có transaction/savepoint từng document, mapping root dùng `SourceKey`/`SourceKeyType`/`SourcePath`, lỗi từng document tạo `MigrationIssues` và fallback sang `LegacyRecords` Canonical Extended JSON. Document được map chuẩn cũng có bản evidence để không làm mất field chưa có mapper; secret/token/password/OTP trong evidence được thay marker SHA-256.

Fixture tổng hợp đã chạy hai lần: mỗi lượt `source=3`, `standard=1`, `preserved=2`, `errors=1`, `skipped=0`. Sau lượt hai vẫn chỉ có một Product, một mapping root, ba `LegacyRecords`, một issue, một manifest; fixture file tùy chọn cũng giữ đúng một `Files`/`FileLocations` và SHA-256 khớp file local test. Toàn bộ fixture và file test đã được dọn, Operational được recreate. Chưa chạy profile, dry-run, migrate hoặc reconcile trên MongoDB `Ecom` hay bản sao được phê duyệt; mapper chuẩn cho collection thực, đối soát tổng tiền/tồn kho/file thực vẫn Blocked.

Cập nhật profile/migration test Ecom ngày 2026-08-15: đã profile trực tiếp 19 collection/1.503 document, dry-run `skipped=0`, migrate hai lần và reconcile pass trên `TTSmart_Operational_V1_Test`. Lượt thứ hai không tăng số dòng đích. Mapper chuẩn có bằng chứng cho Brand, ProductType, Category/CategoryValue, Product, ProductVariant và Stock; 1.126 document thuộc các collection chưa có rule nghiệp vụ được bảo toàn Canonical Extended JSON có redaction cùng MigrationIssue, không bị bỏ qua. Tổng tiền, Sales/Import/Export/history/file thực vẫn Blocked; xem `MONGODB_ECOM_MIGRATION_TEST_REPORT_2026-08-15.md`.

## Khảo sát tổng thể dữ liệu ngày 2026-08-15

Đã khảo sát read-only lại SQL Server local `[ttsmart.com.vn]` và `[TTSmart]`, MongoDB local `Ecom`, source legacy và code/contract V2. Hai database SQL vẫn không có dữ liệu nghiệp vụ: database tổng chỉ có 8 dòng `SchemaVersions`; `[TTSmart]` chỉ có 1 dòng `DatabaseInfo` và 9 dòng `SchemaVersions`. Không chạy DDL/DML, migration dữ liệu, seed, copy file, commit, push hoặc deploy trong lượt khảo sát.

Metadata live vẫn khớp số liệu trước đó, nhưng DDL hiện tại chưa phải gate để copy MongoDB hoặc cutover SQL. Các blocker chính đã xác nhận gồm: thiếu `PublicId` 24-hex cho bản ghi SQL mới trong khi API còn contract ObjectId; `Version > 0` không nhận được nhiều document Mongo có `__v = 0`; `LegacyIds` không ánh xạ một document/subdocument sang nhiều target và thiếu `SourcePath`; kiểu `int`/`tinyint` cùng độ dài một số cột hẹp hơn API; ledger tồn/progress/Variant chưa đủ; database vận hành chưa có local credential để chạy LAN độc lập; chưa có metadata/checksum/trạng thái file; migration `[TTSmart]` hardcode tên database nên chưa thể làm template `_online`.

Database tổng cần harden ranh giới role theo Company/scope, registry nhiều máy/VPS, ASCII allowlist tên database/login, feature entitlement, AI reservation và lease provisioning. Cả hai database đang `AUTO_CLOSE = ON`; instance local là SQL Server 2022 Express RTM `16.0.1000.6`, chưa có backup history. Đây là findings thiết kế/vận hành, chưa được sửa trong lượt khảo sát.

Profile file read-only xác nhận 258 reference không rỗng: 256 file local đều tồn tại và 2 URL tài liệu ngoài; có thêm 73 file vật lý không còn được Mongo tham chiếu. Khi migration phải lập manifest/checksum và giữ nhóm này dưới trạng thái legacy chưa liên kết, không xóa âm thầm.

## Snapshot: 2026-08-14

### Khảo sát thiết kế database bán hàng `[TTSmart]`

Theo xác nhận mới của chủ dự án, `[TTSmart]` là database bán hàng đầy đủ của TTSmart, không phải database chỉ chứa Station/storefront/config. Đã khảo sát read-only model, route, controller/service và consumer frontend legacy liên quan; đồng thời profile lại MongoDB local `Ecom` bằng MongoDB driver với application name `TTSmartEcomReadOnlyProfiler`. Snapshot vẫn có 19 collection/1.503 document.

Phương án mới được ghi tại `TTSMART_CODE_AND_DATA_DISCOVERY.md` và `SQLSERVER_TTSMART_SCHEMA_DESIGN.md`. Dữ liệu nghiệp vụ TTSmart gồm Product/Variant, customer/cart/template, Sales Order, Import/Export Order, hai số tồn, 528 history, Station, storefront, voice và integration metadata được đưa vào ownership `[TTSmart]`. Thiết kế giữ toàn bộ orphan/extra legacy field bằng ID legacy, FK nullable và trạng thái chất lượng; không rebuild opening stock từ history và không bịa giá Sales Order line vốn chưa được lưu ở nguồn.

Chủ dự án chốt mỗi chi nhánh có tồn kho/chứng từ riêng, có thể có catalog riêng, không yêu cầu đồng bộ tức thời và ưu tiên khả năng cài local/cô lập dữ liệu. Vì vậy Product authoritative nằm tại `[TTSmart]` hoặc `[{BranchCode}_online]` tương ứng; database tổng không sở hữu Product master.

Đã tạo database `[TTSmart]` và chạy DDL `001`–`009`: 54 bảng, 54 PK, 52 FK, 77 check constraint, 18 unique constraint và 118 index. Đồng thời migration forward `008_RemoveSalesTables.sql` của `[ttsmart.com.vn]` đã preflight mười bảng catalog/customer rỗng, không dependency ngoài danh sách, rồi loại chúng an toàn; database tổng còn 23 bảng control-plane. Đã chạy lại script để xác minh idempotency. Chưa dry-run/copy dữ liệu, chưa seed, chưa đọc/copy file vật lý và chưa thay runtime. Xem `SQLSERVER_TTSMART_DDL_IMPLEMENTATION.md`.

### Thực hiện DDL database tổng Đợt 2 được phê duyệt riêng

Theo quyền riêng cho phần database tổng, `[ttsmart.com.vn]` đã được tạo trên SQL Server local `DESKTOP-5O6VV3J\SQLEXPRESS` bằng Windows Authentication. DDL version hóa tại `database/sqlserver/ttsmart.com.vn/` đã chạy các migration `001`–`008`. Sau khi migration 008 loại mười bảng catalog/customer rỗng, schema còn 23 bảng control-plane. Không có seed dữ liệu nghiệp vụ.

Metadata đã xác minh không có cột tên connection string, database/SQL password, secret value, access token hoặc API key; cũng không có kiểu `money`, `smallmoney`, `float`, `real`, `text` hay `ntext`. `UserPasswords.PasswordHash` là password hash ứng dụng, không phải SQL Login password. Phạm vi này chưa tạo `[TTSmart]`, database branch `[{BranchCode}_online]`, bảng đồng bộ, migration dữ liệu hoặc runtime SQL Server/EF Core. Chi tiết bảng, quyết định thiết kế và phần chưa xác minh: [SQLSERVER_TTSMART_COM_VN_DDL_IMPLEMENTATION.md](SQLSERVER_TTSMART_COM_VN_DDL_IMPLEMENTATION.md).

Khảo sát read-only cho kiến trúc dữ liệu SQL Server của giai đoạn sau được ghi tại `MONGODB_TO_SQLSERVER_DISCOVERY_AND_TARGET_ARCHITECTURE.md`. MongoDB local `Ecom` được profile tại `MONGODB_ECOM_DATA_PROFILE_AND_SQLSERVER_DECISIONS.md`: 19 collection và 1.503 document. SQL Server local hiện hữu được khảo sát tại `SQLSERVER_DANGNHAP_ONLINE_DISCOVERY.md`: `dangnhap.net` là database tổng 54 bảng; `hiephung2_online`/`petro8_online` là database chi nhánh gần cùng schema, nhưng `petro8_online` có thêm `NhapKho` và các bảng lịch sử đạt hàng triệu dòng. Chủ dự án đã chốt tên vật lý `[ttsmart.com.vn]` cho database tổng, `[TTSmart]` cho database riêng và `[{BranchCode}_online]` cho N database chi nhánh cùng template; không có CompanyDb. Form Company bắt buộc `CompanyCode`; form Branch nhập thông tin cơ bản, `DatabaseName` và `DatabasePassword`, trong đó password chỉ dùng để provision SQL Login và persist dưới dạng `SecretReference`. Đặc tả chi tiết bảng/cột/index của database tổng được ghi tại `SQLSERVER_TTSMART_COM_VN_SCHEMA_DESIGN.md`. DDL/database tổng đã được tạo theo quyền riêng nêu trên; không thay đổi runtime Đợt 1. Query plan/index usage dưới tải production vẫn `NOT VERIFIED`.

Checkpoint công cụ ngày 2026-08-14: `global.json` đã được cập nhật từ .NET SDK `10.0.302` lên `10.0.400` để khớp SDK đang cài đặt. `dotnet --version`, restore locked-mode và build đã chạy thành công; build có 0 warning, 0 error. Test có 331 pass và 1 integration test MongoDB không chạy được vì database local biệt lập không khả dụng; tín hiệu dynamic-skip hiện bị test runner ghi nhận là failure. Đây không phải lỗi phân giải SDK và chưa được tính là bằng chứng integration MongoDB đạt.

Đợt 1 vẫn đang thực hiện. Đủ 201 route đã đi tới xử lý substantive, nhưng bằng chứng MongoDB/provider/staging/E2E và cổng High `SEC-H-001` vẫn chưa hoàn tất. `SEC-H-003` đã được đóng sau khi nâng/pin Vitest và kiểm tra lại AD.

Kết quả so sánh khai báo mới được ghi trong [ROUTE_RECONCILIATION.md](ROUTE_RECONCILIATION.md); báo cáo 201 contract method-path legacy/V2 đã chuẩn hóa, 0 missing và 0 extra.

| Phạm vi | Kiểm kê legacy | Checkpoint V2 | Xác minh |
|---|---:|---|---|
| HTTP handler đã mount | 201 | 201 substantive, 0 explicit 501, 0 absent | 53 Contract test trong tổng 332 test backend; chưa phải một test cho mỗi route |
| Effective URL forms | 402 | 402 khai báo qua middleware không prefix + `/api` | ba case `/api` đại diện, chưa đầy đủ |
| MongoDB collections | 21 suy ra | 21 document mapping; repository bao phủ các slice đang hoạt động | integration MongoDB biệt lập có dữ liệu tổng hợp; các collection/ghi còn lại chưa phủ toàn diện |
| Mẫu route FE | 42 | giữ source JavaScript/JSX | 81 test, lint và production build pass |
| Mẫu route AD | 144 | giữ source JavaScript/JSX | chạy tuần tự có giới hạn: 205/205 test trên 25 file; build pass; lint exit 0 với 27 warning |
| Event order Socket.IO | 4 | đã triển khai Engine.IO v4/Socket.IO v5 và publisher bốn event | Unit decorator và Integration protocol đạt; chưa staging E2E với AD |
| Nhóm provider ngoài | Gemini, Gmail, Telegram, Zalo | adapter AI/voice, SMTP, Telegram, Zalo OAuth và notification order đã có | fake/isolated test đạt; chưa gọi provider thật hoặc staging |

## Checkpoint module

| Module | Legacy | Substantive | Explicit 501 | Absent | Hạn chế chính |
|---|---:|---:|---:|---:|---|
| Users | 30 | 30 | 0 | 0 | signup/recovery/autologin, target-role guard và audit đã có; chưa test SMTP/provider thật |
| Products | 35 | 35 | 0 | 0 | media, AI/voice, `adjusted` và station scope đã có; chưa provider/Mongo E2E rộng |
| Chips + chip types | 21 | 21 | 0 | 0 | media đã có; thiếu endpoint/Mongo fixture test dương tính |
| Cart | 6 | 6 | 0 | 0 | CAS/visibility và embedded `_id` đã có test chọn lọc; chưa positive Mongo endpoint rộng |
| Storefront/manage | 24 | 24 | 0 | 0 | policy đa ngôn ngữ/timestamp và media đã có test; cần review consistency xóa file/Mongo E2E |
| Sales orders | 20 | 20 | 0 | 0 | media, Socket.IO và notification Gmail/Telegram/Zalo đã có; chưa staging/provider E2E |
| Import orders | 17 | 17 | 0 | 0 | completion stock, aggregate và media đã có; cần Mongo/positive test |
| Export orders | 17 | 17 | 0 | 0 | completion stock, aggregate và media đã có; cần Mongo/positive test |
| Stations | 12 | 12 | 0 | 0 | search exact/AND, media, audit và projection allowlist đã có; còn cần staging/Mongo E2E |
| Storage history | 4 | 4 | 0 | 0 | giới hạn export an toàn 10.000 dòng; chưa có fixture test |
| Activity logs | 1 | 1 | 0 | 0 | đọc và mutation audit best-effort đã có; integration Mongo còn chọn lọc |
| Zalo | 4 | 4 | 0 | 0 | OAuth state một lần và order sender đã có; chưa provider thật |
| Telegram | 6 | 6 | 0 | 0 | adapter gửi thử và notification order đã có; chưa provider thật |
| Voice vocabulary | 4 | 4 | 0 | 0 | runtime cache và initialization service đã có; policy seed lúc cutover còn mở |
| **Tổng** | **201** | **201** | **0** | **0** | Số liệu là trạng thái route substantive, không phải tuyên bố tương đương |

## Nền tảng đã triển khai

- Solution modular-monolith ASP.NET Core 10 gồm project Domain, Application, Mongo Infrastructure và Api.
- Cookie JWT, reload identity Mongo trực tiếp, vô hiệu hóa sau đổi mật khẩu, tương thích bcrypt và policy permission động.
- Rewrite tương thích `/api`, JSON camel-case, correlation ID, mapping exception đã redaction, CORS allowlist, rate limit và health endpoint liveness/readiness.
- Mapping tường minh cho toàn bộ 21 collection suy ra, có giữ extra element.
- Slice repository/application/controller cho users, product/catalog, cart, storefront, orders, inventory orders, stations, histories, ActivityLog, provider settings và voice vocabulary.
- Toàn bộ 201 contract method/path legacy có xử lý substantive, gồm alias `/api`; không còn explicit 501 hoặc absent.
- AI/voice Products, Zalo OAuth, Socket.IO, notification đơn khách, ActivityLog mutation, runtime voice-vocabulary và product listing `adjusted`/`stationId` đã có code cùng test cô lập tương ứng.
- Projection legacy `_id` cho order/user/address/template/cart, policy storefront đa ngôn ngữ/timestamp, station search exact và public product pricing redaction đã được bổ sung; mutation user dùng field/array update Mongo để giảm lost-update giữa các concern.
- Product document giữ `_id` hợp lệ qua update; mutation address/order-template dùng compare-and-exchange có thử lại. Reverse proxy chỉ nhận forwarded headers từ IP/CIDR được cấu hình tin cậy và giới hạn số hop.

## Bằng chứng xác minh

Các lệnh backend đã chạy tại checkpoint code cuối này:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode --disable-build-servers
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore -m:1 --disable-build-servers
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore -m:1 --disable-build-servers --verbosity minimal
```

Kết quả:

- Build: pass, 0 warning, 0 error trên bốn project source và bốn project test.
- Test: 332/332 pass - Unit 231, Contract 53, Integration 16, Security 32.
- Project Integration bao phủ pipeline API, protocol Socket.IO và các luồng MongoDB biệt lập được chọn. Đây chưa phải bằng chứng MongoDB runtime rộng cho toàn bộ collection/ghi hoặc staging E2E.
- `git diff --check`: không báo lỗi, nhưng target chưa có commit và toàn bộ source còn untracked nên lệnh này không kiểm tra nội dung untracked. Kiểm tra trực tiếp 40 file Markdown xác nhận UTF-8 hợp lệ, không trailing whitespace, fence cân bằng, số cột bảng nhất quán và không có liên kết local hỏng.

Bằng chứng frontend target đã chạy:

- FE `npm ci`, 81 test/12 suite, lint và production build: pass.
- AD `npm ci` đã pass trước đó; production build được chạy lại tại checkpoint này và pass (chỉ có advisory chunk lớn).
- Test AD: 205/205 pass trên 25 file với Vitest ghim chính xác `3.2.6`, sau `npm ci`, bằng `npm test -- --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1`.
- Lint AD: exit 0 với 27 warning có sẵn và không có error.

Bằng chứng audit dependency:

- Audit lỗ hổng NuGet: zero finding.
- `npm audit` FE: zero finding.
- `npm audit --omit=dev` và audit toàn cây AD: 2 finding moderate trên `exceljs -> uuid`, 0 high/critical. `SEC-H-003` đã đóng; tồn dư được theo dõi tại `SEC-M-008`.
- Kiểm tra thủ công pattern secret/key/token và artifact loại trừ `node_modules`, `dist`, `bin`, `obj`, `.vs` không thấy credential hoặc dữ liệu production; các hit còn lại là placeholder/synthetic test. `gitleaks`, `trufflehog` và `detect-secrets` không khả dụng, nên đây không phải kết quả từ secret scanner chuyên dụng.

## Công việc đang chặn

1. Đóng hoặc chấp thuận `SEC-H-001` bằng kiểm chứng CSRF với topology deployment, reverse proxy và trình duyệt thật.
2. Mở rộng fixture/integration MongoDB biệt lập cho tương thích BSON, CAS, stock và compensation của các collection/route còn lại; không dùng database production.
3. Chạy test provider thật trong môi trường an toàn cho Gemini, SMTP, Telegram và Zalo OAuth/notification; không dùng secret hoặc recipient production.
4. Xác minh FE và AD E2E với API V2 cùng dependency biệt lập, gồm bốn event Socket.IO, station scope và luồng order notification.
5. Chạy smoke test staging, xác minh reverse proxy/static/upload/CSRF/realtime và hoàn tất runbook rollback/restore.

## Trạng thái repository và phạm vi

## Cập nhật migration MongoDB → Operational (2026-08-15, đang thực hiện)

Đây là checkpoint phát triển mapper trên `TTSmart_Operational_V1_Test`; chưa recreate database, chưa cutover runtime và chưa phải kết quả cuối cùng.

- Công cụ `TTSmartEcom.MongoSqlMigration` đã materialize và chạy lặp hai lần không trùng cho `stations` (5), `users` (16), `orders` (37), `iporders` (124), `eporders` (24), `storagehistories` (528), `manages` (1), `activitylogs` (383) và `chatmessages` (3).
- Kiểm tra SQL sau mapper Users/Station: 5 Stations, 188 StationProducts, 6 UserStations, 48 Permissions, 109 UserPermissions, 10 OrderTemplates, 27 OrderTemplateItems và 1 CartItem. Kiểm tra Sales: 37 SalesOrders, 52 SalesOrderItems, không có SalesOrderItem orphan trong snapshot hiện tại.
- Mapper nhập/xuất không cập nhật bảng `Stocks`; khi `quantity`, progress hoặc `stockAppliedQuantity` thiếu/không hợp lệ, raw quantity vẫn được lưu và dòng có `DataStatus=Incomplete`, không suy diễn applied quantity. Orphan được giữ bằng ObjectId legacy khi constraint cho phép.
- Activity log không sao chép `oldValue`/`newValue` nếu tên field nhạy cảm; chat được đưa vào `ArchivedChatMessages` với retention `Restricted`.
- Các translation storefront còn giữ canonical evidence cho tới khi contract đọc SQL đa ngôn ngữ được kiểm thử riêng; không làm thay đổi dữ liệu gốc hay tự đặt locale.

### Kết quả xác minh tiếp theo

- Đã bổ sung `VoiceSettings`/word/alias/code-map, metadata Telegram recipient/subscription, metadata Zalo không credential, `LegacyCounters` và `ChipSettings`.
- Fixture mapper tổng hợp đã chạy hai lần; mỗi lần tự map hai pass và kiểm tra singleton/idempotency cho Users, Station, Sales, Import, Export, History, Storefront, Activity, Chat, Voice, Telegram, Zalo và Counter.
- Sau khi recreate đúng database test, migrate toàn bộ Ecom hai lượt và `reconcile` đã chạy. Dry-run sau cùng: 1.503 document, mọi collection có document đều `standard`, `preserved=0`, `errors=0`; `autologintokens` rỗng.
- Đã chạy truy vấn SQL mô phỏng đọc API: catalog 316 join Product/Variant/Stock, Sales 52 line, Import 2.071 line, Export 277 line, StationProduct 188, StorefrontSectionProduct 27 và Voice 79 row join. Product URL/document metadata cũng được materialize vào `ProductFiles`/`ProductVariantFiles`; không copy binary hay provider credential.

- Legacy vẫn ở `TTSmartEcom_Deploy` tại `c836c8122e5d0e28628235b8e0f44c1c718efb91`; status vẫn có 58 entry và fingerprint `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Target là `main`, origin là `https://github.com/DungVu187/TTSmartEcomWeb_v2.git`, mọi file vẫn chưa commit/untracked tại checkpoint này.
- Visibility GitHub vẫn chưa xác minh vì không có `gh`.
- Không thực hiện commit, push, deploy, kết nối database/provider production, Entity Framework Core hoặc migration JavaScript-to-TypeScript. Có kết nối SQL Server local bằng Windows Authentication chỉ để tạo và xác minh `[ttsmart.com.vn]` theo quyền riêng Đợt 2; không sửa database nguồn.

## Cutover được cấp quyền riêng ngày 2026-08-17

- MongoDB `Ecom` được profile lại qua truy cập chỉ đọc: 19 collection và 1.503 document. Số lượng từng collection vẫn khớp snapshot ngày 2026-08-15, gồm 3 `chatmessages` cần trạng thái `OwnerExcluded` và `autologintokens` rỗng.
- Đích được xác nhận là SQL Server local `DESKTOP-5O6VV3J\SQLEXPRESS` (Express Edition), database `[TTSmart]` đang online. Preflight chỉ đọc xác nhận đây là prototype 54 bảng: tất cả bảng nghiệp vụ rỗng; chỉ có metadata `DatabaseInfo` và 9 hàng `SchemaVersions`.
- Đã tạo backup copy-only có checksum và đã đọc được metadata backup: `D:\TTSmartData\backups\TTSmart_pre_recreate_20260817_102202.bak`.
- Chưa recreate `[TTSmart]`, chưa chạy migrate vào `[TTSmart]`, chưa copy file và chưa chuyển runtime API. Lý do kỹ thuật: DDL hiện hữu materialize 54 bảng, không phải schema 30 bảng được cấp quyền; migration tool hiện tại hard-code/chặn target ngoài `TTSmart_Operational_V1_Test`; API vẫn composition trực tiếp `TTSmartEcom.Infrastructure.MongoDb`. Không sử dụng baseline test cũ làm bằng chứng cutover.
- Các lệnh build/test, DBCC và smoke API SQL sau cutover: **chưa xác minh**.

### Tiến độ materialize schema (tiếp tục ngày 2026-08-17)

- Đã chạy `000_RecreateTTSmart30.sql` trên instance local được cấp quyền. `[TTSmart]` hiện có đúng 30 bảng theo danh sách cutover; checksum SHA-256 của script đã được ghi trong `dbo.SchemaVersions`.
- `TTSmartEcom.MongoSqlMigration` đã chuyển allowlist catalog từ database test sang đúng `[TTSmart]` và build thành công. Mapper SQL vẫn đang được chuyển khỏi các cột/bảng prototype trước khi chạy migration dữ liệu thật; chưa được dùng để ghi MongoDB hay SQL đích.

### Migration dữ liệu lần 1 và lần 2 (2026-08-17)

- Runner đã chạy hai lần vào `[TTSmart]`, với MongoDB chỉ đọc. Mỗi lượt: 1.503 source = 1.500 mapped + 3 `chatmessages` `OwnerExcluded`; Blocked/Skipped/Error đều bằng 0.
- Lần hai không làm tăng `dbo.LegacyRecords` (1.500) hoặc `dbo.MigrationMappings` (1.503), nên mapping root/canonical evidence không sinh trùng.
- `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` và `DBCC CHECKDB ([TTSmart]) WITH PHYSICAL_ONLY` đã chạy, không báo lỗi consistency. Đối soát business subdocument, quantity/tồn, file checksum và runtime API SQL vẫn chưa xác minh; không được dùng các số liệu migration root này để tuyên bố cutover.

### Chuyển runtime SQL từng phần (2026-08-17, đang thực hiện)

- DI mặc định đã dùng SQL cho identity, đọc catalog, Cart, Station, Storefront, Voice vocabulary, Activity log và SalesOrder/Stock port. Các repository MongoDB tương ứng không còn được đăng ký trực tiếp cho những interface này.
- Đã phát hiện `dbo.CartItems` thiếu field legacy `status`. Migration `002_AddCartItemStatus.sql` có transaction, application lock, checksum truyền lúc chạy và đã áp dụng lặp an toàn trên `[TTSmart]`; không thêm bảng và không sửa document migration.
- SQL stock port thực hiện một danh sách điều chỉnh tồn trong cùng một SQL transaction, với kiểm tra số lượng không âm và public variant id khi caller cung cấp.
- Đã smoke API bằng URI MongoDB không khả dụng: `/health/live` và `/manages` trả HTTP 200. Endpoint station yêu cầu xác thực trả HTTP 401 như policy hiện tại.
- Đã chạy `dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore` sau các thay đổi runtime: 0 warning, 0 error. Chưa chạy test ghi cô lập cho Cart/SalesOrder/Stock và chưa chạy toàn bộ test/DBCC sau thay đổi này.
- InventoryOrder (Import/Export) và StorageHistory đã được thay bằng implementation SQL. Import/Export vẫn dùng `InventoryOrders.Direction`; `Quantity`, `ProgressQuantity` và `StockAppliedQuantity` tiếp tục là ba cột độc lập. Storage history mới ghi `StockOperations` cùng `StockMovementLines` trong transaction.
- User profile, address, order template, permission, bcrypt writer và superadmin mutation guard đã có implementation SQL. Address/template/permission/station ids được lưu trong các cột JSON của `Users`; mutation dùng `Users.Version` compare-and-swap.
- Telegram/Zalo settings và Zalo delivery credential repository đã có implementation SQL. `Integrations` chỉ lưu cấu hình và `SecretReference`; Telegram chat id, Zalo SecretKey, access token và refresh token được mã hóa trong kho secret local qua ASP.NET Data Protection.
- Catalog read/write cho Brand, Section/Category và chip cùng kiểm tra ảnh Section đã có implementation SQL.
- `LocalMediaFileService` giữ file vật lý ngoài SQL, sử dụng đường dẫn đã canonicalize/chặn traversal và sau khi copy thành công ghi `Files.StorageKey`, MIME, kích thước, URL nguồn cùng SHA-256. Không lưu BLOB.
- `SqlProductMutationRepository` hiện có mutation lõi Product/Variant, ProductType, đọc/tạo review và manual stock cơ bản. Chưa đăng ký thay adapter Mongo vì update/delete review, purchase count và media reference chưa đủ implementation/đối soát contract.
- Đã hoàn thiện phần SQL còn thiếu cho Product write/media reference và gỡ `AddMongoInfrastructure` cùng các registration repository MongoDB khỏi composition runtime mặc định. API khởi động với URI MongoDB không khả dụng; `/health/live` và `/products?limit=1&page=1` trả HTTP 200.
- Sau thay đổi runtime đã chạy `dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode`, build, test toàn solution: Unit 231/231, Contract 53/53, Integration 16/16, Security 32/32 (tổng 332/332). `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` và `DBCC CHECKDB ([TTSmart]) WITH PHYSICAL_ONLY` không báo consistency error.
- Đối soát SQL hiện tại: 30 bảng; manifest 1.503 source = 1.500 mapped + 3 OwnerExcluded, Blocked/Skipped/Error = 0; 2.349 `MigrationMappings` đều unique; tồn `9.765` sale và `9.826` storage; giữ 2 đơn bán, 2 nhập và 3 xuất trống.
- Chưa thể coi runtime đã đạt mục tiêu: chưa smoke toàn bộ mutation SQL, chưa có integration test ghi cô lập theo từng nhóm, và chưa đối soát/copy file migration cùng checksum vật lý.

### Hoàn tất đối soát file local tại checkpoint hiện tại (2026-08-17)

- Đã kiểm kê nguồn legacy chỉ đọc ở `be\upload\images`, `be\upload\documents` và `be\upload\invoices`: 312 file. Không ghi tên file hoặc nội dung file vào tài liệu/log.
- `TTSmartEcom.MongoSqlMigration` có các lệnh `migrate-files`, `verify-files`, `recover-files` và `prune-missing-file-metadata`. Lệnh copy chỉ nhận database đích `[TTSmart]`, canonicalize `StorageKey`, chặn reparse point/path traversal, copy theo file tạm, kiểm SHA-256 trước khi publish và upsert metadata `dbo.Files` cùng mapping `LegacyFileSystem`.
- Đã chạy `migrate-files` hai lần. Mỗi lần: 312 source, 312 mapped, 0 blocked, 0 skipped, 0 error. Lần hai không tạo mapping/file metadata trùng và không tăng `Version` khi metadata không đổi.
- Trong đối soát đầu tiên phát hiện một metadata cũ không có file vật lý. Record này không có `LegacyFileSystem` mapping; checksum không tồn tại tại storage cũ V2 hoặc ba bản copy legacy được cấp quyền. Đã xóa riêng metadata mồ côi theo lệnh chỉ xóa record không có mapping legacy và file thực sự thiếu; không xóa file nguồn hoặc file migrated.
- Đối soát cuối: 312 metadata, 312 file vật lý, 0 file thiếu, 0 lệch kích thước, 0 lệch SHA-256, 312 mapping legacy, 312 file trong manifest, 0 migration issue mở; `[TTSmart]` vẫn có đúng 30 bảng.
- `Uploads:RootPath` mặc định chuyển sang `D:\TTSmartData\files`; SQL chỉ lưu `StorageKey` tương đối, không lưu BLOB. Luồng xóa upload mới cũng xóa metadata SQL sau khi xử lý file vật lý.
- Runtime API không còn import type MongoDB trong Api/Application: exception middleware nay bắt `DbException` chung nhưng giữ mã lỗi legacy `TTS-MONGO-0001` để không thay đổi contract lỗi quan sát được.
- Build riêng migration runner và API solution đã chạy sau thay đổi file/runtime. Toàn bộ test, smoke mutation có xác thực và DBCC sau checkpoint file này vẫn cần chạy lại trước khi coi là bằng chứng end-to-end.

### Xác minh lại sau checkpoint file/runtime (2026-08-17)

- Đã chạy `dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode`, `dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore` và `dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore`. Kết quả: build 0 warning/0 error; Unit 231/231, Contract 53/53, Integration 17/17 và Security 32/32 (tổng 333/333).
- Integration test mới `SqlFileMetadataIntegrationTests` đã chạy với database có tiền tố `TTSmartEcomV2FilesIntegration_`; test tạo schema `Files` tối thiểu, xác minh upsert/update/delete metadata và dọn database trong `finally`. Không sử dụng `[TTSmart]` làm database test.
- Đã chạy `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` và `DBCC CHECKDB ([TTSmart]) WITH PHYSICAL_ONLY`; không có lỗi SQL.
- Smoke API với `MongoDb__ConnectionString` cố ý trỏ endpoint không khả dụng: `/health/live` trả 200, `/products?limit=1&page=1` trả 200 và `/stations?page=1&limit=1` không xác thực trả 401 đúng policy. Tiến trình smoke đã dừng sau kiểm tra.
- Chưa có bằng chứng cho toàn bộ mutation SQL có cookie JWT, tất cả alias `/api`, transaction tồn kho cạnh tranh hoặc toàn bộ nhóm repository SQL. Các phần này vẫn là công việc tiếp theo, không suy rộng từ smoke đọc hiện tại.

### Cô lập test upload và xác minh lặp cuối checkpoint (2026-08-17)

- `Uploads:RecordMetadata` mặc định là `true` trong runtime. Các `WebApplicationFactory` contract dùng upload root tạm đặt rõ `false`, nên fixture upload không thể để lại metadata trong `[TTSmart]`; đối soát sau full suite vẫn là 312 metadata/312 file vật lý/312 mapping/0 issue mở.
- Test Mongo integration kiểm tra Mongo bằng `ping` ngay ở discovery. Khi Mongo local có mặt, test tiếp tục dùng database có tiền tố test; khi không có, runner nhận trạng thái skip thay vì phụ thuộc runtime SQL vào Mongo.
- Lượt test cuối: Unit 231/231, Contract 53/53, Integration 17/17, Security 32/32 (333/333). Không có dữ liệu test file trong `[TTSmart]` sau lượt này.

### Bổ sung integration Cart/Order/Stock SQL (2026-08-17)

- `SqlCartIntegrationTests` dùng database có tiền tố `TTSmartEcomV2CartIntegration_`: xác minh add/update/status/clear cart và rollback transaction khi replacement vi phạm unique key. Test đã pass và database test được dọn.
- `SqlOrderStockIntegrationTests` dùng database có tiền tố `TTSmartEcomV2OrdersIntegration_`: xác minh đơn rỗng đọc được rồi ghi thêm dòng, điều chỉnh tồn thành công, và rollback toàn bộ batch khi dòng sau lỗi. Test đã pass và database test được dọn.
- Test stock phát hiện `PurchaseCountDelta` trước đó chưa được materialize. `SqlOrderStockPort` nay ghi `purchaseCount` vào `ProductVariants.DetailsJson` bằng parameter `decimal(19,6)` tường minh, đồng thời vẫn giữ cập nhật quantity trong transaction. Test xác minh cả giá trị purchase count và rollback.
- Sau hai test mới, full suite đã chạy: Unit 231/231, Contract 53/53, Integration 19/19, Security 32/32 (335/335).
- Chưa có coverage integration cô lập tương đương cho toàn bộ mutation Profile, InventoryOrder, Station, Storefront, Voice và Integrations; các phạm vi đó vẫn chưa được coi là đã chứng minh end-to-end.

### Bổ sung integration Import/stock history SQL (2026-08-17)

- `SqlInventoryOrderIntegrationTests` dùng database có tiền tố `TTSmartEcomV2InventoryIntegration_`: tạo phiếu nhập rỗng, thêm dòng, hoàn tất dòng, kiểm tra tồn storage tăng và `StockOperations`/`StockMovementLines` được ghi. Test đã pass và database test được dọn.
- Test phát hiện parser SQL của `InventoryOrderItem.DetailsJson` lỗi với `profitPercent: null`. `SqlInventoryOrderRepository` nay chỉ parse `profitPercent` khi JSON value là Number; `null` được bảo toàn như `null` thay vì làm lỗi đọc phiếu.
- Full suite sau bổ sung Import: Unit 231/231, Contract 53/53, Integration 20/20, Security 32/32 (336/336).
- `SqlInventoryOrderIntegrationTests` đã mở rộng case Export: phiếu xuất rỗng, thêm/hoàn tất dòng, tồn bán và tồn kho cùng giảm, đồng thời ghi một stock operation/movement. Hai case Import/Export chạy trên database cô lập và pass.
- `SqlUserProfileIntegrationTests` dùng database có tiền tố `TTSmartEcomV2ProfileIntegration_`: cập nhật profile, thêm/chuyển/xóa địa chỉ mặc định và CRUD template đơn hàng; JSON fields được đọc lại đúng sau mutation SQL và database test được dọn.
- `SqlStorefrontIntegrationTests` dùng database có tiền tố `TTSmartEcomV2StorefrontIntegration_`: upsert singleton JSON, cập nhật section và xóa ảnh; cấu hình được đọc lại đúng và database test được dọn.
- `SqlStationIntegrationTests` dùng database có tiền tố `TTSmartEcomV2StationIntegration_`: create/update image/update product assignments/delete Station; kiểm tra `StationProducts` được ghi/xóa cùng Station trong SQL và database test được dọn.
- `SqlVoiceIntegrationTests` dùng database có tiền tố `TTSmartEcomV2VoiceIntegration_`: seed/read/update Voice singleton và xác minh stale compare-and-swap bị từ chối. Test phát hiện stale writer có thể tạo singleton thứ hai; `SqlVoiceVocabularyRepository` nay chỉ insert khi bảng thật sự rỗng với `UPDLOCK,HOLDLOCK`.
- `SqlIntegrationSettingsTests` dùng database có tiền tố `TTSmartEcomV2IntegrationSettings_` và secret store giả: Telegram/Zalo metadata được ghi/read SQL, trong khi JSON SQL không chứa plaintext chat id hoặc Zalo secret; database test được dọn.
- Full suite sau case Integrations: Unit 231/231, Contract 53/53, Integration 26/26, Security 32/32 (342/342). Đối soát file vẫn 312 metadata/312 file/checksum đúng/0 issue; DBCC constraint và physical-only không lỗi.
- Đã smoke loopback với Mongo URI không khả dụng và user test có cleanup: register 201, login cookie JWT 200, `PUT /users/profile` 200 và `POST /users/profile/addresses` 201. Mutation cookie không có Origin bị CSRF middleware chặn 403 (`TTS-CSRF-0001`); cùng request với Origin từ allowlist `http://localhost:5173` thành công. Không còn user smoke sau cleanup SQL.
- Chưa có smoke mutation JWT cho toàn bộ endpoint nghiệp vụ; trạng thái này không phải bằng chứng end-to-end cho toàn bộ endpoint.

### Sửa validation positional record và smoke Cart/API (2026-08-17)

- Smoke JWT phát hiện `POST /api/products/create` từng từ chối Product AI chỉ vì thiếu `Type`, `Brand`, `Section`, `Value` hoặc `Warranty`. `ProductCatalogWriteService` nay chỉ yêu cầu `Name` khi tạo; các metadata phân loại/bảo hành vẫn nullable. Unit regression xác minh Product thiếu các field này được normalize hợp lệ.
- Smoke tiếp theo phát hiện `CartChangeRequest` gây HTTP 500 trước service do DataAnnotation của positional record đặt vào generated property. Đã chuyển annotation validation của Cart, Order và Inventory DTO sang constructor parameter; các annotation JSON vẫn giữ ở property để bảo toàn JSON contract.
- Smoke loopback bằng Mongo URI không khả dụng đã xác minh với fixture synthetic và dọn trong `finally`: register `201`, login qua `/api` `200`, tạo Product thiếu metadata phân loại `201`, Cart add `200`, Cart update qua `/api` `200`, Cart clear `200`. Đối soát sau dọn: không còn User/Product/Cart/ActivityLog synthetic.
- Hai test Mongo legacy dùng cùng endpoint loopback `127.0.0.1` với probe discovery, tránh sai khác IPv4/`localhost` làm test chạy sau khi discovery không thể kết nối.
- Xác minh sau sửa: build solution `0 warning/0 error`; Unit `232/232`, Contract `53/53`, Integration `26/26`, Security `32/32` (tổng `343/343`). Vẫn chưa có smoke mutation JWT đầy đủ cho SalesOrder, Import/Export, Station, Storefront, Voice, Telegram/Zalo và API file; các phạm vi này tiếp tục cần kiểm chứng.

### Sửa stock port và smoke SalesOrder (2026-08-17)

- Smoke SalesOrder phát hiện `POST /orders/{id}/items` trả `409` giả. Nguyên nhân là câu `UPDATE` trong `SqlOrderStockPort` join `Products` nhưng tham chiếu `DetailsJson` không định danh, dẫn tới SQL Server báo cột mơ hồ; adapter đã bọc SQL exception thành lỗi tồn kho/concurrency.
- Đã định danh rõ `v.DetailsJson` trong phép cập nhật tồn kho và purchase count. `SqlOrderStockIntegrationTests` pass lại.
- Smoke JWT sau sửa pass: tạo draft qua `/api` `201`, thêm dòng qua route không tiền tố `200`, xóa qua `/api` `200`; fixture Product/User/Order/ActivityLog synthetic được dọn và đối soát không còn dòng.

### Smoke Import/Export/Station sau sửa NULL (2026-08-17)

- `SqlInventoryOrderRepository.LinesAsync` đã sửa mapping `Price`/`Vat` nullable thành `DBNull.Value`; regression Import với `VAT = null` pass trên database SQL cô lập.
- Smoke JWT với Mongo URI không khả dụng pass: Import tạo qua `/api`, thêm dòng không prefix, xóa qua `/api`; Export tạo không prefix, thêm dòng qua `/api`, xóa không prefix; Station tạo qua `/api`, gán Product không prefix, xóa qua `/api`. Mỗi fixture dùng marker riêng và đối soát sau cleanup không còn User/Product/InventoryOrder/Station/ActivityLog synthetic.

### Smoke Voice/Telegram (2026-08-17)

- Smoke JWT xác minh Voice thêm không prefix/xóa qua `/api`, và Telegram thêm recipient qua `/api`/xóa không prefix. Mongo URI vẫn cố ý không khả dụng.
- Harness ban đầu đọc `id` thay vì field contract `_id` của Telegram recipient; đã xóa riêng recipient synthetic bị sót, sau đó rerun pass và không còn dữ liệu fixture.

### Smoke file local qua HTTP (2026-08-17)

- API loopback được khởi động với URI MongoDB cố ý không khả dụng. Fixture người dùng synthetic được nâng quyền `admin` chỉ trong thời gian smoke và được xóa trong `finally`.
- `POST /api/orders/upload-image` nhận PNG hợp lệ, trả `200` và `imageUrl`; `GET` cùng URL dưới `/invoice-images` với cookie JWT trả `200` và đúng độ dài nội dung; `DELETE /orders/delete-image?imageUrl=...` trả `200`.
- Sau xóa, metadata SQL theo `Files.StorageKey` không còn và file vật lý synthetic cũng được dọn. Đối soát sau cả lần harness đầu bị thiếu assembly HTTP lẫn lần chạy đạt: không còn User marker hoặc File invoice synthetic.
- Đây là bằng chứng end-to-end cho upload/đọc protected/xóa file không cần Mongo runtime.

### Smoke Storefront và Zalo có hồi phục singleton (2026-08-17)

- API loopback tiếp tục chạy với URI MongoDB cố ý không khả dụng. Harness dùng actor `admin` synthetic, cookie JWT và Origin hợp lệ; actor cùng mọi ActivityLog do actor tạo được xóa trong `finally`.
- `GET /api/manages` và `PUT /manages/update-introduction` trả `200`. Mutation Storefront mang marker synthetic được quan sát trực tiếp trong `StorefrontSettings.ConfigurationJson` và `Version` tăng đúng một đơn vị, chứng minh HTTP write đi vào SQL.
- `GET /zalo/settings` và `POST /api/zalo/settings` với payload chỉ gồm giá trị `null` trả `200`. Không truyền SecretKey, vì vậy không đọc, trả về hoặc thay thế secret. `Integrations.Version` tăng đúng một đơn vị, chứng minh write path SQL của endpoint.
- Trước mutation, harness giữ raw `ConfigurationJson`, `SecretReference`, `Version` và timestamp trong bộ nhớ. Sau assertion, các bản ghi được khôi phục bằng SQL parameterized về đúng snapshot, không in nội dung cấu hình/secret; marker không còn trong Storefront, không còn User/ActivityLog synthetic và không có listener loopback bị bỏ lại.
- Sau các smoke này đã chạy lại `dotnet restore --locked-mode`, build solution (0 warning, 0 error), toàn bộ test (Unit 232, Contract 53, Integration 26, Security 32; tổng 343), `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS`, `DBCC CHECKDB ([TTSmart]) WITH PHYSICAL_ONLY` và `git diff --check`: đều pass. Đối soát hiện tại vẫn là đúng 30 bảng và không còn fixture smoke User/File.
- Khi rà soát composition lần cuối, API còn một `ProjectReference` MongoDB không được source hay DI sử dụng. Tham chiếu này đã được gỡ khỏi `TTSmartEcom.Api`; các Mongo integration test nhận `Infrastructure.MongoDb` bằng tham chiếu trực tiếp của chính project test, nên adapter Mongo vẫn tồn tại cho đối chứng/rollback mà API runtime không còn phụ thuộc build-time. Sau thay đổi này, restore locked-mode, build và toàn bộ 343 test đều pass; API build mới khởi động, `/health/live` và `/api/products` trả `200` với URI MongoDB cố ý không khả dụng. Tìm source/API DI không còn `MongoClient`, `IMongo`, `Infrastructure.MongoDb`, `AddMongoInfrastructure` hoặc Mongo repository registration.
- Đối soát migration hiện tại bằng manifest chỉ thuộc `MigrationRuns.SourceSystem=MongoDB`: `1.503 = 1.500 mapped + 3 OwnerExcluded`, Blocked/Skipped/Error đều 0; fingerprint mapping không trùng, còn 2 SalesOrder và 5 InventoryOrder trống, tổng tồn Variant là sale `9.765` và storage `9.826`. `verify-files` hiện loại `dbo.sysdiagrams` (bảng SQL Server Designer không thuộc schema runtime) khỏi số đếm; kết quả mới nhất là 30 bảng nghiệp vụ, 312 metadata/312 file vật lý, không thiếu/sai byte/sai SHA-256, 312 mapping, 0 issue.

### Recreate `[TTSmart]` và migration lặp có kiểm chứng (2026-08-17)

- Ngay trước recreate đã tạo backup copy-only có checksum tại `D:\TTSmartData\backups\TTSmart_pre_recreate_20260817_163431.bak`; `RESTORE VERIFYONLY` trả thành công.
- Runner `schema-recreate` dựng lại `[TTSmart]` từ baseline và kiểm tra SHA-256 của baseline cùng migration 010, 011, 012. Migration lần một đọc đúng 1.503 document Mongo: 1.500 `Mapped`, 3 `OwnerExcluded`, `Blocked`/`Skipped`/`Error` đều bằng 0.
- File migration dùng root local đã được cấp phép: 329 metadata, 329 file vật lý, 329 mapping; không thiếu file, không sai byte hoặc SHA-256. Gắn owner trong migration tool cho 243 file (Category, Station, ProductVariant, InventoryOrder), còn 86 `Unlinked`.
- Sau khi materialize `ProductTypes.SourceUpdatedAtUtc` từ `types.updatedAt`, đã chụp fingerprint count/checksum/rowversion của 23 bảng nghiệp vụ và file, chạy lại migration + file + owner, rồi so sánh: mọi giá trị fingerprint và `MaxRowVersion` giữ nguyên. Chỉ MigrationRuns/audit được cập nhật.
- `reconcile` sau vòng lặp báo 1.503 document nguồn, MappingNull/MappingBroken/MappingDuplicate/OpenIssues/FailedRuns/PlaintextIntegrationSecret/FileChecksumMissing đều bằng 0. `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` và `DBCC CHECKDB([TTSmart]) WITH PHYSICAL_ONLY` không trả lỗi.
- Build solution: 0 warning/0 error. Unit 232/232, Contract 53/53, Security 32/32 pass. Integration SQL với database cô lập được cấu hình riêng: 26/26 pass. Còn cần mở rộng reconcile field-level trước khi kết luận mọi điều kiện Definition of Done của migration đã được chứng minh.
- Audit secret không đọc nội dung: local secret root có 3 file mã hóa; một reference SQL có file tương ứng, không có reference thiếu; hai file không được SQL hiện tại tham chiếu. Hai file dư chưa bị xóa vì chưa có bằng chứng chúng chỉ là fixture/test cũ.
- `reconcile` hiện đọc lại Mongo để so `PublicId`, `Version` của các document gốc và timestamp `types`, Product, Sales, Import, Export, StockHistory với SQL `datetime2(7)`: RootPublicIdMismatch/RootVersionMismatch/TimestampMismatch đều bằng 0.
- Evidence field-level: với 1.500 document được migrate, SHA-256 của Canonical Extended JSON sau redaction được so lại giữa Mongo và `LegacyRecords`; CanonicalEvidenceMismatch và FieldMismatch đều bằng 0. Ba `chatmessages` `OwnerExcluded` không được đọc hay lưu nội dung.

### Backfill dữ liệu Mongo theo field (2026-08-17)

- Backup copy-only có checksum đã được tạo và `RESTORE VERIFYONLY` trước DML. User backfill: 2 address, 1 CartItem, 6 UserStations, 9 AutoLoginTokenHash và 1 PasswordChangedAtUtc; Station backfill: 5 Name/Code và 188 StationProducts đều có ProductId.
- Product backfill: 316 Product/316 Variant có timestamp nguồn; PurchaseCount tổng 6; VAT giữ raw và số chuẩn hóa. Sales/Inventory/StockHistory: 52 Sales line FK, 2 SalesOrder trống, Import 2.054 FK + 17 orphan, Export 276 FK + 1 orphan, Export ProgressQuantity 115, StockHistory 472 FK + 56 orphan và 528 cặp timestamp nguồn.
- File: 329 metadata/physical/mapping, checksum đúng, 0 issue. Telegram giữ chatSecretReference vào local protected secret store, không còn `chatId` trong JSON Integration; evidence User/Telegram được refresh redaction. Ba OwnerExcluded chat mapping nay trỏ evidence LegacyRecords, nên không còn TargetId rỗng.
- Build 0 warning/error, Unit 232, Contract 53, Integration 26, Security 32 pass; DBCC và API `/health/live`, `/api/products` với Mongo URI bất khả dụng pass. Vòng backfill thứ hai giữ nguyên số Users, CartItems, UserStations, StationProducts, SalesOrderItems, InventoryOrderItems, StockMovementLines, Files, MigrationMappings, PurchaseCount và ExportProgress; không tạo dòng/mapping/file trùng hoặc thay đổi metric nghiệp vụ. `Version` là metadata concurrency và không được dùng làm metric business migration.
- Audit mapping cuối đã sửa 846 `LegacyRecords` mapping từng trỏ GUID mapper thay vì `LegacyRecordId` thật. Hiện không còn TargetId rỗng hoặc TargetId đứt trên các bảng mapping, không còn issue mở, JSON Telegram không có `chatId`/plaintext, và API Product trả các property legacy (`infoDoc`, documents, purchaseCount, warranty, solution, features, operatingMethod, advantages, specifications, timestamp cùng field Variant) khi Mongo URI bất khả dụng.
