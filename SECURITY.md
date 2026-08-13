# Chính sách bảo mật

## Trạng thái được hỗ trợ

TTSmartEcomWeb V2 đang trong Đợt 1 của quá trình migration và chưa được phê duyệt để cutover production. Một finding mức High vẫn đang mở: xác minh CSRF trên staging/proxy/trình duyệt (`SEC-H-001`). Finding phân quyền user đích và dependency phát triển AD mức High đã được xử lý; các tồn dư độ bao phủ và ExcelJS/UUID được theo dõi ở mức Medium. Xem `docs/security/SECURITY_FINDINGS.md`.

## Báo cáo lỗ hổng

Báo cáo riêng lỗ hổng cho chủ sở hữu repository hoặc đầu mối bảo mật được công ty chỉ định. Không mở issue công khai chứa thông tin xác thực, dữ liệu cá nhân, các bước khai thác, token provider, chi tiết MongoDB hoặc URL production. Nêu rõ thành phần bị ảnh hưởng, tác động, điều kiện tái hiện an toàn và biện pháp khắc phục đề xuất. Redact mọi giá trị nhạy cảm.

## Yêu cầu xử lý

- Không commit `.env`, API key, password, hash, JWT/AES key, thông tin xác thực SMTP, token provider, connection string, dump, upload, log hoặc backup.
- Không test với dữ liệu hoặc dịch vụ production.
- Không tự động rotate thông tin xác thực; đề xuất và phối hợp việc rotate với chủ sở hữu.
- Chỉ giữ khả năng tương thích API khi việc đó không yêu cầu tái tạo lỗ hổng mức Critical hoặc High.
- Bản sửa bảo mật làm thay đổi contract bên ngoài cần có quyết định rõ ràng và ghi chú migration.

## Các cổng review tối thiểu

Authentication, authorization, quyền sở hữu đối tượng, CSRF, CORS, rate limiting, cách dựng Mongo query, mass assignment, validation upload, ranh giới provider, redaction lỗi, kiểm kê dependency và rollback không phá hủy phải được review trước mọi quyết định cutover. Security test phải chạy với các dependency biệt lập.
