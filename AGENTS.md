# AGENTS.md

## Mục đích

Repository này là bản thay thế được xây dựng độc lập cho `D:\TTSmartEcomWeb`. Quá trình migration gồm ba đợt riêng biệt:

1. Đợt 1: Chuyển Node.js/Express sang ASP.NET Core 10, tiếp tục sử dụng MongoDB và các frontend JavaScript/JSX.
2. Đợt 2: Chuyển MongoDB sang SQL Server trong khi vẫn giữ nguyên API contract của ASP.NET.
3. Đợt 3: Chuyển JavaScript/JSX sang TypeScript/TSX trong khi vẫn sử dụng ASP.NET Core và SQL Server.

Hiện tại Đợt 1 được giữ làm baseline hành vi và Đợt 2 là phạm vi đang thực hiện. Đợt 2 bao gồm:

- chuyển persistence runtime từ MongoDB sang SQL Server nhưng giữ nguyên API contract ASP.NET Core hiện hữu;
- thiết kế và triển khai schema ControlPlane cùng schema Operational dùng lại;
- xây công cụ profile, dry-run, migration, đối soát và cutover MongoDB sang SQL Server;
- bổ sung chức năng quản lý đa công ty, chi nhánh, database registry, provisioning, feature/quota và phân quyền theo Company/Branch;
- bổ sung `TTSmartEcom.Infrastructure.SqlServer`, Entity Framework Core hoặc DDL/migration SQL có version khi phù hợp với kiến trúc được duyệt;
- triển khai metadata file local/cloud theo storage abstraction, trước mắt ưu tiên lưu file local ngoài SQL Server.

Đợt 2 không mặc nhiên cho phép chuyển JavaScript/JSX sang TypeScript/TSX, microservices, queue, Redis, subscription billing, đồng bộ local-cloud, dual-write MongoDB/SQL Server kéo dài hoặc chức năng nghiệp vụ không liên quan. Các hạng mục đó cần chỉ dẫn riêng, rõ ràng. Không được thay runtime sang SQL Server hoặc cutover dữ liệu thật chỉ vì schema đã được materialize trên database test.

## Ranh giới repository

- Nguồn legacy: `D:\TTSmartEcomWeb` — luôn luôn chỉ đọc.
- Đích V2: `D:\TTSmartEcomWeb_v2` — mọi chỉnh sửa chỉ được thực hiện tại đây.
- Không sao chép `.env`, thông tin xác thực, dữ liệu production, upload, dump, log, backup, `node_modules`, `dist` hoặc output `build`.
- Không commit, push, deploy, thay đổi chế độ hiển thị của GitHub hoặc kết nối tới dịch vụ production nếu không có chỉ dẫn riêng, rõ ràng.
- Bảo toàn các thay đổi không liên quan trong cả hai repository. Không bao giờ dùng lệnh Git có tính phá hủy để khôi phục công việc của người khác.

Trước và sau khi khảo sát legacy, chỉ đọc và ghi nhận kết quả của `git branch --show-current`, `git rev-parse HEAD` và `git status --short`. Ghi kết quả vào `docs/migration/LEGACY_BASELINE.md` mà không thay đổi worktree legacy.

## Nguồn sự thật của Đợt 2

Source code legacy và implementation ASP.NET Core đã được xác minh trong Đợt 1 là nguồn có thẩm quyền về hành vi nghiệp vụ, route, hình dạng request/response, authentication, authorization, file, dịch vụ ngoài và giả định của frontend. MongoDB legacy cùng bản sao/profile dữ liệu được phê duyệt là nguồn có thẩm quyền về hình dạng BSON và dữ liệu cần bảo toàn. SQL Server là đích của Đợt 2, không được dùng chính schema SQL dự kiến để suy ngược rằng dữ liệu nguồn không tồn tại.

Kiểm kê đã đối chiếu hiện tại:

- 201 HTTP handler đã được mount.
- Cả URL không có tiền tố và URL có tiền tố `/api` đều có hiệu lực, tạo thành 402 dạng method/URL.
- 21 collection MongoDB được suy luận.
- 42 mẫu API route của frontend khách hàng và 144 mẫu của frontend quản trị.
- Authentication bằng cookie JWT sử dụng `authToken`.
- Bốn sự kiện đơn hàng Socket.IO.

Không được tuyên bố tương đương chỉ dựa trên các tổng số này. Mỗi field MongoDB quan sát được phải có trạng thái `Mapped`, `Archived`, `SecretStore`, `Empty` hoặc `Blocked`; mỗi endpoint phải được xác minh lại với persistence SQL Server trước khi được coi là đạt Đợt 2.

## Kiến trúc và chiều dependency

Đợt 2 tiếp tục là một modular monolith. Kiến trúc đích của các project trong `backend/src` là:

```text
TTSmartEcom.Api -> TTSmartEcom.Application -> TTSmartEcom.Domain
        |                    ^
        +-> TTSmartEcom.Infrastructure.SqlServer
        |                     |
        |                     +-> Application, Domain
        |
        +-> TTSmartEcom.Infrastructure.MongoDb
                              |
                              +-> Application, Domain
```

- Domain chứa các khái niệm nghiệp vụ và không có dependency vào MongoDB, SQL Server, Entity Framework Core hoặc ASP.NET.
- Application chứa các use case, port, validation và interface chính sách authorization.
- Infrastructure.SqlServer sở hữu EF Core/SQL client, mapping relational, transaction, concurrency, index và implementation persistence đích.
- Infrastructure.MongoDb tiếp tục sở hữu khả năng tương thích BSON và implementation nguồn trong thời gian migration/rollback; không được xóa trước khi có bằng chứng cutover và rollback được duyệt.
- Api sở hữu khả năng tương thích HTTP, cookie, middleware authentication, serialization, ranh giới static/file, xử lý exception, logging và composition.
- Controller phải giữ mỏng. Api không được truy cập MongoDB/SQL Server trực tiếp; Domain/Application không được chứa type Mongo, EF Core hoặc SQL client.
- Không triển khai dual-write âm thầm. Nếu cần shadow-read, comparison hoặc dual-write tạm thời, phải có thiết kế idempotency, quan sát sai lệch và rollback được phê duyệt riêng.

Authentication, authorization, xử lý exception, logging, configuration, contract, `Program.cs`, file project và phiên bản package trung tâm dùng chung cần có quyền sở hữu được điều phối; tránh chỉnh sửa đồng thời.

## Quy tắc tương thích API

- Giữ nguyên method, kiểu chữ hoa/thường, path, tên query, tên trường multipart, tên property JSON, response envelope, status code và cả hai dạng có/không có tiền tố `/api` của legacy.
- Không thêm tiền tố phiên bản hoặc âm thầm chuẩn hóa route.
- Giữ nguyên tên cookie `authToken`, khả năng tương thích JWT claim, hành vi phiên 12 giờ, xác minh bcrypt legacy và lộ trình nâng cấp autologin legacy có giới hạn.
- Thực thi authorization cấp đối tượng và permission rõ ràng. `ADMIN_FULL_ACCESS=true` của legacy là một quyết định tương thích/bảo mật đã được ghi nhận, không phải sự cho phép âm thầm làm yếu code mới.
- Các bản sửa secure-by-default làm thay đổi hành vi quan sát được phải được ghi vào `docs/security/SECURITY_FINDINGS.md` và `docs/migration/OPEN_QUESTIONS.md`.
- Endpoint mới cho Company, Branch, database registry, provisioning, feature/quota hoặc quản trị đa công ty được phép trong Đợt 2 nhưng phải được ghi rõ là contract mới, cập nhật ma trận API/access và không làm thay đổi ngầm endpoint legacy.
- Company/Branch scope phải lấy từ identity và authorization context đáng tin cậy; không tin `CompanyId`/`BranchId` do client gửi nếu chưa kiểm tra quyền cấp đối tượng.

## Quy tắc tương thích dữ liệu

- MongoDB là nguồn migration chỉ đọc cho Đợt 2; SQL Server là persistence đích. Không sửa dữ liệu MongoDB nguồn để làm cho migration dễ hơn.
- Coi giá trị bị thiếu, null, giá trị legacy hỗn hợp, ObjectId, ngày tháng, mảng nhúng, giá trị mặc định, giá dạng chuỗi, orphan và extra field là các vấn đề tương thích phải được mapping rõ.
- Không chạy migration, seed, script sửa dữ liệu hoặc probe database production. Profile/dry-run dữ liệu thật chỉ được chạy trên bản sao được phê duyệt, bằng principal chỉ đọc và không xuất PII/secret vào log hoặc tài liệu.
- Mọi mapping MongoDB sang SQL Server phải ở mức field, bao gồm source collection/ObjectId/path, target table/column, phép chuyển đổi, null/default, orphan handling và tiêu chí đối soát.
- GUID là khóa nội bộ SQL. Entity còn xuất hiện qua API contract ObjectId dùng `PublicId char(24)` lowercase-hex; dữ liệu migrate giữ ObjectId nguồn, dữ liệu tạo mới phải sinh PublicId tương thích.
- `Version = 0` phải hợp lệ cho dữ liệu MongoDB; `rowversion` là concurrency token SQL riêng, không thay cho `Version` API/legacy.
- Tiền dùng `decimal(19,4)` và quantity/progress cần số lẻ dùng `decimal(19,6)` trừ khi contract đã chứng minh kiểu khác. Không dùng `money`, `smallmoney`, `float`, `real`, `text`, `ntext` hoặc `image`.
- Không bịa dữ liệu lịch sử: giá, snapshot, timestamp hoặc quan hệ bị thiếu ở nguồn phải giữ `NULL` cùng migration issue/evidence phù hợp.
- Migration phải idempotent, có `MigrationRuns`, `MigrationMappings.SourcePath`, issue tracking, source/file manifest và đối soát count/tổng/checksum.
- Migration schema phải version hóa, có SHA-256 checksum, transaction, `XACT_ABORT`, application lock và phát hiện drift. Script reusable kết nối trực tiếp database đích, không hardcode `USE` database prototype.
- Không có foreign key hoặc transaction nghiệp vụ xuyên database. Logical reference giữa ControlPlane và Operational do application kiểm tra.
- File PDF/ảnh không lưu BLOB trong SQL Server. SQL chỉ giữ metadata, checksum và storage key tương đối; storage root nằm trong cấu hình máy/VPS. Phải canonicalize và xác nhận đường dẫn cuối vẫn nằm dưới root.
- Chưa phát triển đồng bộ local-cloud. Database Operational cài local là authoritative độc lập cho dữ liệu của nó; không tạo bảng/công tắc sync nếu chưa có thiết kế được duyệt.

## Kiến trúc database và mô hình đa công ty

Đợt 2 dùng ba nhóm tên database vật lý nhưng chỉ hai họ schema:

```text
ControlPlane
└── [ttsmart.com.vn]

Operational
├── [TTSmart]
└── [{ChiNhanh}_online]
```

- `[ttsmart.com.vn]` quản lý Company, Branch, identity/quyền control-plane, feature/quota/AI, database server/template/release/registry, provisioning và audit hệ thống. Không đặt Product, Customer, Cart, chứng từ, Stock hoặc metadata file nghiệp vụ tại đây.
- `[TTSmart]` là database bán hàng đầy đủ của TTSmart.
- Mỗi `[{ChiNhanh}_online]` là database Operational độc lập theo cùng một schema/version cố định với `[TTSmart]`; không cho phép schema drift tùy chi nhánh.
- Mỗi database Operational tự chứa local identity, catalog, customer/cart/template, sales, nhập/xuất/tồn, file metadata, Station/storefront, voice/integration và audit để có thể chạy trong LAN khi không kết nối ControlPlane.
- Mỗi chi nhánh có catalog, tồn kho và chứng từ riêng; không có Product master bắt buộc dùng chung và không yêu cầu thay đổi giữa các chi nhánh xuất hiện tức thời.
- Không tạo `CompanyDb` trung gian và không gom dữ liệu nhiều công ty/chi nhánh vào một Operational database dùng chung bằng `TenantId`, trừ khi owner thay đổi kiến trúc bằng quyết định riêng.
- Form tạo Company bắt buộc `CompanyCode`. Form tạo Branch giữ thông tin cơ bản và có `DatabaseName`, `DatabasePassword`; password chỉ tồn tại trong luồng provisioning/secret manager, không lưu plaintext trong ControlPlane, log, config hoặc audit.
- Tên database/login phải được allowlist và quote an toàn. Database/login unique trong phạm vi `DatabaseServer`; database branch phải có prefix không rỗng và hậu tố `_online`.
- Provisioning phải idempotent, một active operation trên mỗi database đích, có lease token, retry, release/template checksum và kiểm tra stale worker.
- Authorization phải chặn role, feature, audit và database assignment xuyên Company/Branch. Feature Branch không được vượt entitlement Company.
- Không dùng application account có quyền `sysadmin`/`db_owner` thường trực. Quyền tạo database/login thuộc worker provisioning tách biệt và tối thiểu cần thiết.

## Baseline bảo mật

- Không bao giờ ghi log hoặc trả về secret, token, giá trị OTP, thông tin xác thực, connection string, password hash, dữ liệu khách hàng đã upload hoặc payload provider chứa dữ liệu nhạy cảm.
- Xác thực DTO theo allowlist, ObjectId/PublicId, GUID nội bộ, Company/Branch scope, tên database/login, pagination, trường sort, khoảng ngày, input regex, kích thước collection, storage key, tên file, MIME type, extension, kích thước và chữ ký file.
- Mutation được authentication bằng cookie cần chiến lược CSRF rõ ràng. CORS không phải cơ chế bảo vệ CSRF.
- Giữ các tích hợp provider sau interface có timeout, cancellation, giới hạn kích thước response, redaction và ánh xạ lỗi xác định.
- SQL luôn parameter hóa value. Dynamic identifier chỉ được tạo từ allowlist và `QUOTENAME`; không ghép trực tiếp database name/login/password do client gửi.
- Password ứng dụng chỉ lưu adaptive hash; reset/autologin token chỉ lưu hash. SQL Login password và provider secret chỉ lưu qua secret reference/secret manager.
- Không báo cáo hệ thống là an toàn hoặc sẵn sàng cutover khi vẫn còn finding mức High.

## Test và xác minh

Các project test được tách thành bộ Unit, Contract, Integration và Security. Test placeholder không được tính là bằng chứng xác minh. Mặc định chỉ sử dụng dữ liệu tổng hợp; test migration dữ liệu thật chỉ dùng bản sao được phê duyệt và phải redaction.

Chuỗi lệnh backend đã được xác minh tại checkpoint hiện tại:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build
```

Chỉ chạy integration/security test với các dependency test biệt lập. Với công việc frontend, sử dụng lockfile npm và script hiện có; không trộn package manager hoặc tự tạo script. Bộ AD được chạy tái lập bằng `npx vitest run --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1` vì lệnh song song mặc định không kết thúc trong môi trường này.

Đối với SQL Server Đợt 2:

- chỉ recreate/DDL/DML trên database test có tên được cấp phép rõ ràng; không chạm `[ttsmart.com.vn]`, `[TTSmart]`, database `_online` hiện hữu hoặc SQL Server production nếu chưa có quyền riêng;
- test schema phải bao phủ checksum mismatch, chạy lại idempotent, concurrent runner, constraint trusted/enabled, tên constraint ổn định, database options và schema fingerprint tái lập;
- constraint test phải rollback và thực sự thực thi từng case; không dùng `WHERE 1=0` hoặc test chỉ đếm object làm bằng chứng nghiệp vụ;
- integration test phải bao phủ local identity, Company/Branch authorization, provisioning lease/idempotency, AI reservation ledger, snapshot đơn hàng, stock operation/reversal, file path traversal và transaction tồn kho đồng thời;
- migration dry-run phải đối soát document/subdocument count, orphan, tổng tiền, quantity, source mapping và file manifest/checksum;
- `DBCC CHECKCONSTRAINTS` và `DBCC CHECKDB ... WITH PHYSICAL_ONLY` là kiểm tra bổ sung, không thay thế contract/data/concurrency test;
- RCSI, collation, recovery model, backup/restore và query/index plan phải được kiểm thử trước khi chốt cấu hình production.

Trước khi bàn giao, chạy các build/test phù hợp, `git diff --check`, kiểm tra whitespace/UTF-8 cho file untracked và `git status --short`, rồi cập nhật `docs/migration/MIGRATION_STATUS.md`. Lệnh chưa thực sự chạy phải được đánh dấu `Not yet verified` (chưa xác minh).

## Trách nhiệm tài liệu

Giữ các tài liệu sống sau đồng bộ với code:

- `docs/migration/API_CONTRACT_MATRIX.md` cho mọi route legacy và trạng thái triển khai.
- `docs/security/ENDPOINT_ACCESS_MATRIX.md` cho chính sách truy cập.
- `docs/migration/MONGODB_MODEL_MAP.md` cho nguồn BSON và khả năng tương thích legacy.
- `docs/migration/MONGODB_TO_SQLSERVER_MAPPING_V1.md` cho mapping field-level và trạng thái bảo toàn dữ liệu.
- `docs/architecture/SQLSERVER_TARGET_ARCHITECTURE.md` cho ranh giới ControlPlane/Operational và mô hình đa công ty.
- `docs/migration/SQLSERVER_V1_BASELINE_IMPLEMENTATION.md` cho bằng chứng DDL/database test và các phần chưa xác minh.
- `docs/architecture/MODULE_MAP.md` để điều hướng.
- `docs/operations/ERROR_CATALOG.md` cho các định danh lỗi và sự kiện ổn định.
- `docs/migration/MIGRATION_STATUS.md` và `docs/migration/OPEN_QUESTIONS.md` cho tiến độ, blocker và quyết định còn mở. `docs/migration/PHASE_1_EXECPLAN.md` là hồ sơ lịch sử của Đợt 1, không còn là kế hoạch đang thực hiện.

Không sử dụng các cụm từ “hoàn tất”, “feature parity”, “production-ready”, “secure”, “lossless migration” hoặc “ready for cutover” cho tới khi có bằng chứng cho mọi cổng Definition of Done. Baseline SQL Server chỉ có đúng số bảng/constraint không đủ chứng minh bảo toàn MongoDB hoặc sẵn sàng migration.

## Chính sách ngôn ngữ

1. Mọi trao đổi với người dùng phải bằng tiếng Việt.
2. Mọi tài liệu Markdown do dự án sở hữu phải được viết bằng tiếng Việt.
3. Subagent phải báo cáo bằng tiếng Việt.
4. Giữ nguyên code identifier và API contract khi cần để bảo đảm tính chính xác kỹ thuật và khả năng tương thích.
5. Không tự ý dịch response của API hoặc thông báo giao diện nếu việc đó làm thay đổi feature parity.
6. Output nguyên bản của công cụ có thể giữ nguyên, nhưng phần giải thích và kết luận phải bằng tiếng Việt.
7. Tên file phải rõ ràng, nhất quán và dễ tìm kiếm khi bảo trì.

Việc chuyển tài liệu sang tiếng Việt không phải lý do để dừng migration.
