# ADR-003: Ranh giới lưu trữ MongoDB

- Trạng thái: Được chấp thuận cho Đợt 1
- Ngày: 2026-08-13

## Bối cảnh

Đợt 1 phải đọc và ghi các document MongoDB cùng tên collection hiện có. Đợt 2 sẽ xử lý SQL Server riêng biệt. Code legacy sử dụng model Mongoose, mảng nhúng, ObjectId, trường ngày tháng, các giá trị lịch sử hỗn hợp và việc khởi tạo vocabulary khi startup.

## Quyết định

Giữ MongoDB phía sau `TTSmartEcom.Infrastructure.MongoDb`. Các repository và ánh xạ BSON phải bảo toàn rõ ràng tên collection, tên field, hành vi null/thiếu, document nhúng, giá trị ObjectId, ngày tháng, giá trị mặc định và index. Contract của Domain/Application không được để lộ kiểu dữ liệu của MongoDB driver.

## Các phương án đã xem xét

- Dùng Entity Framework Core/SQL Server ngay: nằm ngoài phạm vi Đợt 1 một cách rõ ràng.
- Gọi MongoDB trực tiếp từ API handler: bị bác bỏ vì gắn chặt hành vi HTTP với lớp lưu trữ và cản trở Đợt 2.
- Tự động migration schema khi startup: bị bác bỏ; migration và thao tác ghi production cần một quy trình riêng có kiểm soát.

## Hệ quả

Khối lượng công việc ánh xạ là đáng kể và phải được contract test bằng fixture tổng hợp. Các điểm bất quy tắc trong dữ liệu legacy phải được biểu diễn có chủ đích. Những thao tác ghi khi startup như seed/backfill voice vocabulary cần có chính sách V2 rõ ràng trước khi triển khai.
