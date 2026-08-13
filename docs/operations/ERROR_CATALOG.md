# Danh mục lỗi

Ranh giới API dùng mã `TTS-*`, status HTTP và `X-Correlation-ID` để chẩn đoán mà không trả stack trace/secret. Một số response legacy có envelope riêng; `X-Error-Code` được thêm khi contract/boundary cho phép. Không log token, OTP, connection string, password, owner guard, payload provider hoặc dữ liệu khách hàng đã upload.

## Nhóm lỗi HTTP ổn định

| Nhóm/mã đại diện | Ý nghĩa | HTTP | Ghi chú vận hành |
|---|---|---:|---|
| `AUTH_MISSING`, `AUTH_INVALID` | Không có hoặc session không còn hợp lệ | 401 | Cookie giữ tên `authToken`; EventId `1003` hỗ trợ nhận diện identity stale/missing. |
| `AUTH_FORBIDDEN`, `OBJECT_FORBIDDEN` | Thiếu role/permission hoặc quyền trên object | 403 | Không retry với quyền cao hơn theo cách tự động. |
| `TTS-CSRF-0001` | Mutation bằng cookie không có nguồn gốc trình duyệt tin cậy | 403 | Kiểm tra allowlist/proxy; không nới sang wildcard hoặc `same-site`. |
| `VALIDATION_FAILED`, `OBJECT_ID_INVALID` | Input ngoài allowlist/type/range hoặc ObjectId sai | 400 | Giữ envelope cụ thể theo feature. |
| `NOT_FOUND` | Không có resource/route | 404 | Static/API không tồn tại không được fallback sang SPA HTML. |
| `CONFLICT` | Xung đột version, unique hoặc guard | 409 hoặc envelope tương thích | Guard Super Admin fail closed; xem EventId `1291`–`1292`. |
| `TTS-UPLOAD-*` | Tên, size, MIME, extension, chữ ký hoặc path file không hợp lệ | 400 | File/storage failure sau validation có thể là 500 theo route. |
| `TTS-MONGO-0001` | MongoDB không khả dụng | 503 | `GET /health/ready` cũng có thể trả 503. |
| `TTS-PRODUCT-SCAN-INVOICE-0503` | Gemini scan hóa đơn đã được gọi nhưng thất bại/timeout/response sai | 503 | Có chủ đích, fail closed. |
| `TTS-PRODUCT-VOICE-AUDIO-0503` | Gemini voice đã được gọi nhưng thất bại/timeout/response sai | 503 | Có chủ đích, fail closed. |
| `TTS-USERS-EMAIL-0503` | Email khôi phục không khả dụng | 503 | Không giả lập gửi thành công và không trả OTP. |
| `TTS-ZALO-STATE-0503`, `TTS-ZALO-CONFIG-0503` | State/callback Zalo chưa được cấu hình an toàn | 503 | Provider reject/invalid response có thể dùng 400/502 theo contract callback. |
| `TTS-API-0001` | Request sai định dạng lọt tới boundary | 400 | Không trả chi tiết parser thô. |
| `TTS-API-0000`, `INTERNAL_ERROR` | Lỗi bất ngờ đã redaction | 500 | Tìm correlation ID + EventId; không trả stack trace. |

Thiếu `GEMINI_API_KEY` hợp lệ hiện được route AI trả lỗi cấu hình theo contract; khi provider đã được gọi và thất bại, status 503 là hành vi cố ý. Không đổi lỗi provider thành kết quả 200 rỗng.

## EventId nền tảng, auth và dữ liệu

| EventId | Mức | Ý nghĩa |
|---:|---|---|
| `1000` | Error | Exception boundary ghi request thất bại cùng mã lỗi an toàn và EventId nghiệp vụ. |
| `1001` | Error qua boundary `1000` | Request sai định dạng; được ghi trong trường EventId nghiệp vụ của log boundary. |
| `1002` | Warning | Từ chối mutation cookie từ origin không tin cậy. |
| `1003` | Information | Identity legacy stale/missing hoặc token cũ hơn lần đổi password. |
| `1101` | Information | Đăng nhập legacy thất bại; không ghi credential. |
| `1291` | Warning | Mutation Super Admin bị từ chối vì distributed guard đang được giữ. |
| `1292` | Error | Không giải phóng được guard; cần kiểm tra owner thủ công. |
| `4391` | Warning | Side effect sau commit của đơn khách hàng thất bại. |
| `4392` | Warning | Ghi storage history của sales order thất bại. |
| `4401` | Error | Không lưu được ảnh station. |
| `4402` | Warning | Không xóa được file ảnh station sau cập nhật reference. |
| `4591` | Warning | Ghi storage history của inventory order thất bại. |
| `4691` | Warning | Ghi một ActivityLog thất bại sau mutation. |
| `4692` | Warning | Ghi batch ActivityLog thất bại sau mutation. |
| `4810` | Information | Cache voice vocabulary đã nạp lúc startup. |
| `4811` | Error | Nạp cache voice vocabulary thất bại; runtime tiếp tục bằng defaults. |
| `9001` | Error qua boundary `1000` | MongoDB không khả dụng; response dùng `TTS-MONGO-0001`/503. |

ActivityLog/storage history/realtime/notification có các đường best-effort sau commit. EventId Warning/Error ở các đường này không chứng minh mutation chính thất bại; luôn đọc lại state trước khi retry.

## EventId nghiệp vụ động qua exception boundary

Các application service tạo mã theo `base + HTTP status`. `LegacyExceptionMiddleware` ghi chúng trong trường `EventId` của message EventId `1000`; chúng không phải EventId metadata riêng của `LoggerMessage`.

| Nhóm | Quy tắc | Giá trị hiện dùng |
|---|---:|---|
| Product station scope | `4000 + status` | `4400` (400), `4403` (403) |
| Cart | `4200 + status` | `4600` (400), `4603` (403), `4604` (404), `4609` (409) |
| Sales order | `4300 + status` | `4700` (400), `4703` (403), `4704` (404), `4709` (409) |
| Stock port | `4400 + status` | `4800` (400), `4804` (404), `4809` (409), `4900` (500) |
| Inventory order | `4500 + status` | `4900` (400), `4904` (404), `4909` (409), `5000` (500) |
| Provider settings | `4700 + status` | `5100` (400) |
| Voice vocabulary | `4800 + status` | `5200` (400), `5204` (404), `5209` (409) |
| Inventory rollback | Cố định | `4999` (rollback tồn kho không đầy đủ) |

Một số số trùng giữa các base/status khác nhau, ví dụ `4900`; luôn đọc kèm mã `TTS-*`, module và correlation ID. Không suy luận nguyên nhân chỉ từ số động.

## EventId provider và notification

| EventId | Mức | Ý nghĩa |
|---:|---|---|
| `4601` | Warning | Gemini trả HTTP không thành công. |
| `4602` | Warning | Gemini timeout. |
| `4603` | Warning | Request Gemini lỗi transport. |
| `4604` | Warning | Response Gemini không hợp lệ. |
| `4701` | Warning | Telegram trả HTTP không thành công. |
| `4702` | Warning | Telegram timeout/lỗi transport đã phân loại theo type. |
| `4801` | Warning | Zalo OAuth trả HTTP không thành công. |
| `4802` | Warning | Zalo OAuth timeout. |
| `4803` | Warning | Zalo OAuth lỗi transport. |
| `4804` | Warning | Response Zalo OAuth không hợp lệ. |
| `4901` | Warning | SMTP khôi phục mật khẩu chưa cấu hình. |
| `4902` | Warning | SMTP khôi phục mật khẩu timeout. |
| `4903` | Warning | SMTP khôi phục mật khẩu thất bại. |
| `4911` | Warning | Một kênh notification đơn phát sinh exception đã redact. |
| `4921` | Warning | SMTP notification đơn chưa cấu hình. |
| `4922` | Warning | SMTP notification đơn timeout. |
| `4923` | Warning | SMTP notification đơn thất bại. |
| `4931` | Warning | Zalo notification đơn chưa cấu hình. |
| `4932` | Warning | Refresh/send Zalo notification timeout. |
| `4933` | Warning | Refresh/send Zalo notification lỗi transport. |
| `4934` | Warning | Refresh token Zalo trả HTTP không thành công. |
| `4935` | Warning | Gửi Zalo notification trả HTTP không thành công. |
| `4936` | Warning | Response refresh/send Zalo notification không hợp lệ. |
| `4941` | Warning | Notification bị bỏ vì scheduler bounded không nhận thêm tác vụ. |
| `4942` | Information | Notification bị dừng trong shutdown. |
| `4943` | Warning | Dispatch notification nền thất bại. |
| `4944` | Warning | Chờ notification khi shutdown vượt ngân sách host. |

Zalo OAuth, Gemini, SMTP, Telegram và notification đã có code/test fake hoặc boundary, nhưng provider thật chưa được xác minh. EventId không chứa credential/payload và không thay thế smoke test staging.

## EventId Socket.IO/realtime

| EventId | Mức | Ý nghĩa |
|---:|---|---|
| `4981` | Warning | Từ chối request Socket.IO từ origin không tin cậy. |
| `4982` | Debug | Session đóng do heartbeat timeout. |
| `4983` | Warning | Session vượt giới hạn outbound queue. |
| `4984` | Debug | Delivery event tới session thất bại. |
| `4985` | Warning | Publish event order thất bại sau khi mutation đã commit. |

Socket.IO được mount tại `/socket.io` và `/api/socket.io`; bốn event nghiệp vụ là `order_created`, `order_updated`, `order_cancelled`, `order_deleted`. Xem [khắc phục sự cố](TROUBLESHOOTING.md) để phân loại proxy/origin/queue.
