# Baseline bảo mật

## Đánh giá hiện tại

Đây là baseline migration, không phải chứng nhận bảo mật. V2 hiện có các biện pháp kiểm soát dùng chung cho cookie JWT, tải lại principal, CSRF khớp origin chính xác, authorization user đích, correlation ID, exception đã redact, rate limiting, ranh giới provider và health check. `SEC-H-002` đã đóng bằng policy tập trung, bộ lọc `expectedRole` và guard Super Admin; `SEC-H-003` đã đóng bằng Vitest `3.2.6` ghim chính xác và audit/test/lint/build lại. Một finding High vẫn mở: xác minh CSRF trên staging/proxy/trình duyệt (`SEC-H-001`). Cổng kiểm tra backend đầy đủ đạt 332/332 test, nhưng bằng chứng endpoint/Mongo/provider/staging vẫn chưa đủ để chứng minh mức an toàn cho production.

## Baseline bắt buộc

- Chỉ cung cấp secret qua môi trường/secret manager; source và tài liệu không chứa giá trị secret.
- Cookie có HttpOnly, Secure, SameSite phù hợp và cơ chế bảo vệ CSRF rõ ràng.
- CORS allowlist nghiêm ngặt và cấu hình proxy đáng tin cậy.
- Xác thực algorithm/issuer/audience/expiry của JWT và thu hồi khi đổi password.
- Authorization theo role, permission và cấp đối tượng.
- Allowlist DTO và request JSON/multipart có giới hạn.
- Mongo filter được dựng từ giá trị có type; từ chối operator và regex không an toàn.
- Bảo vệ upload nhiều lớp: kích thước, MIME, extension, chữ ký, giới hạn tên file, quarantine và xóa an toàn theo reference.
- Structured log đã redact, mã lỗi ổn định, correlation ID và không có stack trace trong response.
- Rate limiting cho login, khôi phục password, endpoint AI/upload tốn tài nguyên và các lượt đọc public dễ bị lạm dụng.
- Cố định dependency, lockfile, review lỗ hổng và build có thể tái lập.
- Dữ liệu test biệt lập và không có mutation production trong quá trình xác minh.

## Cổng kiểm soát

Mọi vấn đề mức Critical/High chưa được giải quyết đều chặn production cutover. Tại checkpoint này chỉ `SEC-H-001` còn mở; `SEC-H-002` và `SEC-H-003` đã đóng, còn các tồn dư độ bao phủ/dependency được theo dõi ở mức Medium. Chi tiết nằm trong `SECURITY_FINDINGS.md`.
