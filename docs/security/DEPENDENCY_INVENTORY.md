# Kiểm kê dependency

## Phiên bản tập trung của backend đích

Các phiên bản dưới đây được đọc từ `Directory.Packages.props`; audit lỗ hổng NuGet chỉ đọc đã hoàn tất mà không có finding nào đối với các project đích.

| Package | Phiên bản | Mục đích | Trạng thái |
|---|---:|---|---|
| `MongoDB.Driver` | 3.11.0 | Driver MongoDB | Đã cố định; không có finding lỗ hổng NuGet |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.11 | Middleware JWT | Đã cố định; không có finding lỗ hổng NuGet |
| `Microsoft.AspNetCore.OpenApi` | 10.0.11 | Hỗ trợ OpenAPI | Đã cố định; không có finding lỗ hổng NuGet |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.11 | Hỗ trợ test host | Đã cố định; không có finding lỗ hổng NuGet |
| `BCrypt.Net-Next` | 4.2.1 | Xác minh password legacy | Đã cố định; không có finding lỗ hổng NuGet |
| `xunit` | 2.9.3 | Test | Đã cố định; không có finding lỗ hổng NuGet |
| `xunit.runner.visualstudio` | 3.1.5 | Test runner | Đã cố định; không có finding lỗ hổng NuGet |
| `Microsoft.NET.Test.Sdk` | 18.8.1 | SDK test | Đã cố định; không có finding lỗ hổng NuGet |
| `coverlet.collector` | 10.0.1 | Thu thập độ bao phủ | Đã cố định; không có finding lỗ hổng NuGet |

## Dependency runtime legacy (kiểm kê source)

Các tích hợp Express/Mongoose/JWT/cookie/CORS/Helmet/Multer/Socket.IO/Nodemailer/Gemini/Telegram/Zalo được ghi nhận là input tương thích, không phải dependency V2. Phiên bản package legacy nằm trong lockfile source chỉ đọc và không được cài đặt hoặc thực thi trong tác vụ này.

## Bằng chứng audit frontend

- FE `npm audit`: 0 finding.
- AD `npm audit --omit=dev`: 2 finding moderate, 0 high, 0 critical. Chuỗi còn lại là `exceljs@4.4.0 -> uuid@8.3.2`.
- AD `npm audit` đầy đủ sau khi nâng Vitest: cũng còn 2 finding moderate, 0 high và 0 critical.
- `vitest` được ghim chính xác ở `3.2.6`; cây test dùng Vite `6.4.3` và esbuild `0.25.12`. Sau `npm ci`, AD đạt 205/205 test, lint thoát 0 với 27 warning và production build đạt.
- `npm` chỉ đề xuất hạ phiên bản gây phá tương thích xuống `exceljs@3.4.0` để xử lý tự động chuỗi ExcelJS/uuid; không chấp nhận việc hạ phiên bản này khi chưa review tương thích.
- Không chạy bản sửa audit tự động hoặc cưỡng bức.

## Công việc tiếp theo bắt buộc

Finding High của cây Vitest đã được xử lý và đóng. Trước khi phát hành, tiếp tục theo dõi ExcelJS/UUID mức moderate, review đường gọi export Excel và nâng khi upstream có bản tương thích; chạy audit chỉ đọc với đúng lockfile và không dùng auto-fix khi chưa review khả năng tương thích.
