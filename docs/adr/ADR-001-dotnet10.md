# ADR-001: ASP.NET Core 10 / .NET 10

- Trạng thái: Được chấp thuận cho Đợt 1
- Ngày: 2026-08-13

## Bối cảnh

Đợt 1 thay thế backend Node.js/Express legacy, đồng thời giữ lại MongoDB và các client JavaScript/JSX. Kho mã đích ghim SDK `10.0.302` trong `global.json` và đặt framework đích là `net10.0` trong các thuộc tính build dùng chung.

## Quyết định

Sử dụng ASP.NET Core 10 Web API trên .NET 10 cho backend V2. Ghim SDK, quản lý tập trung phiên bản package, bật nullable references/analyzers, coi warning là error và giữ lại các tệp khóa NuGet.

## Các phương án đã xem xét

- Tiếp tục dùng Node.js/Express: bị bác bỏ vì không đáp ứng mục tiêu viết lại của Đợt 1.
- Sử dụng .NET SDK không được ghim hoặc bản preview: bị bác bỏ vì cần khả năng tái lập và hỗ trợ ổn định.
- Sử dụng một bản .NET LTS cũ hơn: bị bác bỏ tại checkpoint migration này vì scaffold đích và kiến trúc được yêu cầu sử dụng .NET 10.

## Hệ quả

Hệ thống đích có được hệ thống kiểu mạnh và middleware ASP.NET tiêu chuẩn, nhưng vẫn cần xử lý khả năng tương thích cho hành vi cookie JWT, password hash legacy, quy tắc viết hoa/thường JSON, biểu mẫu multipart và cấu trúc BSON của MongoDB. ADR này không khẳng định rằng các hành vi đó đã được triển khai.
