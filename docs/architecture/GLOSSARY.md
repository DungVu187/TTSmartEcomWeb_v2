# Bảng thuật ngữ

| Thuật ngữ | Ý nghĩa trong đợt migration này |
|---|---|
| AD | Frontend quản trị React/Vite JavaScript/JSX. Thông tin legacy: cổng 5173, base `/admin/`, API base không có prefix. |
| API alias | Dạng có prefix `/api` được middleware legacy chấp nhận; prefix bị loại bỏ trước khi router phân phối request. |
| Application | Project V2 chứa các use case, DTO, validation và policy port; project này không được phụ thuộc vào kiểu dữ liệu của MongoDB driver. |
| AuthToken | Tên cookie HTTP-only legacy `authToken` mang session JWT có thời hạn 12 giờ. |
| BSON | Dạng biểu diễn document MongoDB mà V2 phải ánh xạ rõ ràng. |
| Contract test | Kiểm thử method/path, request, response, status, quyền truy cập và side effect theo contract tương thích với legacy. |
| Correlation ID | Định danh request/thao tác được truyền xuyên qua log, lỗi, lời gọi provider và sự kiện thời gian thực. |
| FE | Frontend khách hàng React/Vite JavaScript/JSX. Thông tin legacy: cổng 3000, proxy `/api`, `VITE_BACK_END`. |
| Infrastructure | Project V2 sở hữu ánh xạ BSON MongoDB, repository, lưu trữ tệp và adapter cho provider bên ngoài. |
| Legacy | Hệ thống Node.js/Express chỉ đọc tại `D:\TTSmartEcomWeb`. |
| Handler đã mount | Khai báo route được import và mount bởi `be/index.js`, được tính trong danh mục 201 handler. |
| Authorization cấp object | Authorization dựa trên quyền sở hữu hoặc mối quan hệ với một object order, review, station, product hoặc user cụ thể. |
| Đợt 1 | Chuyển Express sang ASP.NET Core 10 trong khi giữ lại MongoDB và các frontend JS/JSX. |
| Đợt 2 | Kế hoạch chuyển MongoDB sang SQL Server; nằm ngoài phạm vi hiện tại. |
| Đợt 3 | Kế hoạch chuyển JS/JSX sang TypeScript/TSX; nằm ngoài phạm vi hiện tại. |
| V2 | Kho mã đích này tại `D:\TTSmartEcomWeb_v2`. |
| Trạng thái xác minh | `Documented` chỉ có nghĩa là có bằng chứng từ source/thiết kế; `Locally verified`, `Staging verified` và `Production verified` cần có bằng chứng thực tế. |
| Route chưa mount | Khai báo route có trong source nhưng không thể truy cập khi runtime; `drink.js` có 13 khai báo như vậy. |
