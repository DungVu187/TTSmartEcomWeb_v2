# Câu hỏi mở

Các câu hỏi này cần quyết định của owner/product/security hoặc cần bằng chứng không thể có được qua việc kiểm tra source an toàn. Không được tự ngầm đoán.

1. `ADMIN_FULL_ACCESS=true` có nên được giữ trong Đợt 1 hay phải áp dụng permission admin qua một đợt rollout tương thích?
2. Chính sách CSRF Origin/Referer/Fetch Metadata hiện tại có được phê duyệt cho topology deployment dự kiến hay phải chuyển sang synchronizer/double-submit token (`SEC-H-001`)?
3. `/documents` và toàn bộ response tra cứu station public có được phép công khai các field hiện tại hay không?
4. Zalo OAuth state dùng một lần đã triển khai cần được xác minh với callback/provider và topology staging nào trước khi bật cấu hình thật?
5. Những format token AES autologin legacy nào phải tiếp tục đọc được, và ngày loại bỏ compatibility path là khi nào?
6. Cơ chế seed/backfill voice-vocabulary lúc startup đã triển khai có được phép chạy khi cutover hay phải chuyển thành task migration rõ ràng dành cho admin?
7. Index BSON chính xác và bảo đảm uniqueness của toàn bộ 21 collection là gì?
8. Những biến thể null/missing và string-number legacy nào phải được chấp nhận vô thời hạn?
9. Scanner chữ ký upload nào được phê duyệt và kích thước aggregate/request tối đa là bao nhiêu?
10. Semantics retry, timeout và failure nào của provider phải tiếp tục quan sát được từ FE/AD?
11. Chính sách retention và restore có tính authoritative cho upload, invoice, log và backup là gì?
12. Backup-task parameter contract đã sửa là gì? Ghi chú khảo sát cho thấy tham số không khớp và retention/path hardcode cần owner quyết định.
13. Repository GitHub target có ở chế độ private không? `gh` không khả dụng nên chưa xác minh visibility.
14. Deployment topology nào thay thế process legacy? Không tìm thấy PM2, Nginx, Docker hoặc cấu hình CI được theo dõi trong inventory đã đối chiếu.
15. Consumer route FE/AD chính xác nào có tính business-critical cho vertical slice đầu tiên?

## Ghi chú xác minh

- Các bước restore/build/test backend và kiểm tra frontend đã chạy cục bộ tại checkpoint 2026-08-13; xem `MIGRATION_STATUS.md` để biết lệnh và kết quả chính xác.
- Đã chạy integration test MongoDB biệt lập và protocol Socket.IO bằng dữ liệu tổng hợp; không gọi service production, không chạy staging smoke test, deployment, commit hoặc push. Coverage MongoDB/provider cho toàn bộ collection và thao tác ghi vẫn chưa có.
- Lệnh AD Vitest `3.2.6` có giới hạn đã pass 205/205 test. Backend checkpoint mới nhất có 332/332 test (Unit 231, Contract 53, Integration 16, Security 32); FE có 81 test đạt.
- `SEC-H-003` đã đóng; `SEC-H-001` chưa được đóng/chấp thuận. Staging/provider/E2E vẫn chưa xác minh nên không được tuyên bố đạt Definition of Done hoặc sẵn sàng cutover.
