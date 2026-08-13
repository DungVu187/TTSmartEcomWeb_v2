# ADR-004: Khả năng tương thích API legacy

- Trạng thái: Được chấp thuận cho Đợt 1
- Ngày: 2026-08-13

## Bối cảnh

Quá trình rà soát source xác định có 201 HTTP handler đã mount. Một middleware rewrite giúp cả dạng không có prefix và dạng có prefix `/api` đều có hiệu lực, tạo thành 402 tổ hợp method/URL. Các consumer FE và AD phụ thuộc vào quy tắc viết hoa/thường, envelope, cookie, tên multipart và hành vi status hiện có.

## Quyết định

Bảo toàn method, path, tên query/route, tên JSON property, response envelope, status code, tên field multipart và cả hai dạng URL. Không thêm prefix phiên bản hoặc âm thầm “dọn dẹp” một route. Ngoại lệ tương thích phải có mục ghi ADR/trạng thái, lý do bảo mật và đánh giá tác động đến client.

## Các phương án đã xem xét

- Thêm `/api/v1`: bị bác bỏ vì các client legacy không sử dụng prefix này.
- Viết lại client trước: bị bác bỏ vì contract backend là nguồn sự thật của migration.
- Chỉ bảo toàn những route được sử dụng thường xuyên nhất: bị bác bỏ; toàn bộ 201 handler đều thuộc phạm vi.

## Hệ quả

API V2 có thể phải giữ lại những tên khó hiểu và ngữ nghĩa status legacy. Contract test phải bao phủ mọi handler và cả hai dạng prefix khi áp dụng. Một route chưa được coi là “đã port” cho đến khi có bằng chứng về phần triển khai và hành vi phía client.
