# Kiến trúc V2

## Trạng thái

Tài liệu này mô tả kiến trúc đích đã được chấp thuận cho Đợt 1. V2 đã có một số vertical slice và hạ tầng dùng chung được triển khai; hành vi endpoint ở trường hợp thành công, khả năng tương thích với fixture MongoDB và các tích hợp provider bên ngoài/thời gian thực vẫn chưa được xác minh đầy đủ hoặc đang được hoãn lại.

## Các thành phần

```text
React/Vite JS/JSX FE + AD
          |
          v
TTSmartEcom.Api (HTTP, cookies/JWT, CORS/CSRF, errors, logging, static/media, Socket.IO boundary)
          |
          v
TTSmartEcom.Application (use cases, DTOs, validation, policy ports)
          |
          v
TTSmartEcom.Domain (business concepts and invariants)
          ^
          |
TTSmartEcom.Infrastructure.MongoDb (BSON mappings, repositories, indexes, provider adapters)
          |
          v
MongoDB + bounded external providers (Gmail, Gemini, Telegram, Zalo)
```

## Luồng request

1. Correlation ID được chấp nhận từ request hoặc được tạo mới.
2. CORS, giới hạn body, security header, quá trình phân tích cookie và chính sách CSRF được thực thi.
3. Authentication xác thực cookie JWT tương thích với legacy và tải lại người dùng.
4. Authorization kiểm tra role, permission và quyền sở hữu object.
5. Validation DTO của API từ chối các field không xác định/không an toàn và kiểm tra giới hạn đầu vào.
6. Use case của Application điều phối các quy tắc domain và các port repository/provider.
7. Infrastructure lưu các document tương thích với MongoDB hoặc gọi provider với timeout/cancellation.
8. API ánh xạ kết quả thành response và status code tương thích với legacy.
9. Log có cấu trúc và sự kiện Socket.IO mang theo correlation ID mà không chứa payload nhạy cảm.

## Luồng dữ liệu và tệp

MongoDB vẫn là hệ thống lưu trữ chính thức của Đợt 1. Tệp upload phải vượt qua các bước kiểm tra extension, MIME, kích thước, chữ ký tệp, giới hạn tên tệp trong thư mục cho phép và tham chiếu trước khi được lưu. Việc phục vụ nội dung public/static là một ranh giới có chủ đích: hóa đơn và tài liệu riêng tư không được vô tình công khai. Tệp AI tạm thời cần được dọn dẹp theo vòng đời và kiểm tra tham chiếu.

## Ranh giới module

Các module legacy cần port gồm Users, Products, Orders, Import Orders, Export Orders, Cart, Stations, Chips/Types, Storefront Manage, Storage History, Activity Logs, Voice Vocabulary, Zalo và Telegram. Phạm vi route chính xác được ghi trong `docs/migration/API_CONTRACT_MATRIX.md`; phạm vi dữ liệu được ghi trong `docs/migration/MONGODB_MODEL_MAP.md`.

## Chuẩn bị cho Đợt 2

Các repository và port của application giữ kiểu dữ liệu riêng của MongoDB ra khỏi code nghiệp vụ. Contract ổn định, ánh xạ field rõ ràng và việc controller không truy cập MongoDB trực tiếp cho phép bổ sung SQL adapter sau này mà không thay đổi hành vi FE/AD. Đợt 2 không được triển khai tại đây.
