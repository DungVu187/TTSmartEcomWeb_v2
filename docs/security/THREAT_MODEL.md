# Mô hình đe dọa

## Tài sản

- Tài khoản người dùng, password hash, cookie auth, token autologin, địa chỉ, đơn hàng, tồn kho, hóa đơn, document sản phẩm, media đã upload, document MongoDB, thông tin xác thực provider và log vận hành.

## Ranh giới tin cậy

1. Trình duyệt FE/AD tới API (cookie, CORS, CSRF, multipart, JSON).
2. API tới MongoDB (query, ánh xạ BSON, authorization, update inventory nguyên tử).
3. API tới filesystem/object storage (upload, phục vụ static, kiểm tra xóa/reference).
4. API tới Gemini, Gmail, Telegram và Zalo (thông tin xác thực, payload, timeout/retry, callback).
5. API tới client quản trị Socket.IO (cookie handshake, authorization sự kiện, redaction payload).
6. Ranh giới deployment/configuration (key môi trường, proxy header, backup, log).

## Đe dọa và biện pháp kiểm soát

| Đe dọa | Bằng chứng/rủi ro legacy | Biện pháp kiểm soát V2 bắt buộc |
|---|---|---|
| Đánh cắp/giả mạo phiên | Cookie JWT với cấu hình secret | HttpOnly/Secure/SameSite, rotation, expiry, thu hồi khi đổi password, log đã redact |
| CSRF | Thao tác ghi được authentication bằng cookie; dependency `csurf` không được sử dụng | Chiến lược token/origin CSRF rõ ràng và test |
| Authorization đối tượng bị lỗi | Order/review/media/station có phạm vi theo đối tượng | Chính sách tập trung + test quyền sở hữu |
| NoSQL injection/mass assignment | Trường/route legacy động | Allowlist DTO, filter có type, từ chối operator |
| Lạm dụng upload | Kiểm tra MIME/extension không đồng nhất; file static public | Kiểm tra chữ ký, kích thước, extension, containment, quarantine/reference |
| Lộ thông tin xác thực provider | Key Gemini/Gmail/Telegram/Zalo | Secret manager, redaction, timeout/cancellation, lỗi an toàn |
| Lạm dụng callback OAuth | Callback Zalo có state cố định và không ràng buộc phiên người dùng | State dùng một lần, ràng buộc với admin/phiên khởi tạo và có thời hạn |
| Mất dữ liệu/race | Bù trừ inventory và update bộ đếm order | Update có version/nguyên tử, idempotency, test rollback |
| Lộ dữ liệu static nhạy cảm | `/documents` public, response tra cứu rộng | Projection public rõ ràng và review authorization |
| Rò rỉ realtime | Sự kiện Socket.IO trong phòng admin | Handshake đã authentication, kiểm tra role, payload sự kiện đã redact |

## Trường hợp lạm dụng cần test

Token bị thiếu/không hợp lệ/hết hạn, sai role/permission, ID đối tượng của người dùng khác, ObjectId sai định dạng, operator Mongo, page/collection/khoảng ngày quá lớn, sort không an toàn, upload traversal/giả MIME/chữ ký không khớp, timeout provider, phát lại callback, gửi order trùng lặp và redaction lỗi.
