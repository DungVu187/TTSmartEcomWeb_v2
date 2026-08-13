# Checklist review bảo mật

## Identity và truy cập

- [ ] Cookie bị thiếu, sai định dạng, hết hạn, đã thu hồi hoặc sai role trả response 401/403 an toàn.
- [ ] Password hash và khả năng tương thích autologin legacy được test mà không làm lộ thông tin xác thực.
- [ ] Kiểm tra permission và cấp đối tượng bao phủ mọi nhóm route. Policy user đích, `expectedRole` và guard Super Admin đã có; độ bao phủ dương tính/Mongo/giữa các đối tượng toàn ma trận còn thiếu.
- [ ] Giới hạn lạm dụng login/recovery/AI/upload được cấu hình và test.

## HTTP và trình duyệt

- [ ] Cơ chế bảo vệ CSRF bao phủ mọi mutation được authentication bằng cookie. Kiểm thử khớp origin chính xác và từ chối origin cùng site nhưng khác origin đã đạt; staging/proxy/trình duyệt chưa xác minh.
- [ ] CORS allowlist, thông tin xác thực, proxy header, HSTS, CSP và security header được review. Kiểm thử CORS/mã nguồn đã có; review triển khai còn mở.
- [ ] Route `/api` và route không có tiền tố không thể bỏ qua middleware hoặc rơi xuống HTML của SPA.
- [ ] Response lỗi không chứa stack, secret, payload provider hoặc dữ liệu cá nhân.

## Input và persistence

- [ ] Allowlist DTO từ chối các trường không xác định và operator Mongo.
- [ ] ObjectId, page/limit/sort/date/regex/kích thước collection có giới hạn.
- [ ] Update inventory/order nguyên tử và các path rollback/xung đột được test.
- [ ] Tên trường BSON, hành vi null/thiếu, hook, index và giá trị legacy được test bằng fixture.

## File và provider

- [ ] Kiểm tra MIME, extension, kích thước, magic/chữ ký, giới hạn tên file, quarantine và reference đạt.
- [ ] Route static public chỉ công khai các projection được phê duyệt.
- [ ] Lời gọi Gemini/Gmail/Telegram/Zalo có timeout, cancellation, redaction và ánh xạ lỗi an toàn. Mã nguồn/Unit/Contract đã có; staging/provider thật chưa xác minh.
- [x] State callback Zalo chỉ dùng một lần, được ràng buộc với subject, có chữ ký và kiểm tra hết hạn; unit/contract/security test đã đạt.

## Vận hành

- [ ] Có correlation ID/EventId/mã lỗi và các giá trị này đã được redact phù hợp. EventId ActivityLog đã tách khỏi dải Gemini; độ bao phủ toàn hệ thống còn cần rà cuối.
- [ ] Bằng chứng audit dependency và quét secret được lưu giữ. Audit dependency đã cập nhật; chưa chạy scanner secret chuyên dụng.
- [ ] Backup/restore và rollback được diễn tập trong môi trường biệt lập.
- [ ] Cổng cutover không còn finding mức High chưa giải quyết.

Checkpoint kiểm tra hồi quy backend: 332/332 test đạt, gồm Unit 231, Contract 53, Integration 16 và Security 32. `SEC-H-003` đã đóng sau khi ghim Vitest `3.2.6`; checklist vẫn để mở khi bằng chứng staging, provider, Mongo hoặc độ bao phủ theo từng mục chưa đủ.
