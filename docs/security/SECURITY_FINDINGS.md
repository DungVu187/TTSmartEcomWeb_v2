# Các finding bảo mật

Bản chụp: 2026-08-13. Các giá trị đã được redact. Finding dựa trên việc kiểm tra legacy an toàn và review source/test V2; không thực hiện khai thác production hoặc kết nối dịch vụ production.

## High

### SEC-H-001 — Chính sách CSRF cho thao tác ghi bằng cookie chưa được xác minh khi triển khai

- Bằng chứng legacy: thao tác ghi bằng cookie JWT không có middleware CSRF đang hoạt động.
- Trạng thái V2: `LegacyCsrfOriginMiddleware` từ chối yêu cầu `POST`, `PUT`, `PATCH` và `DELETE` có cookie `authToken` nếu không có nguồn gốc trình duyệt đáng tin cậy. Middleware chấp nhận đúng một `Origin` trong allowlist, một `Referer` có origin trong allowlist hoặc, khi thiếu cả hai header đó, `Sec-Fetch-Site: same-origin`. Giá trị `same-site` không được chấp nhận.
- Bằng chứng kiểm thử: năm case chuyên biệt trong `ApiBoundarySecurityTests` bao phủ origin không đáng tin cậy, thiếu toàn bộ header nguồn gốc, origin được phép, origin cùng site nhưng khác origin bị từ chối và Fetch Metadata cùng origin được chấp nhận. Bộ Security đạt 30/30 trong cổng kiểm tra đầy đủ gần nhất.
- Tác động còn lại: mã nguồn và kiểm thử đã thu hẹp theo origin khớp chính xác, nhưng hành vi cookie HTTPS, trình duyệt và reverse proxy của mô hình triển khai dự kiến chưa được xác minh. Cách triển khai hiện tại không sử dụng token CSRF mã hóa.
- Khắc phục: xác minh trên staging phía sau reverse proxy với trình duyệt thật, cookie HTTPS, allowlist `Origin`/`Referer`, origin cùng site nhưng khác origin và forwarded-header/TLS; nếu chính sách origin khớp chính xác không được chủ sở hữu bảo mật phê duyệt thì triển khai token synchronizer/double-submit trong khi vẫn giữ khả năng tương thích `authToken`.
- Tương thích: giữ nguyên `authToken`; có thể cần thay đổi client để dùng header/token CSRF.
- Trạng thái: Đang mở; chặn cutover.

### SEC-H-002 — Phân quyền cấp đối tượng đích và role đích

- Bằng chứng khắc phục: `UserAdministrationPolicy` tập trung hóa phân cấp tạo role và thao tác trên role đích. Các route cập nhật quyền, cập nhật/xóa user, gán station và xoay autologin token tải lại đối tượng đích trước mutation; repository ghi/xóa kèm `expectedRole` để từ chối an toàn nếu role đổi trong lúc xử lý. Việc tạo hoặc nâng vai trò lên Super Admin đi qua mutex phân tán Mongo trước khi kiểm tra quy tắc duy nhất một Super Admin.
- Bằng chứng kiểm thử: nhóm policy role đích/guard đạt 7/7; kiểm thử bảo mật xác nhận admin có permission vẫn không thể thao tác admin ngang cấp hoặc tạo admin ngang cấp, guard đang bị giữ trả xung đột mà không gọi mutation. Kiểm thử tích hợp MongoDB biệt lập chạy tám contender, chỉ cho một owner và cho phép yêu cầu tiếp theo sau khi release; kết quả đạt 1/1.
- Tác động còn lại: chưa có độ bao phủ endpoint dương tính rộng hoặc kiểm thử tích hợp Mongo user thật cho mọi mutation; hai hình thức `PUT /users/{id}` và `PUT /users/stations` chưa có case endpoint âm tính riêng trong bộ role đích hiện tại. Đây là khoảng trống xác minh, không còn là lỗi phân quyền mức High đã thấy ban đầu.
- Khắc phục còn lại: bổ sung case endpoint dương tính/âm tính còn thiếu và kiểm thử tích hợp Mongo user với dữ liệu tổng hợp; ghi runbook xử lý guard mồ côi trước triển khai.
- Tương thích: nhằm khôi phục authorization legacy, không nới lỏng authorization.
- Trạng thái: Đã xử lý và đóng ở mức High. Phần thiếu độ bao phủ còn lại được theo dõi tại `SEC-M-007`.

### SEC-H-003 — Dependency phát triển frontend quản trị có finding Critical/High

- Bằng chứng khắc phục: `vitest` đã được nâng và ghim chính xác ở `3.2.6`; lockfile sạch resolve cây Vitest qua Vite `6.4.3` và esbuild `0.25.12`. Sau `npm ci`, bộ AD đạt 205/205 test bằng runner tuần tự có giới hạn, lint thoát 0 với 27 warning hiện hữu và production build đạt.
- Bằng chứng audit sau khắc phục: cả `npm audit --omit=dev` và `npm audit` đầy đủ cho AD chỉ còn 2 finding moderate trên chuỗi `exceljs@4.4.0 -> uuid@8.3.2`, 0 high và 0 critical. FE và audit lỗ hổng NuGet báo cáo 0 finding.
- Tác động còn lại: tồn dư ExcelJS/UUID không còn ở mức High và được theo dõi riêng tại `SEC-M-008`; npm chỉ đề xuất hạ ExcelJS xuống `3.4.0`, nên không áp dụng auto-fix gây phá tương thích.
- Trạng thái: Đã xử lý và đóng ở mức High. Không còn finding Critical/High từ dependency tại checkpoint này.

### SEC-H-004 — MongoDB archive nằm trong workspace V2

- Bằng chứng: file untracked `Ecom_2026-08-18_ 400.archive.gz` nằm ngay root repository, dung lượng 123.344.022 byte, SHA-256 `CBED3D34CF74702B78A771D21BA80F5122B4A396C55DC76C334B064AAB0775DB`. Header giải nén có metadata đặc trưng của MongoDB archive; audit không đọc document hoặc nội dung payload.
- Tác động: dump có thể chứa PII, password hash, token/provider secret hoặc dữ liệu nghiệp vụ và đang nằm sai ranh giới lưu trữ dù chưa được Git theo dõi. Việc vô tình đóng gói, backup hoặc chia sẻ workspace có thể làm lộ dữ liệu.
- Khắc phục: owner phải xác nhận đây là bản sao tổng hợp hay dữ liệu thật; nếu là dữ liệu thật, chuyển sang kho migration được phê duyệt có kiểm soát truy cập/retention rồi xóa an toàn khỏi workspace. Không commit, upload hoặc mở rộng nội dung để triage.
- Trạng thái: Đang mở; chặn mọi tuyên bố cutover hoặc chia sẻ workspace.

### SEC-H-005 — Scope Company/Branch chưa điều khiển database Operational

- Bằng chứng: `CurrentUserContextMiddleware` xác minh `X-Company-Id`/`X-Branch-Id`, nhưng `OperationalDbConnectionFactory` luôn dùng một `OperationalConnectionString` tĩnh và `DefaultSqlConnectionFactory` chuyển toàn bộ repository vào connection này. Không có lookup `BranchDatabases`, database assignment hoặc factory request-scoped theo active Branch.
- Tác động: khi user chọn Branch B, authorization có thể cho phép đúng membership B nhưng nghiệp vụ vẫn đọc/ghi database Operational cấu hình chung. Nếu triển khai đa chi nhánh ở trạng thái này, ranh giới dữ liệu vật lý không khớp ranh giới quyền và có nguy cơ truy cập chéo chi nhánh.
- Khắc phục: triển khai resolver server-side từ active Company/Branch sang registry đã kiểm tra quyền, trạng thái provisioning, release/schema và secret reference; tạo connection request-scoped sau allowlist; thêm integration test hai database Operational cô lập chứng minh không đọc/ghi chéo.
- Trạng thái: Đang mở; chặn rollout đa công ty/chi nhánh và cutover.

### SEC-H-006 — Phiên Control Plane không bị thu hồi khi credential thay đổi

- Bằng chứng: `SecurityStamp` và `MustChangePassword` được đọc từ SQL nhưng không được đưa vào `ICurrentUserContext`, JWT hoặc validation mỗi request. Cookie Control Plane chỉ mang `userId`, `phone`, `role`, scope và `iat`; middleware tải lại status/membership nhưng không so khớp security stamp. Cơ chế `PasswordChangedAt` chỉ áp dụng cho identity Operational.
- Tác động: token đã phát hành có thể tiếp tục hoạt động tối đa thời lượng phiên sau khi đổi/reset password hoặc xoay security stamp; tài khoản có `MustChangePassword = 1` vẫn đăng nhập như bình thường.
- Khắc phục: chốt contract bắt buộc đổi mật khẩu, gắn phiên với `SecurityStamp` hoặc session version đáng tin cậy, kiểm tra lại ở HTTP và Socket.IO, đồng thời có test thu hồi sau password reset/stamp rotation.
- Trạng thái: Đang mở; chặn rollout identity Control Plane.

### SEC-H-007 — Runbook `[TTSmart]` có thể chạy script recreate phá hủy dữ liệu

- Bằng chứng: `database/sqlserver/TTSmart/README.md` hướng dẫn chạy mọi `*.sql` theo tên. Cùng thư mục có `000_RecreateTTSmart30.sql`, script hardcode `USE [master]`, chuyển `[TTSmart]` sang single-user, drop rồi tạo lại database. Runner `schema-recreate` cũng chạy script này và ghi đè checksum `SchemaVersions` khi khác thay vì từ chối drift; danh sách runner chưa gồm migration 013 mới.
- Tác động: làm theo runbook có thể xóa toàn bộ `[TTSmart]`; cơ chế cập nhật checksum che drift và tạo trạng thái schema không phản ánh chuỗi migration thực tế.
- Khắc phục: ngừng dùng wildcard runner cho database thật; tách script destructive khỏi migration directory; chỉ cho recreate đúng database test allowlist; dùng một chuỗi migration version hóa duy nhất với checksum content bất biến, application lock và mismatch fail-closed.
- Trạng thái: Đang mở; chặn chạy lại schema/cutover trên `[TTSmart]`.

## Medium

### SEC-M-001 — Đã có validation upload nhưng độ bao phủ endpoint/lưu trữ chưa được xác minh đầy đủ

- Bằng chứng legacy: kiểm tra chỉ dựa trên MIME/extension không nhất quán giữa upload sản phẩm, storefront, station và catalog.
- Trạng thái V2: `FileValidationService` xác thực giới hạn tên file, extension, MIME, kích thước và magic byte; các route media Product, Catalog, Storefront, Station, Sales Order và Import/Export Order đã gọi validator trước khi ghi. Unit test validator/media, positive contract Product upload và security boundary media đã đạt; chưa có integration test MongoDB/filesystem biệt lập cho toàn bộ luồng hoặc positive endpoint coverage rộng.
- Tác động: các ranh giới lưu trữ đã mở rộng đáng kể nhưng tính nhất quán giữa cập nhật reference Mongo và xóa file vật lý, vòng đời file tạm và static policy vẫn chưa được chứng minh.
- Khắc phục: bổ sung integration test biệt lập, kiểm tra lỗi nửa chừng giữa Mongo/filesystem, sử dụng tên sinh an toàn và test chữ ký, traversal, giới hạn cùng header phục vụ static.
- Trạng thái: Cổng triển khai đang mở.

### SEC-M-002 — Ranh giới nội dung tĩnh công khai cần được xác minh khi triển khai

- Bằng chứng: các route station công khai hiện ánh xạ DTO tường minh theo hình dạng legacy, không tuần tự hóa trực tiếp document Mongo. `UseStaticFiles` và các root upload công khai vẫn chưa được xác minh end-to-end theo mô hình triển khai.
- Tác động: cấu hình sai root static, content type hoặc header HTTP có thể công khai file ngoài dự kiến hay cho trình duyệt diễn giải nội dung không an toàn.
- Khắc phục: ánh xạ và phân loại rõ từng root static, content type, CSP/content-disposition và chạy kiểm thử triển khai với file tổng hợp; tiếp tục giữ projection station tường minh khi model mở rộng.
- Trạng thái: Đang mở.

### SEC-M-003 — Cơ chế bỏ qua kiểm tra bằng quyền truy cập đầy đủ của admin vẫn được bật để tương thích

- Bằng chứng: `LegacyCompatibility.AdminFullAccess` mặc định là true và authorization handler cấp cho admin quyền truy cập theo chính sách permission khi được bật.
- Tác động: admin có quyền quá rộng/bị xâm phạm có thể bỏ qua các permission chi tiết.
- Khắc phục: phê duyệt quá trình backfill permission theo giai đoạn và chuyển sang mặc định false kèm regression test.
- Trạng thái: Đang mở; cần quyết định về khả năng tương thích.

### SEC-M-004 — Ranh giới provider/OAuth/gửi dữ liệu chưa được xác minh với staging

- Bằng chứng: Zalo OAuth dùng state có chữ ký, gắn subject, hết hạn và chỉ dùng một lần; adapter Zalo/Gemini/Telegram/Gmail có timeout, cancellation, giới hạn response/lỗi và logging đã redact. Notification order Gmail/Telegram/Zalo cùng bốn event Socket.IO chạy best-effort sau commit; kiểm thử dùng handler/provider giả hoặc dependency biệt lập, không gọi dịch vụ production.
- Tác động: thông tin xác thực, callback URL, proxy/TLS, quota, response thực tế và ngữ nghĩa lỗi của provider vẫn chưa được xác minh trong staging. Cấu hình sai có thể làm notification thất bại hoặc làm lộ siêu dữ liệu vận hành dù mã nguồn đã có redaction.
- Khắc phục: chạy kiểm thử contract/smoke cho provider trên staging biệt lập với thông tin xác thực thử nghiệm, xác minh timeout/quota/callback/replay/redaction và giữ công tắc tắt bằng cấu hình trước khi bật gửi thật.
- Trạng thái: Đang mở.

### SEC-M-005 — ActivityLog đã được port nhưng độ bao phủ persistence/staging còn giới hạn

- Bằng chứng: 48 mutation route ghi 44 action legacy qua application audit port theo best-effort sau commit. Actor lấy từ identity đang sống; secret, token, OTP và chat ID không được ghi. Unit/Contract/Integration bao phủ hình dạng BSON, projection, redaction, action provider/voice và lỗi writer không làm hỏng mutation đã commit.
- Tác động còn lại: chưa có độ bao phủ Mongo/staging dương tính cho từng callsite và chính sách lưu giữ/dung lượng/khả dụng của collection `activitylogs` chưa được diễn tập.
- Khắc phục: bổ sung kiểm thử lấy mẫu/tích hợp theo nhóm mutation còn thiếu, kiểm tra index/chính sách lưu giữ và quan sát EventId `4691`–`4692` trong staging khi kho audit lỗi.
- Trạng thái: Đang mở.

### SEC-M-006 — Độ bao phủ fixture và thao tác ghi MongoDB chưa đầy đủ

- Bằng chứng: đã có repository/document BSON rõ ràng và fixture tổng hợp cho Product/Order/Section, `_id` cart nhúng, `productId` ObjectId/string hỗn hợp của import/export order, policy storefront đa ngôn ngữ/timestamp và hình dạng ghi ActivityLog. Integration MongoDB biệt lập đã kiểm tra aggregation inventory, query/reference ActivityLog và tranh chấp mutex Super Admin. Tuy vậy, các bằng chứng này chỉ bao phủ lát cắt được chọn, chưa đủ 21 collection hoặc mọi đường ghi.
- Tác động còn lại: reference ObjectId/string hỗn hợp, tiền dạng số/chuỗi, giá trị thiếu/null, hook và version optimistic vẫn có thể khác legacy ở các collection/đường ghi chưa phủ. Defect tái sinh `_id` của `product.documents[]` đã được sửa; address/order-template đã có compare-and-exchange với thử lại và nhận diện template theo `_id`, nhưng chưa có positive endpoint integration Mongo rộng.
- Khắc phục: mở rộng fixture BSON legacy và integration test Mongo biệt lập cho mọi nhóm thao tác ghi; bổ sung positive endpoint Mongo cho product documents và các mutation address/order-template trước bất kỳ kết nối production nào.
- Trạng thái: Đang mở.

### SEC-M-007 — Độ bao phủ xác minh phân quyền quản trị user chưa đầy đủ

- Bằng chứng: policy role đích, `expectedRole` từ chối an toàn và mutex Super Admin đã đóng finding High tương ứng. Bộ role đích hiện có kiểm thử âm tính cho cập nhật quyền, xóa, thêm station, xoay token, alias `/api`, tạo admin ngang cấp và tranh chấp guard.
- Khoảng trống: chưa có case endpoint âm tính riêng cho `PUT /users/{id}` và `PUT /users/stations`, chưa có case dương tính cho toàn bộ phân cấp hợp lệ và chưa có kiểm thử tích hợp các mutation user trên MongoDB biệt lập.
- Tác động: lỗi hồi quy trong adapter/controller hoặc mapping Mongo của những đường chưa phủ có thể không được phát hiện sớm, dù mã nguồn hiện không cho thấy đường leo thang đặc quyền ban đầu.
- Khắc phục: bổ sung ma trận dương tính/âm tính theo role actor, role đích và action; chạy với repository giả để chứng minh ranh giới và MongoDB biệt lập để chứng minh bộ lọc `expectedRole`.
- Trạng thái: Đang mở; không tái mở `SEC-H-002` nếu không có bằng chứng lỗi phân quyền mới.

### SEC-M-008 — Dependency ExcelJS/UUID còn finding Moderate

- Bằng chứng: audit runtime và audit toàn cây AD đều báo 2 finding moderate trên chuỗi `exceljs@4.4.0 -> uuid@8.3.2`, 0 high và 0 critical. Vitest đã được ghim ở `3.2.6` và không còn cây Vite/esbuild cũ bị báo Critical/High.
- Tác động: advisory UUID liên quan đường gọi có buffer do caller cung cấp; cần tiếp tục đánh giá khả năng tiếp cận từ luồng export Excel của AD. Không có bằng chứng cho thấy frontend hiện truyền buffer không tin cậy vào API UUID bị ảnh hưởng.
- Khắc phục: theo dõi bản ExcelJS kéo UUID đã sửa, review release notes và nâng có kiểm soát; không hạ xuống ExcelJS `3.4.0` hoặc chạy `npm audit fix --force` chỉ để làm sạch số audit.
- Trạng thái: Đang mở ở mức Medium; không phải blocker High nhưng cần triage trước release.

### SEC-M-009 — Thay đổi đường fallback authentication khi tích hợp Control Plane

- Bằng chứng thay đổi: tài khoản có identifier trong Control Plane chỉ được xác thực bằng `ttsmart.com.vn`. Tài khoản legacy trong `TTSmart` chỉ được thử khi identifier không tồn tại trong Control Plane; sai mật khẩu, khóa hoặc inactive không được rơi xuống Operational DB.
- Tác động tương thích: nếu cùng một số điện thoại/email tồn tại ở cả hai nguồn, mật khẩu Control Plane trở thành authority; đây là thay đổi có chủ đích để không cho phép bypass lockout hoặc password bằng tài khoản trùng trong Operational DB.
- Bằng chứng kiểm thử: test authentication Control Plane trên SQL Server cô lập và test unit boundary permission đã đạt; chưa xác minh với dữ liệu tài khoản thật hoặc staging.
- Trạng thái: Đã xử lý trong checkpoint local; cần owner/security phê duyệt chính sách trùng identifier trước rollout.

### SEC-M-010 — Endpoint self-service và role legacy chưa tương thích với Control Plane

- Bằng chứng: `/users/change-password`, update profile/address/template và các mutation user self-service vẫn dùng `IUserProfileRepository` của Operational với GUID Control Plane ở vị trí `PublicId char(24)`, nên trả 404. Projection profile/JWT đổi mọi Control Plane user không phải platform SuperAdmin thành role `staff`; frontend `adminOnly` và các endpoint `[Authorize(Roles = "superadmin,admin")]` vì vậy chặn Company Admin khỏi account/Zalo/Telegram bất kể permission scope.
- Tác động: user Control Plane đăng nhập được nhưng không tự đổi mật khẩu/cập nhật hồ sơ và có menu/API không nhất quán với role Company.
- Khắc phục: tách use case profile/password Control Plane khỏi Operational local identity; định nghĩa mapping role/permission rõ cho endpoint legacy hoặc thay role gate bằng permission scope đã duyệt; thêm contract test dương tính cho Company Admin và Branch Staff.
- Trạng thái: Đang mở.

### SEC-M-011 — `ScopeAuthorizeAttribute` chưa được nối vào endpoint và có nhánh bỏ qua action

- Bằng chứng: không có controller/action nào gắn `[ScopeAuthorize]`. Trong attribute, nhánh `IsPlatformSuperAdmin` `return` trực tiếp mà không gọi `next()`, nên nếu bắt đầu dùng thì SuperAdmin nhận response rỗng thay vì action được thực thi. Test hiện chỉ kiểm tra service scope thuần, không chạy action filter end-to-end.
- Tác động: tài liệu đang mô tả object-scope đã được attribute bảo vệ trong khi code chưa có callsite; defect latent có thể làm hỏng endpoint khi áp dụng.
- Khắc phục: sửa pipeline filter, gắn vào từng contract mới có target Company/Branch, tải target object server-side và thêm test MVC end-to-end cho allow/deny/SuperAdmin.
- Trạng thái: Đang mở.

### SEC-M-012 — Tài liệu migration đang chứa định danh tài khoản thật

- Bằng chứng: `MIGRATION_STATUS.md` ghi trực tiếp GUID và số điện thoại của tài khoản Super Admin đã chuyển. Baseline repo cấm xuất PII vào tài liệu/evidence.
- Tác động: tài liệu Git có thể trở thành kênh phát tán định danh cá nhân và làm khó retention/redaction.
- Khắc phục: thay bằng định danh synthetic hoặc bằng chứng aggregate/checksum đã redact; rà lịch sử Git nếu nội dung từng được commit.
- Trạng thái: Đang mở.

## Mức Low / vệ sinh bảo mật

- `appsettings.json` development chứa các giá trị placeholder Mongo/JWT local. Deployment phải ghi đè chúng và nên fail closed khi gặp placeholder.
- Đã có rate limiting cho authentication, nhưng giới hạn lạm dụng/chi phí rộng theo từng endpoint vẫn chưa đầy đủ.
- `UseStaticFiles` chưa có biện pháp tăng cường CSP/content-disposition được ghi tài liệu tại checkpoint này.
- Tìm kiếm văn bản thủ công không thấy giá trị secret production, nhưng chưa chạy công cụ quét secret chuyên dụng; không được mô tả việc này là quét secret đạt.

## Biện pháp kiểm soát đã triển khai kèm bằng chứng

- Trích xuất cookie JWT, mặc định 12 giờ, tải lại identity đang hoạt động và vô hiệu hóa khi đổi password.
- Unit test khả năng tương thích Bcrypt.
- Chính sách permission và khả năng tương thích admin có thể cấu hình.
- Correlation ID và lỗi toàn cục đã redact.
- CORS allowlist cùng kiểm thử CSRF khớp origin chính xác cho Origin không đáng tin cậy, thiếu nguồn gốc trình duyệt, Origin được phép, origin cùng site nhưng khác origin và Fetch Metadata cùng origin. Forwarded headers chỉ được bật khi có allowlist proxy/network hợp lệ, với giới hạn hop và test cấu hình .NET 10.
- Unit test validation file cho chữ ký PDF, extension/MIME không khớp, path traversal, chữ ký ảnh sai và kích thước.
- Test hình dạng liveness an toàn và redaction route không xác định.
- Checkpoint Phase 3A chạy với SQL Server test cô lập đạt 362/362 test: Unit 245, Contract 53, Integration 27 và Security 37. Đây không phải bằng chứng staging/provider/E2E.

Các biện pháp kiểm soát này đã đóng `SEC-H-002` và `SEC-H-003`. `SEC-H-001` vẫn mở và chặn cutover; các kết quả local không chứng minh trạng thái sẵn sàng triển khai.
