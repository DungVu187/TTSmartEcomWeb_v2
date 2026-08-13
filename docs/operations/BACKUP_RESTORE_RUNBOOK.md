# Runbook sao lưu và khôi phục

## Trạng thái

Chỉ mới lập tài liệu; chưa chạy backup/restore production.

Sao lưu MongoDB, object upload, metadata cấu hình và log vận hành liên quan theo retention policy đã được owner phê duyệt. Mã hóa bản sao lưu, giới hạn quyền truy cập, ghi checksum và kiểm thử khôi phục trong môi trường biệt lập. Không đưa secret vào fixture của repository.

Khảo sát operations đã đối chiếu ghi nhận tham số backup-task không khớp cùng retention/path hardcode. Coi đây là vấn đề chưa giải quyết cho đến khi task contract, retention và đích đến được sửa và kiểm thử. Không chạy task trên production trong migration này.

Quy trình khôi phục: xin cấp quyền, cô lập target, khôi phục vào database/object prefix mới, xác thực số collection và các kiểm tra tổng hợp đại diện, so sánh hành vi đọc API, sau đó quyết định việc promote có an toàn hay không. Ghi lại bằng chứng và điểm rollback.
