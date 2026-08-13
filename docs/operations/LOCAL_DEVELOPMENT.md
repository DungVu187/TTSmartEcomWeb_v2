# Phát triển cục bộ

## Trạng thái

Được lập tài liệu từ cấu hình repository. Các lệnh restore/build/test và xác minh frontend đã chạy tại checkpoint này; tiến trình API và chuỗi end-to-end có Mongo vẫn **Not yet verified**.

## Điều kiện cần

- .NET SDK `10.0.302` (được ghim bởi `global.json`).
- Node.js 24.16 và npm 11.13 được quan sát cục bộ để phục vụ tương thích frontend.
- Một instance/database MongoDB biệt lập cho phát triển cục bộ.
- Không dùng credential production, upload, dump, log hoặc dữ liệu khách hàng.

## Backend

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build
dotnet run --project .\backend\src\TTSmartEcom.Api\TTSmartEcom.Api.csproj
```

Không đánh dấu lệnh đã xác minh cục bộ cho đến khi lệnh thực sự chạy và output được lưu. Dùng user secrets/biến môi trường; không bao giờ đặt giá trị vào appsettings được theo dõi.

## Frontend

Thông tin legacy cho việc sao chép có chọn lọc sau này: FE là React 18/Vite 6 JavaScript/JSX, port 3000, có proxy `/api` và `VITE_BACK_END`; AD là React 18/Vite 6 JavaScript/JSX, port 5173, base `/admin/`, dùng `VITE_API_URL` không prefix. Giữ npm lockfile v3. Xác minh FE pass (81 test, lint, production build). Xác minh AD pass với lệnh test tuần tự có giới hạn được ghi trong `README.md` (205 test), lint có warning và production build; lệnh Vitest song song mặc định không kết thúc trong môi trường này.

## Khắc phục sự cố an toàn

Dùng tài khoản tổng hợp, collection Mongo test và log đã redaction. Không trỏ cấu hình cục bộ vào production. Dừng nếu lệnh có thể ghi ngoài V2 hoặc làm thay đổi legacy.
