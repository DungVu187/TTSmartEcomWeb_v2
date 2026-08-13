# AGENTS.md

## Mục đích

Repository này là bản thay thế được xây dựng độc lập cho `D:\TTSmartEcomWeb`. Quá trình migration gồm ba đợt riêng biệt:

1. Đợt 1: Chuyển Node.js/Express sang ASP.NET Core 10, tiếp tục sử dụng MongoDB và các frontend JavaScript/JSX.
2. Đợt 2: Chuyển MongoDB sang SQL Server trong khi vẫn giữ nguyên API contract của ASP.NET.
3. Đợt 3: Chuyển JavaScript/JSX sang TypeScript/TSX trong khi vẫn sử dụng ASP.NET Core và SQL Server.

Hiện tại chỉ Đợt 1 thuộc phạm vi. Không đưa vào SQL Server, Entity Framework Core, migration TypeScript, microservices, queue, Redis, multi-tenancy, subscription hoặc chức năng nghiệp vụ mới.

## Ranh giới repository

- Nguồn legacy: `D:\TTSmartEcomWeb` — luôn luôn chỉ đọc.
- Đích V2: `D:\TTSmartEcomWeb_v2` — mọi chỉnh sửa chỉ được thực hiện tại đây.
- Không sao chép `.env`, thông tin xác thực, dữ liệu production, upload, dump, log, backup, `node_modules`, `dist` hoặc output `build`.
- Không commit, push, deploy, thay đổi chế độ hiển thị của GitHub hoặc kết nối tới dịch vụ production nếu không có chỉ dẫn riêng, rõ ràng.
- Bảo toàn các thay đổi không liên quan trong cả hai repository. Không bao giờ dùng lệnh Git có tính phá hủy để khôi phục công việc của người khác.

Trước và sau khi khảo sát legacy, chỉ đọc và ghi nhận kết quả của `git branch --show-current`, `git rev-parse HEAD` và `git status --short`. Ghi kết quả vào `docs/migration/LEGACY_BASELINE.md` mà không thay đổi worktree legacy.

## Nguồn sự thật của Đợt 1

Source code legacy là nguồn có thẩm quyền về hành vi nghiệp vụ, route, hình dạng request/response, authentication, authorization, document MongoDB, file, dịch vụ ngoài và giả định của frontend. Kiểm kê đã đối chiếu hiện tại:

- 201 HTTP handler đã được mount.
- Cả URL không có tiền tố và URL có tiền tố `/api` đều có hiệu lực, tạo thành 402 dạng method/URL.
- 21 collection MongoDB được suy luận.
- 42 mẫu API route của frontend khách hàng và 144 mẫu của frontend quản trị.
- Authentication bằng cookie JWT sử dụng `authToken`.
- Bốn sự kiện đơn hàng Socket.IO.

Không được tuyên bố tương đương chỉ dựa trên các tổng số này. Cập nhật ma trận contract và trạng thái khi từng endpoint được triển khai và xác minh.

## Kiến trúc và chiều dependency

Đợt 1 là một modular monolith với các project nằm trong `backend/src`:

```text
TTSmartEcom.Api -> TTSmartEcom.Application -> TTSmartEcom.Domain
        |                    ^
        +-> TTSmartEcom.Infrastructure.MongoDb
                              |
                              +-> Application, Domain
```

- Domain chứa các khái niệm nghiệp vụ và không có dependency vào MongoDB/ASP.NET.
- Application chứa các use case, port, validation và interface chính sách authorization.
- Infrastructure.MongoDb sở hữu các type của Mongo driver, tên collection, khả năng tương thích BSON, index và implementation persistence.
- Api sở hữu khả năng tương thích HTTP, cookie, middleware authentication, serialization, ranh giới static/file, xử lý exception, logging và composition.
- Controller phải giữ mỏng. Api không được truy cập Mongo trực tiếp và Domain/Application không được chứa type Mongo.

Authentication, authorization, xử lý exception, logging, configuration, contract, `Program.cs`, file project và phiên bản package trung tâm dùng chung cần có quyền sở hữu được điều phối; tránh chỉnh sửa đồng thời.

## Quy tắc tương thích API

- Giữ nguyên method, kiểu chữ hoa/thường, path, tên query, tên trường multipart, tên property JSON, response envelope, status code và cả hai dạng có/không có tiền tố `/api` của legacy.
- Không thêm tiền tố phiên bản hoặc âm thầm chuẩn hóa route.
- Giữ nguyên tên cookie `authToken`, khả năng tương thích JWT claim, hành vi phiên 12 giờ, xác minh bcrypt legacy và lộ trình nâng cấp autologin legacy có giới hạn.
- Thực thi authorization cấp đối tượng và permission rõ ràng. `ADMIN_FULL_ACCESS=true` của legacy là một quyết định tương thích/bảo mật đã được ghi nhận, không phải sự cho phép âm thầm làm yếu code mới.
- Các bản sửa secure-by-default làm thay đổi hành vi quan sát được phải được ghi vào `docs/security/SECURITY_FINDINGS.md` và `docs/migration/OPEN_QUESTIONS.md`.

## Quy tắc tương thích dữ liệu

- Tiếp tục sử dụng MongoDB và tên collection hiện có trong Đợt 1.
- Coi giá trị bị thiếu, null, giá trị legacy hỗn hợp, ObjectId, ngày tháng, mảng nhúng, giá trị mặc định và giá dạng chuỗi là các vấn đề tương thích.
- Không chạy migration, seed, script sửa dữ liệu hoặc probe database production.
- Mọi implementation repository phải ánh xạ tên BSON một cách rõ ràng thay vì dựa vào quy ước đặt tên C# tình cờ.

## Baseline bảo mật

- Không bao giờ ghi log hoặc trả về secret, token, giá trị OTP, thông tin xác thực, connection string, password hash, dữ liệu khách hàng đã upload hoặc payload provider chứa dữ liệu nhạy cảm.
- Xác thực DTO theo allowlist, giá trị ObjectId, pagination, trường sort, khoảng ngày, input regex, kích thước collection, tên file, MIME type, extension, kích thước và chữ ký file.
- Mutation được authentication bằng cookie cần chiến lược CSRF rõ ràng. CORS không phải cơ chế bảo vệ CSRF.
- Giữ các tích hợp provider sau interface có timeout, cancellation, giới hạn kích thước response, redaction và ánh xạ lỗi xác định.
- Không báo cáo hệ thống là an toàn hoặc sẵn sàng cutover khi vẫn còn finding mức High.

## Test và xác minh

Các project test được tách thành bộ Unit, Contract, Integration và Security. Test placeholder không được tính là bằng chứng xác minh. Chỉ sử dụng dữ liệu tổng hợp.

Chuỗi lệnh backend đã được xác minh tại checkpoint hiện tại:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build
```

Chỉ chạy integration/security test với các dependency test biệt lập. Với công việc frontend, sử dụng lockfile npm và script hiện có; không trộn package manager hoặc tự tạo script. Bộ AD được chạy tái lập bằng `npx vitest run --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1` vì lệnh song song mặc định không kết thúc trong môi trường này.

Trước khi bàn giao, chạy các build/test phù hợp, `git diff --check` và `git status --short`, rồi cập nhật `docs/migration/MIGRATION_STATUS.md`. Lệnh chưa thực sự chạy phải được đánh dấu `Not yet verified` (chưa xác minh).

## Trách nhiệm tài liệu

Giữ các tài liệu sống sau đồng bộ với code:

- `docs/migration/API_CONTRACT_MATRIX.md` cho mọi route legacy và trạng thái triển khai.
- `docs/security/ENDPOINT_ACCESS_MATRIX.md` cho chính sách truy cập.
- `docs/migration/MONGODB_MODEL_MAP.md` cho khả năng tương thích BSON.
- `docs/architecture/MODULE_MAP.md` để điều hướng.
- `docs/operations/ERROR_CATALOG.md` cho các định danh lỗi và sự kiện ổn định.
- `docs/migration/PHASE_1_EXECPLAN.md` và `MIGRATION_STATUS.md` cho tiến độ trung thực.

Không sử dụng các cụm từ “hoàn tất”, “feature parity”, “production-ready”, “secure” hoặc “ready for cutover” cho tới khi có bằng chứng cho mọi cổng Definition of Done.

## Chính sách ngôn ngữ

1. Mọi trao đổi với người dùng phải bằng tiếng Việt.
2. Mọi tài liệu Markdown do dự án sở hữu phải được viết bằng tiếng Việt.
3. Subagent phải báo cáo bằng tiếng Việt.
4. Giữ nguyên code identifier và API contract khi cần để bảo đảm tính chính xác kỹ thuật và khả năng tương thích.
5. Không tự ý dịch response của API hoặc thông báo giao diện nếu việc đó làm thay đổi feature parity.
6. Output nguyên bản của công cụ có thể giữ nguyên, nhưng phần giải thích và kết luận phải bằng tiếng Việt.
7. Tên file phải rõ ràng, nhất quán và dễ tìm kiếm khi bảo trì.

Việc chuyển tài liệu sang tiếng Việt không phải lý do để dừng migration.
