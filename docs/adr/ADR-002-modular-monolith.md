# ADR-002: Kiến trúc modular monolith

- Trạng thái: Được chấp thuận cho Đợt 1
- Ngày: 2026-08-13

## Bối cảnh

Backend legacy có các hành vi liên quan chặt chẽ đến thương mại, tồn kho, storefront, người dùng, thông báo và media. Đợt 1 yêu cầu feature parity mà không đưa thêm hoạt động vận hành hệ thống phân tán.

## Quyết định

Sử dụng kiến trúc modular monolith với ranh giới project và module rõ ràng:

```text
Api -> Application -> Domain
Api -> Infrastructure.MongoDb -> Application/Domain
```

Các module vẫn được triển khai dưới dạng một process. Domain/Application không phụ thuộc vào kiểu dữ liệu của MongoDB hoặc ASP.NET. Controller điều phối các use case của application; infrastructure sở hữu lớp lưu trữ và các provider adapter.

## Các phương án đã xem xét

- Microservices: bị bác bỏ trong Đợt 1 vì làm tăng chi phí triển khai, tính nhất quán và khả năng quan sát mà không có yêu cầu cụ thể.
- Một project duy nhất không có ranh giới: bị bác bỏ vì sẽ khiến việc rà soát migration và xử lý ranh giới SQL trong tương lai khó khăn hơn.
- Mặc định dùng CQRS/MediatR: được hoãn lại trừ khi một use case cụ thể yêu cầu.

## Hệ quả

Một đơn vị triển khai duy nhất duy trì sự đơn giản trong vận hành và hành vi thời gian thực. Ranh giới rõ ràng là một cổng chất lượng; controller không được truy cập MongoDB trực tiếp. Các transaction xuyên module cần cơ chế bù trừ rõ ràng hoặc một ranh giới được ghi lại trong tài liệu.
