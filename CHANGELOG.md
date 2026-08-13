# Nhật ký thay đổi

Mọi thay đổi đáng chú ý của TTSmartEcomWeb V2 được ghi lại tại đây. Dự án chưa phát hành phiên bản nào.

## Chưa phát hành

### Đã thêm

- Khung solution ASP.NET Core 10 dạng module ban đầu với các project Api, Application, Domain và MongoDB Infrastructure.
- Khung project test unit, contract, integration và security.
- .NET SDK được cố định, phiên bản package tập trung, analyzer, nullable reference, thiết lập build xác định và lockfile NuGet.
- Tài liệu kiến trúc, migration, vận hành, bảo mật và bàn giao cho Đợt 1.
- Kiểm kê legacy đã đối chiếu: 201 handler đã được mount, 402 dạng method/URL có hiệu lực, 21 collection được suy luận và số lượng consumer frontend.

### Giới hạn đã biết

- Cả 201 contract method/path legacy hiện đi tới xử lý substantive; số explicit `501` và absent đều bằng 0. Đây là trạng thái triển khai route, không phải tuyên bố tương đương hành vi hoặc sẵn sàng cutover.
- Đã có ánh xạ document Mongo, fixture BSON tổng hợp và integration test MongoDB biệt lập cho các luồng được chọn; biến thể BSON lịch sử, index, hook, thao tác ghi và ngữ nghĩa null/thiếu của các collection còn lại vẫn chưa được xác minh đầy đủ.
- Đã triển khai upload/media, AI/voice Products, Zalo OAuth state dùng một lần, bốn sự kiện đơn hàng Socket.IO, notification đơn khách qua Gmail/Telegram/Zalo, ActivityLog mutation, runtime voice-vocabulary và product listing tương thích `adjusted`/`stationId`.
- Checkpoint backend đạt 332 test: Unit 231, Contract 53, Integration 16 và Security 32. FE đạt 81 test; AD đạt 205 test với Vitest `3.2.6` ghim chính xác và runner tuần tự có giới hạn.
- Đã sửa projection `_id` order/user/cart, bảo toàn policy đa ngôn ngữ/timestamp, station search exact, public product pricing redaction và giảm lost-update trong repository user bằng atomic update.
- Đã đóng `SEC-H-003`: audit AD toàn cây còn 2 moderate và 0 High/Critical. Chưa xác minh provider thật, staging, deployment hoặc E2E FE/AD với API V2; `SEC-H-001` vẫn mở nên production cutover bị chặn.

Mục nhật ký thay đổi này không đại diện cho commit, push, deployment, công việc SQL Server, công việc EF Core hoặc migration TypeScript nào.
