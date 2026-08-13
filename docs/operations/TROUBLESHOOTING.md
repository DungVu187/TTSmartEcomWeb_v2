# Khắc phục sự cố

## Quy trình chung

1. Ghi timestamp, release checksum, route/method, status và `X-Correlation-ID`.
2. Tìm EventId/mã `TTS-*` trong log có cấu trúc; không suy đoán từ stack trace trả về client.
3. Che token, OTP, email, số điện thoại, connection string, owner guard và payload provider trước khi chia sẻ bằng chứng.
4. Kiểm tra sự hiện diện/định dạng cấu hình mà không in giá trị.
5. Xác định mutation chính đã commit hay chưa trước khi retry side effect, notification hoặc realtime.
6. Tái hiện bằng tài khoản, Mongo và file tổng hợp trong môi trường biệt lập.

## Health và MongoDB

- `/health/live` chỉ chứng minh process đang phục vụ HTTP.
- `/health/ready` kiểm tra health đã đăng ký, gồm MongoDB; 503 readiness không đồng nghĩa process chết.
- Lỗi Mongo chưa xử lý được boundary ánh xạ thành 503 `TTS-MONGO-0001`; kiểm tra DNS/TLS/network, quyền database, tên database và timeout mà không in connection string.
- Kiểm tra document legacy mixed type/null bằng fixture trước khi kết luận dữ liệu hỏng; không sửa production để “thử”.

### Guard Super Admin không giải phóng

- EventId `1291`: mutation bị từ chối vì mutex đã tồn tại.
- EventId `1292`: thao tác release mutex thất bại.
- Document nằm trong `counters` với `_id` chính xác `__ttsmart_v2_superadmin_mutation_guard` và không có TTL.
- Không xóa theo tuổi document. Chỉ xóa thủ công sau khi chắc chắn không còn owner hoạt động, dùng điều kiện `_id` + `owner` theo quy trình trong [runbook rollback](ROLLBACK_RUNBOOK.md#guard-mutation-super-admin-bị-orphan).

## Static FE/AD

- FE được host tại `/`; AD tại `/admin` khi `FrontendHosting:Enabled=true` và bundle tương ứng có `index.html`.
- 404 ở route SPA: kiểm tra `CustomerDistPath`/`AdminDistPath`, quyền đọc, working directory và artifact có đúng release.
- API hoặc asset không tồn tại phải trả 404 JSON/static, không được fallback sang HTML. Nếu nhận `index.html`, kiểm tra proxy rewrite và API root.
- UI trắng sau deploy thường là bundle/base path hoặc cache lệch. Đối chiếu asset hash, response content type và base `/admin/`; không xóa dữ liệu Mongo.

## Socket.IO

- Xác nhận proxy cho phép GET/POST/OPTIONS và WebSocket upgrade ở cả `/socket.io` và `/api/socket.io`.
- EventId `4981`: origin không thuộc `Cors:AllowedOrigins`; sửa allowlist đúng origin, không dùng wildcard với cookie.
- `4982`: heartbeat timeout; kiểm tra proxy idle timeout và latency.
- `4983`: session vượt giới hạn outbound queue; kiểm tra client chậm, tải và các giới hạn `Realtime:SocketIo` trước khi tăng trần.
- `4984`/`4985`: delivery/publish best-effort thất bại. Mutation order có thể đã commit; kiểm tra order trước khi retry để tránh event trùng.
- Xác minh bốn event legacy: `order_created`, `order_updated`, `order_cancelled`, `order_deleted`.

## Gemini/AI

- Chưa cấu hình key hợp lệ: endpoint scan/voice trả lỗi cấu hình theo contract; kiểm tra key tồn tại nhưng không in key.
- Khi provider đã được gọi nhưng trả HTTP lỗi, timeout, lỗi transport hoặc response không hợp lệ, API cố ý trả 503 với thông báo an toàn và `X-Error-Code` `TTS-PRODUCT-SCAN-INVOICE-0503` hoặc `TTS-PRODUCT-VOICE-AUDIO-0503`.
- 503 này là fail-closed có chủ đích, không phải lý do đổi sang 200 hoặc trả payload provider thô. Dùng EventId `4601`–`4604` để phân loại.
- Kiểm tra kích thước/MIME/chữ ký file trước provider; lỗi validation là 400, không phải lỗi Gemini.

## SMTP, Telegram, Zalo và notification

- Adapter Zalo OAuth, SMTP OTP/email đơn, Telegram, Zalo notification và scheduler đã có code, nhưng provider thật chưa được xác minh. Không coi test fake là bằng chứng credential/callback/network production đúng.
- SMTP: `4901`–`4903` cho OTP; `4921`–`4923` cho email đơn. Kiểm tra host/port/TLS/sender/recipient và timeout mà không log credential hoặc OTP.
- Telegram: `4701`–`4702`; token nằm trong URL provider nên không bật URI logging thô. Kiểm tra recipient `new_order`, trạng thái enabled và HTTP status đã redact.
- Zalo OAuth: `4801`–`4804`; kiểm tra HTTPS public address/frontend URL, state secret tối thiểu 32 byte, state hết hạn/dùng lại và response size.
- Zalo notification đơn: `4931`–`4936`; kiểm tra credential lưu trong Mongo, refresh CAS và recipient, không in access/refresh token.
- Dispatcher/scheduler: `4911`, `4941`–`4944`. Scheduler giới hạn bốn notification đồng thời và bỏ tác vụ mới khi hết capacity; đơn hàng vẫn có thể đã commit.
- Không retry notification mù quáng. Xác minh order và kênh nào đã gửi để tránh email/tin nhắn trùng.

## Authentication, CSRF và lỗi HTTP

- EventId `1002`/mã `TTS-CSRF-0001`: request mutation bằng cookie không có origin tin cậy. Kiểm tra `Origin`, `Referer`, `Sec-Fetch-Site`, proxy và `Cors:AllowedOrigins`.
- Chỉ `same-origin` được chấp nhận khi browser không gửi Origin/Referer; không nới sang `same-site` để chữa lỗi tạm thời.
- EventId `1003`: JWT hợp lệ về chữ ký nhưng identity không còn tồn tại/hoạt động hoặc token cũ hơn lần đổi password.
- `SEC-H-001` vẫn chặn cutover; `SEC-H-003` đã đóng và phải được tái mở nếu audit AD xuất hiện High/Critical. Xử lý sự cố staging không phải sự chấp thuận bỏ qua gate CSRF.

Nếu dữ liệu toàn vẹn, authorization hoặc khả dụng có rủi ro, dừng retry và dùng [runbook rollback](ROLLBACK_RUNBOOK.md). Danh sách mã/EventId đầy đủ nằm trong [danh mục lỗi](ERROR_CATALOG.md).
