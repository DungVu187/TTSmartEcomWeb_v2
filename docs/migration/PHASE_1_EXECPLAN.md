# Kế hoạch thực hiện Đợt 1

## Mục tiêu

Chuyển toàn bộ hành vi Node.js/Express legacy sang ASP.NET Core 10, đồng thời giữ MongoDB, tính tương thích collection/document, xác thực JWT bằng cookie, upload, tích hợp AI/provider, sự kiện realtime và consumer FE/AD JavaScript/JSX.

## Rào chắn

Legacy chỉ đọc. Không đọc giá trị `.env`, kết nối production, chạy script làm thay đổi dữ liệu, cài package trong legacy, commit, push, deploy, đưa SQL/EF Core vào, chuyển sang TypeScript hoặc thêm chức năng nghiệp vụ không liên quan.

## Checkpoint

1. **Inventory (current):** 201 mounted handlers, 402 effective URL forms, 21 inferred collections, 42 FE và 144 AD consumer route patterns. Đã ghi nhận baseline.
2. **Contracts:** tạo một dòng rõ ràng cho mỗi handler với request, response, status, access, side effect và bằng chứng. Giải quyết xung đột trong `OPEN_QUESTIONS.md`.
3. **Persistence:** triển khai BSON mapping/repository cho từng collection cùng fixture tổng hợp và test tương thích.
4. **Shared infrastructure:** triển khai configuration, correlation ID, lỗi/log có cấu trúc và redaction, cookie JWT, CSRF, CORS, rate limit, chính sách upload, ranh giới timeout provider và tương thích Socket.IO.
5. **Vertical slices:** chuyển Users/Auth, Products/Cart, Orders, inventory orders, storefront/config, stations/attributes/history, sau đó notifications/voice/AI.
6. **Client compatibility:** sao chép có chọn lọc FE/AD JavaScript/JSX và giữ lockfile, base path, hành vi proxy và field response.
7. **Verification:** chạy unit/contract/integration/security test với dependency biệt lập; build FE/AD; thực hiện smoke test cục bộ an toàn.
8. **Handover:** cập nhật status, runbook, threat model, access matrix, dependency inventory, changelog và bằng chứng rollback.

## Cổng Definition of done

Không dùng các cụm “complete”, “feature parity”, “production-ready”, “secure” hoặc “ready for cutover” cho đến khi mọi handler được triển khai và kiểm thử, mọi mapping collection có bằng chứng, finding mức High được đóng/chấp thuận, client chạy với V2 và có đính kèm bằng chứng build/test/vận hành.

## Hành động tiếp theo hiện tại

Target hiện có xử lý substantive cho đủ 201/201 route legacy, 0 explicit `501` và 0 absent. AI/voice Products, Zalo OAuth, bốn sự kiện Socket.IO, notification đơn khách, ActivityLog mutation, runtime voice-vocabulary và product listing `adjusted`/`stationId` đã được triển khai ở mức code/test checkpoint. Backend đạt 332 test (Unit 231, Contract 53, Integration 16, Security 32); FE đạt 81 test và AD đạt 205 test. `SEC-H-003` đã đóng sau khi ghim Vitest `3.2.6`; ưu tiên tiếp theo là đóng hoặc chấp thuận `SEC-H-001`, mở rộng fixture MongoDB/provider, chạy E2E FE/AD và smoke test staging an toàn. Không kết nối MongoDB hoặc provider production ngoài dependency test cô lập.
