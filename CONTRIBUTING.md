# Hướng dẫn đóng góp

## Trước khi thực hiện thay đổi

1. Đọc `AGENTS.md`, các ADR liên quan và `docs/migration/MIGRATION_STATUS.md`.
2. Xác nhận tác vụ thuộc phạm vi Đợt 1 và không đưa vào SQL Server, EF Core, migration TypeScript hoặc hành vi nghiệp vụ mới.
3. Kiểm tra worktree đích và bảo toàn các thay đổi không liên quan.
4. Coi `D:\TTSmartEcomWeb` là chỉ đọc. Không bao giờ chạy server, test, seed, migration hoặc cài package tại đó trong quá trình khảo sát source.

## Quy tắc triển khai

- Làm việc với từng module có phạm vi giới hạn và cập nhật cả ma trận API lẫn ma trận truy cập.
- Giữ nguyên method/path, alias, kiểu chữ JSON, status code, cookie, tên multipart và các trường response mà frontend sử dụng.
- Giữ controller mỏng và đặt persistence trong `Infrastructure.MongoDb`.
- Sử dụng allowlist DTO rõ ràng. Không bao giờ bind trực tiếp tài liệu persistence từ input HTTP.
- Bổ sung mã lỗi ổn định, định danh sự kiện, structured logging và correlation ID mà không trả về chi tiết nội bộ.
- Bổ sung test chứng minh hành vi thay vì độ bao phủ placeholder.

## Xác minh

Chỉ chạy các kiểm tra phù hợp với môi trường. Trình tự backend dự kiến là restore, build không restore, sau đó test không build. Chạy script frontend đúng như được định nghĩa bởi lockfile/package manifest đã sao chép. Đánh dấu lệnh chưa chạy là `Not yet verified` (chưa xác minh).

Trước khi bàn giao:

```powershell
git diff --check
git status --short
```

Review diff để tìm secret, dữ liệu production, output sinh tự động, dump, upload, log, backup và dependency bị trôi phiên bản. Không commit, push hoặc deploy trừ khi được cấp quyền riêng một cách rõ ràng.

## Tài liệu

Cập nhật `CHANGELOG.md`, kế hoạch thực thi sống, trạng thái migration, ma trận API contract, ma trận truy cập endpoint, bản đồ model, danh mục lỗi và mọi runbook bị ảnh hưởng trong cùng một thay đổi. Không bao giờ tuyên bố tương đương, an toàn hoặc sẵn sàng deployment khi chưa có bằng chứng.
