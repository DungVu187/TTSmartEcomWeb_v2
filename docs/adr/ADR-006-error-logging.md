# ADR-006: Chiến lược lỗi và ghi log

- Trạng thái: Được chấp thuận; đã triển khai tại HTTP, provider, notification và realtime, còn chờ xác minh staging/provider thật
- Ngày: 2026-08-13

## Bối cảnh

Legacy dùng nhiều error envelope và ghi log console không đồng nhất. Một số đường có nguy cơ chuyển tiếp lỗi provider/database, trong khi vận hành cần correlation ID, mã lỗi và EventId đủ ổn định để chẩn đoán. V2 cũng có các side effect sau commit như ActivityLog, storage history, notification và Socket.IO; lỗi ở đây không được làm người vận hành hiểu nhầm mutation chính đã rollback.

## Quyết định

1. Mọi HTTP request đi qua `CorrelationIdMiddleware`; V2 nhận hoặc sinh `X-Correlation-ID`, giới hạn ký tự/độ dài và đưa ID vào logging scope/response.
2. `LegacyExceptionMiddleware` ánh xạ exception xác định sang status và mã `TTS-*`, trả thông báo công khai an toàn và không trả stack trace, Mongo error hoặc payload provider thô.
3. Các module dùng `[LoggerMessage]` với EventId ổn định. Log chỉ chứa dữ liệu vận hành allowlist như mã lỗi, HTTP status, operation, action và exception type; không chứa token, OTP, credential, connection string, owner guard, PII hoặc nội dung upload.
4. Provider có timeout/cancellation, response giới hạn khi phù hợp và mapping fail-closed. Riêng lỗi Gemini runtime sau khi gọi provider trả 503 có chủ đích; không giả lập kết quả AI thành công.
5. ActivityLog, storage history, notification và Socket.IO publish có đường best-effort sau commit được ghi EventId riêng. Response mutation chính không bị đổi thành lỗi chỉ vì side effect legacy-compatible thất bại; operator phải đọc lại state trước khi retry.
6. Guard mutation Super Admin fail closed và không TTL. Contention/release failure dùng EventId `1291`/`1292`; chỉ được xóa guard orphan thủ công sau khi chắc chắn không còn owner hoạt động.
7. `ERROR_CATALOG.md` là danh mục vận hành sống cho mã lỗi và EventId; EventId đã cấp không được tái sử dụng với nghĩa khác.

## Phạm vi đã triển khai

- Boundary HTTP/auth/CSRF/Mongo: correlation ID, mã `TTS-*`, EventId nền tảng và redaction.
- Gemini, Telegram, Zalo OAuth, SMTP, Zalo notification: EventId provider, timeout/error mapping và response an toàn.
- Notification scheduler/dispatcher: EventId cho channel failure, saturation và shutdown.
- ActivityLog/storage history: EventId best-effort sau commit.
- Engine.IO/Socket.IO: EventId cho origin, heartbeat, queue, delivery và publish bốn event order.

Provider thật cho Zalo OAuth, Gemini, SMTP, Telegram và Zalo notification chưa được xác minh. Static FE/AD và Socket.IO chưa được diễn tập qua reverse proxy staging; vì vậy ADR này không phải bằng chứng production readiness. `SEC-H-001` vẫn chặn cutover; `SEC-H-003` đã đóng tại checkpoint dependency hiện tại.

## Các phương án đã xem xét

- Chuyển tiếp exception/message provider thô: bác bỏ vì có thể lộ secret, topology và dữ liệu cá nhân.
- Luôn trả 500 chung: bác bỏ vì client/operator cần phân biệt validation, auth, conflict, Mongo, provider và lỗi nội bộ.
- Trả 200 với payload rỗng khi AI/provider lỗi: bác bỏ vì tạo false success; Gemini dùng 503 có chủ đích.
- Để side effect sau commit ném lỗi ngược lên mutation: bác bỏ vì lệch hành vi legacy và dễ khiến client retry mutation đã commit.
- Tự động hết hạn/xóa guard Super Admin: bác bỏ vì có thể mở lại race và tạo nhiều Super Admin.

## Hệ quả

- Operator có thể tương quan response/log bằng `X-Correlation-ID`, mã `TTS-*` và EventId trong [danh mục lỗi](../operations/ERROR_CATALOG.md).
- Client chỉ nhận thông báo an toàn; chẩn đoán sâu dựa vào log đã redaction.
- 503 provider là trạng thái có chủ đích, không phải fallback thành công.
- Warning/Error ở side effect phải được đánh giá cùng state đã commit để tránh retry trùng.
- Thêm EventId mới đòi hỏi cập nhật danh mục lỗi, test redaction và tài liệu troubleshooting.
- Việc thay policy origin/reverse proxy hoặc bật provider thật cần staging verification riêng; xem [runbook triển khai](../operations/DEPLOYMENT_RUNBOOK.md).
