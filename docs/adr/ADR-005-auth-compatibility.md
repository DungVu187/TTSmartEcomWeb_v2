# ADR-005: Khả năng tương thích xác thực

- Trạng thái: Được chấp thuận cho Đợt 1
- Ngày: 2026-08-13

## Bối cảnh

Hệ thống xác thực legacy sử dụng cookie HTTP-only `authToken` chứa JWT có thời hạn 12 giờ, xác minh người dùng với MongoDB, hỗ trợ các role customer/admin/staff, vô hiệu hóa session sau khi đổi mật khẩu và có thể nâng cấp token autologin AES cũ thành token ngẫu nhiên. Password hash được quản lý bởi model người dùng legacy.

## Quyết định

V2 sẽ bảo toàn tên cookie, ý nghĩa của claim, thời lượng session, sự phân biệt role, việc xác minh password hash, vô hiệu hóa khi đổi mật khẩu và quá trình nâng cấp autologin legacy có giới hạn. Token mới phải sử dụng cấu hình secret được quản lý và các giá trị mặc định an toàn cho cookie. Authorization phải bao gồm kiểm tra permission của route và kiểm tra ở cấp object.

`ADMIN_FULL_ACCESS=true` được ghi nhận là hành vi tương thích legacy, cần có quyết định bảo mật rõ ràng trước khi siết chặt permission của admin.

## Các phương án đã xem xét

- Bắt buộc đặt lại mật khẩu: bị bác bỏ vì Đợt 1 yêu cầu tương thích với hash hiện có.
- Thay cookie bằng bearer token: bị bác bỏ vì các consumer FE/AD hiện tại sử dụng cookie.
- Tiếp tục cấp token AES chứa thông tin xác thực: bị bác bỏ; chỉ cho phép khả năng tương thích để đọc/nâng cấp.

## Hệ quả

Hành vi cookie/CSRF, CORS, proxy và xoay vòng khóa phải được kiểm thử cùng nhau. Một bản sửa lỗi bảo mật làm thay đổi hành vi đăng nhập có thể quan sát được phải được lên lịch như một quyết định tương thích, không được âm thầm hợp nhất.
