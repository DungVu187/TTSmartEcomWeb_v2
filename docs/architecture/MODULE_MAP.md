# Bản đồ module

| Module | Router legacy | Số handler | Phần triển khai legacy chính | Trạng thái V2 |
|---|---|---:|---|---|
| Users | `/users` | 30 | `be/components/user.js`, `be/controllers/user*.js` | 30 có xử lý; signup/recovery/autologin, role đích/mutex Super Admin, projection `_id` và field/array update hẹp đã có |
| Products | `/products` | 35 | `be/components/product.js`, `be/controllers/product*.js`, `be/services/product*.js` | 35 có xử lý, gồm AI/voice, listing `adjusted`/`stationId` và projection public/private tách biệt |
| Orders | `/orders` | 20 | `be/components/order.js`, `be/controllers/order*.js`, `be/services/order*.js` | 20 có xử lý, gồm media, Socket.IO và notification đơn khách |
| Chips | `/chips` | 18 | `be/components/chip.js` | 18 có xử lý, gồm media ảnh section |
| Chip types | `/chips/types` | 3 | `be/components/chiptypes.js`, `be/controllers/chipTypeOperations.js` | 3 có xử lý |
| Cart | `/carts` | 6 | `be/components/cart.js` | 6 có xử lý thực chất; bảo toàn `_id` của cart item nhúng khi cập nhật |
| Manage | `/manages` | 24 | `be/components/manage.js`, `be/controllers/manage*.js` | 24 có xử lý, gồm bốn route multipart/delete-image và policy `translations.vi/zh/en` với timestamp theo nội dung |
| Import orders | `/iporders` | 17 | `be/components/iporder.js`, `be/controllers/ipOrder*.js` | 17 có xử lý, gồm stock completion, aggregation và media |
| Export orders | `/eporders` | 17 | `be/components/eporder.js`, `be/controllers/epOrder*.js` | 17 có xử lý, gồm stock completion, aggregation và media |
| Stations | `/stations` | 12 | `be/components/station.js` | 12 có xử lý, gồm upload/remove image và search exact không phân biệt hoa thường, dùng AND khi có cả `name`/`code` |
| Storage history | `/histories` | 4 | `be/components/storagehistory.js` | 4 có xử lý thực chất |
| Activity logs | `/activity-logs` | 1 | `be/components/activitylog.js` | 1 route đọc có xử lý với `_id`/nhãn/reference legacy; mutation nghiệp vụ ghi ActivityLog best-effort qua application port |
| Zalo | `/zalo` | 4 | `be/components/zalo.js`, `be/zaloService.js` | 4 có xử lý, gồm OAuth state dùng một lần |
| Telegram | `/telegram` | 6 | `be/components/telegram.js`, `be/telegramService.js` | 6 có xử lý; test-send gọi adapter Telegram có giới hạn |
| Voice vocabulary | `/voice-vocabs` | 4 | `be/components/voicevocab.js`, `be/services/voiceVocabRuntime.js` | 4 có xử lý, có runtime cache và initialization service |

Tổng cộng: 201 handler đã mount. `be/components/drink.js` chứa 13 khai báo nhưng không được `be/index.js` mount; `be/components/producttype.js` không phải là router. V2 có xử lý substantive cho đủ 201 contract method/path đã chuẩn hóa, 0 explicit `501` và 0 absent. API middleware dùng chung, health endpoint, các class document MongoDB/class map, Socket.IO transport và side effect nền được theo dõi riêng, không tính vào số lượng route nghiệp vụ.

Checkpoint backend đầy đủ gần nhất đạt 332/332 test: Unit 231, Contract 53, Integration 16 và Security 32. Con số này là bằng chứng hồi quy theo module/lát cắt, không chứng minh tương đương hành vi cho từng route hoặc mọi đường persistence MongoDB.

## Quyền sở hữu xuyên suốt

- Authentication/authorization: Api + các policy port của Application; sở hữu dùng chung.
- Ánh xạ MongoDB: chỉ thuộc Infrastructure.MongoDb.
- Contract/serialization: Api cùng các DTO của Application.
- Lỗi/log/correlation: hạ tầng dùng chung của Api.
- Adapter upload/provider: Infrastructure cùng các port của application.
- Test: bộ Unit/Contract/Integration/Security của từng module.

## Ranh giới còn mở

- Browser/reverse proxy/staging, provider thật và E2E FE/AD với API V2 cùng MongoDB biệt lập chưa được xác minh.
- Tương thích BSON/ghi chưa phủ đủ 21 collection; xem `docs/migration/MONGODB_MODEL_MAP.md`.
- `SEC-H-001` vẫn mở và chặn cutover. Module map này không phải tuyên bố feature parity hoặc trạng thái sẵn sàng triển khai.

## Baseline SQL Server v1 tách biệt

`database/sqlserver/v1/` là DDL test-only lịch sử của Đợt 2, hiện chỉ gồm ControlPlane và một Operational schema từng được dự kiến dùng lại cho `[TTSmart]`/branch `_online`. Quyết định kiến trúc ngày 2026-08-24 đã chuyển đích sang ba vai trò Platform DB, Company DB và Branch DB với Company/Branch schema riêng. DDL v1 hiện hữu chưa tự động đáp ứng đích mới và không được dùng làm bằng chứng implementation đã phù hợp; xem `SQLSERVER_TARGET_ARCHITECTURE.md`.

Company Shared baseline được đặt tại `database/sqlserver/v1/company/`: runner/test-only, không có dependency runtime. Ownership của nó là Product Master dùng chung cấp Company, catalog/file metadata, CompanySettings, audit Company và metadata migration; route runtime Product hiện vẫn dùng persistence đang cấu hình cho đến khi có lát cắt routing Company DB và test contract riêng.
