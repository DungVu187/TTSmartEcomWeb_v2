# Ma trận yêu cầu bảo mật

| ID | Yêu cầu | Bằng chứng bắt buộc | Trạng thái |
|---|---|---|---|
| SEC-01 | Repository không chứa secret/dữ liệu production | Review thủ công văn bản đã redact; không có scanner chuyên dụng | Đã hoàn tất review thủ công; chưa chạy scanner |
| SEC-02 | Khả năng tương thích cookie JWT và các cờ bảo mật | Contract test/security test auth | Cookie `authToken`, tải lại identity, thời hạn phiên và vô hiệu hóa sau đổi password đã triển khai; bộ Security đạt 30/30 trong cổng đầy đủ gần nhất |
| SEC-03 | Bảo vệ CSRF cho thao tác ghi bằng cookie | Security test dương tính/âm tính | Origin khớp chính xác đã triển khai: Origin/Referer theo allowlist, fallback chỉ `same-origin`, origin cùng site nhưng khác origin với `same-site` bị từ chối; staging/proxy/trình duyệt còn chưa xác minh |
| SEC-04 | CORS nghiêm ngặt và chính sách proxy đáng tin cậy | Test middleware/review cấu hình | Đã triển khai tại ranh giới dùng chung; việc xác minh proxy deployment còn mở |
| SEC-05 | Authorization theo role/permission/đối tượng | Ma trận endpoint + test | Finding user đích mức High đã đóng bằng policy tập trung, `expectedRole` và guard Super Admin; độ bao phủ dương tính/Mongo/giữa các đối tượng toàn ma trận còn mở ở mức Medium |
| SEC-06 | Allowlist input và phòng vệ mass assignment | Test DTO/fuzz/security | Validation DTO/ranh giới được triển khai có chọn lọc; độ bao phủ fuzz rộng còn mở |
| SEC-07 | An toàn operator/query Mongo | Test repository | Repository dùng type/filter dựng sẵn; Integration đạt cho inventory aggregation, ActivityLog query và mutex Super Admin, nhưng độ bao phủ collection/ghi còn mở |
| SEC-08 | Bảo vệ upload nhiều lớp | Test chữ ký/path/kích thước | Validator, positive contract Product upload và các route media chính (product, catalog, storefront, station, order/inventory) đã có; Mongo/filesystem integration và coverage endpoint rộng còn mở |
| SEC-09 | Timeout, retry và redaction cho provider | Test ranh giới | Gemini, Gmail SMTP, Telegram và Zalo có adapter timeout/cancellation/redaction; OAuth state dùng một lần và notification order chạy best-effort; chưa gọi provider production/staging |
| SEC-10 | Lỗi ổn định đã redact/correlation ID | Danh mục lỗi/test log | Đã triển khai exception/correlation dùng chung; EventId ActivityLog `4691`–`4692` không còn trùng dải Gemini; bằng chứng endpoint diện rộng còn mở |
| SEC-11 | Rate limit và kiểm soát lạm dụng | Cấu hình + test | Đã triển khai giới hạn auth/toàn cục; giới hạn chi phí diện rộng còn mở |
| SEC-12 | Nguồn gốc/audit dependency | Lockfile + bằng chứng audit | NuGet/FE bằng 0; AD runtime và audit đầy đủ còn 2 moderate, 0 high/critical sau khi ghim Vitest `3.2.6`; `SEC-H-003` đã đóng, tồn dư ở `SEC-M-008` |
| SEC-13 | Health endpoint không tiết lộ thông tin nội bộ | Test response | Đã triển khai và được bao phủ bởi contract/integration/security test |
| SEC-14 | An toàn backup/restore và rollback | Bằng chứng diễn tập biệt lập | Chưa xác minh |

Cổng kiểm tra backend đầy đủ gần nhất đạt 332/332 test: Unit 231, Contract 53, Integration 16 và Security 32. Tổng số này là bằng chứng kiểm tra hồi quy tại checkpoint, không thay thế độ bao phủ dương tính/âm tính theo từng yêu cầu trong ma trận.
