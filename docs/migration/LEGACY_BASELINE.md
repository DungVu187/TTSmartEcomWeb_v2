# Đường cơ sở Legacy

## Ghi nhận

- Path: `D:\TTSmartEcomWeb`
- Branch: `TTSmartEcom_Deploy`
- Commit: `c836c8122e5d0e28628235b8e0f44c1c718efb91`
- Survey mode: kiểm tra source chỉ đọc; không cài package, không chạy server, build, test, database, seed, migration hoặc gọi dịch vụ ngoài.
- Initial `git status --short`: 58 entries (các thay đổi có sẵn từ trước; không thuộc task migration này).
- Initial status fingerprint SHA-256: `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d` (tính trên 58 dòng `git status --short` sau khi bỏ khoảng trắng cuối dòng, nối bằng LF và không thêm LF cuối; không lặp lại toàn bộ status để tránh nhầm với công việc không liên quan).
- Final branch/commit/status check: branch và commit không đổi; trạng thái dirty có sẵn vẫn được giữ nguyên. Task này không sửa file legacy nào.

## Số liệu kiểm kê

- 201 mounted HTTP handlers.
- 402 effective method/URL forms vì middleware loại bỏ `/api` trong khi các path không có prefix vẫn được hỗ trợ.
- 13 khai báo route bổ sung trong `components/drink.js` chưa được mount; không phải runtime endpoint.
- 21 MongoDB collections được suy ra.
- 42 mẫu route của FE và 144 mẫu route của AD.

## Ghi chú target/repository

Target repository đang ở `main` với origin `https://github.com/DungVu187/TTSmartEcomWeb_v2.git`. Visibility trên GitHub chưa được xác minh vì không có `gh`. Không thực hiện commit, push hoặc deployment.

## Ranh giới bằng chứng

Đường cơ sở này ghi metadata và số liệu của source, không ghi nhận hành vi production. Không đọc hoặc sao chép giá trị secret, thông tin xác thực database, upload, log hay dữ liệu production. Mọi trường chưa có bằng chứng trực tiếp từ source vẫn là câu hỏi mở.
