# Trạng thái migration

## Snapshot: 2026-08-13

Đợt 1 vẫn đang thực hiện. Đủ 201 route đã đi tới xử lý substantive, nhưng bằng chứng MongoDB/provider/staging/E2E và cổng High `SEC-H-001` vẫn chưa hoàn tất. `SEC-H-003` đã được đóng sau khi nâng/pin Vitest và kiểm tra lại AD.

Kết quả so sánh khai báo mới được ghi trong [ROUTE_RECONCILIATION.md](ROUTE_RECONCILIATION.md); báo cáo 201 contract method-path legacy/V2 đã chuẩn hóa, 0 missing và 0 extra.

| Phạm vi | Kiểm kê legacy | Checkpoint V2 | Xác minh |
|---|---:|---|---|
| HTTP handler đã mount | 201 | 201 substantive, 0 explicit 501, 0 absent | 53 Contract test trong tổng 332 test backend; chưa phải một test cho mỗi route |
| Effective URL forms | 402 | 402 khai báo qua middleware không prefix + `/api` | ba case `/api` đại diện, chưa đầy đủ |
| MongoDB collections | 21 suy ra | 21 document mapping; repository bao phủ các slice đang hoạt động | integration MongoDB biệt lập có dữ liệu tổng hợp; các collection/ghi còn lại chưa phủ toàn diện |
| Mẫu route FE | 42 | giữ source JavaScript/JSX | 81 test, lint và production build pass |
| Mẫu route AD | 144 | giữ source JavaScript/JSX | chạy tuần tự có giới hạn: 205/205 test trên 25 file; build pass; lint exit 0 với 27 warning |
| Event order Socket.IO | 4 | đã triển khai Engine.IO v4/Socket.IO v5 và publisher bốn event | Unit decorator và Integration protocol đạt; chưa staging E2E với AD |
| Nhóm provider ngoài | Gemini, Gmail, Telegram, Zalo | adapter AI/voice, SMTP, Telegram, Zalo OAuth và notification order đã có | fake/isolated test đạt; chưa gọi provider thật hoặc staging |

## Checkpoint module

| Module | Legacy | Substantive | Explicit 501 | Absent | Hạn chế chính |
|---|---:|---:|---:|---:|---|
| Users | 30 | 30 | 0 | 0 | signup/recovery/autologin, target-role guard và audit đã có; chưa test SMTP/provider thật |
| Products | 35 | 35 | 0 | 0 | media, AI/voice, `adjusted` và station scope đã có; chưa provider/Mongo E2E rộng |
| Chips + chip types | 21 | 21 | 0 | 0 | media đã có; thiếu endpoint/Mongo fixture test dương tính |
| Cart | 6 | 6 | 0 | 0 | CAS/visibility và embedded `_id` đã có test chọn lọc; chưa positive Mongo endpoint rộng |
| Storefront/manage | 24 | 24 | 0 | 0 | policy đa ngôn ngữ/timestamp và media đã có test; cần review consistency xóa file/Mongo E2E |
| Sales orders | 20 | 20 | 0 | 0 | media, Socket.IO và notification Gmail/Telegram/Zalo đã có; chưa staging/provider E2E |
| Import orders | 17 | 17 | 0 | 0 | completion stock, aggregate và media đã có; cần Mongo/positive test |
| Export orders | 17 | 17 | 0 | 0 | completion stock, aggregate và media đã có; cần Mongo/positive test |
| Stations | 12 | 12 | 0 | 0 | search exact/AND, media, audit và projection allowlist đã có; còn cần staging/Mongo E2E |
| Storage history | 4 | 4 | 0 | 0 | giới hạn export an toàn 10.000 dòng; chưa có fixture test |
| Activity logs | 1 | 1 | 0 | 0 | đọc và mutation audit best-effort đã có; integration Mongo còn chọn lọc |
| Zalo | 4 | 4 | 0 | 0 | OAuth state một lần và order sender đã có; chưa provider thật |
| Telegram | 6 | 6 | 0 | 0 | adapter gửi thử và notification order đã có; chưa provider thật |
| Voice vocabulary | 4 | 4 | 0 | 0 | runtime cache và initialization service đã có; policy seed lúc cutover còn mở |
| **Tổng** | **201** | **201** | **0** | **0** | Số liệu là trạng thái route substantive, không phải tuyên bố tương đương |

## Nền tảng đã triển khai

- Solution modular-monolith ASP.NET Core 10 gồm project Domain, Application, Mongo Infrastructure và Api.
- Cookie JWT, reload identity Mongo trực tiếp, vô hiệu hóa sau đổi mật khẩu, tương thích bcrypt và policy permission động.
- Rewrite tương thích `/api`, JSON camel-case, correlation ID, mapping exception đã redaction, CORS allowlist, rate limit và health endpoint liveness/readiness.
- Mapping tường minh cho toàn bộ 21 collection suy ra, có giữ extra element.
- Slice repository/application/controller cho users, product/catalog, cart, storefront, orders, inventory orders, stations, histories, ActivityLog, provider settings và voice vocabulary.
- Toàn bộ 201 contract method/path legacy có xử lý substantive, gồm alias `/api`; không còn explicit 501 hoặc absent.
- AI/voice Products, Zalo OAuth, Socket.IO, notification đơn khách, ActivityLog mutation, runtime voice-vocabulary và product listing `adjusted`/`stationId` đã có code cùng test cô lập tương ứng.
- Projection legacy `_id` cho order/user/address/template/cart, policy storefront đa ngôn ngữ/timestamp, station search exact và public product pricing redaction đã được bổ sung; mutation user dùng field/array update Mongo để giảm lost-update giữa các concern.
- Product document giữ `_id` hợp lệ qua update; mutation address/order-template dùng compare-and-exchange có thử lại. Reverse proxy chỉ nhận forwarded headers từ IP/CIDR được cấu hình tin cậy và giới hạn số hop.

## Bằng chứng xác minh

Các lệnh backend đã chạy tại checkpoint code cuối này:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode --disable-build-servers
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore -m:1 --disable-build-servers
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore -m:1 --disable-build-servers --verbosity minimal
```

Kết quả:

- Build: pass, 0 warning, 0 error trên bốn project source và bốn project test.
- Test: 332/332 pass - Unit 231, Contract 53, Integration 16, Security 32.
- Project Integration bao phủ pipeline API, protocol Socket.IO và các luồng MongoDB biệt lập được chọn. Đây chưa phải bằng chứng MongoDB runtime rộng cho toàn bộ collection/ghi hoặc staging E2E.
- `git diff --check`: không báo lỗi, nhưng target chưa có commit và toàn bộ source còn untracked nên lệnh này không kiểm tra nội dung untracked. Kiểm tra trực tiếp 40 file Markdown xác nhận UTF-8 hợp lệ, không trailing whitespace, fence cân bằng, số cột bảng nhất quán và không có liên kết local hỏng.

Bằng chứng frontend target đã chạy:

- FE `npm ci`, 81 test/12 suite, lint và production build: pass.
- AD `npm ci` đã pass trước đó; production build được chạy lại tại checkpoint này và pass (chỉ có advisory chunk lớn).
- Test AD: 205/205 pass trên 25 file với Vitest ghim chính xác `3.2.6`, sau `npm ci`, bằng `npm test -- --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1`.
- Lint AD: exit 0 với 27 warning có sẵn và không có error.

Bằng chứng audit dependency:

- Audit lỗ hổng NuGet: zero finding.
- `npm audit` FE: zero finding.
- `npm audit --omit=dev` và audit toàn cây AD: 2 finding moderate trên `exceljs -> uuid`, 0 high/critical. `SEC-H-003` đã đóng; tồn dư được theo dõi tại `SEC-M-008`.
- Kiểm tra thủ công pattern secret/key/token và artifact loại trừ `node_modules`, `dist`, `bin`, `obj`, `.vs` không thấy credential hoặc dữ liệu production; các hit còn lại là placeholder/synthetic test. `gitleaks`, `trufflehog` và `detect-secrets` không khả dụng, nên đây không phải kết quả từ secret scanner chuyên dụng.

## Công việc đang chặn

1. Đóng hoặc chấp thuận `SEC-H-001` bằng kiểm chứng CSRF với topology deployment, reverse proxy và trình duyệt thật.
2. Mở rộng fixture/integration MongoDB biệt lập cho tương thích BSON, CAS, stock và compensation của các collection/route còn lại; không dùng database production.
3. Chạy test provider thật trong môi trường an toàn cho Gemini, SMTP, Telegram và Zalo OAuth/notification; không dùng secret hoặc recipient production.
4. Xác minh FE và AD E2E với API V2 cùng dependency biệt lập, gồm bốn event Socket.IO, station scope và luồng order notification.
5. Chạy smoke test staging, xác minh reverse proxy/static/upload/CSRF/realtime và hoàn tất runbook rollback/restore.

## Trạng thái repository và phạm vi

- Legacy vẫn ở `TTSmartEcom_Deploy` tại `c836c8122e5d0e28628235b8e0f44c1c718efb91`; status vẫn có 58 entry và fingerprint `307dc6b214efa163c1d87cd461549530e1bd7f63b7cc8746c5963a7b89e1749d`.
- Target là `main`, origin là `https://github.com/DungVu187/TTSmartEcomWeb_v2.git`, mọi file vẫn chưa commit/untracked tại checkpoint này.
- Visibility GitHub vẫn chưa xác minh vì không có `gh`.
- Không thực hiện commit, push, deploy, kết nối database/provider production, SQL Server, Entity Framework Core hoặc migration JavaScript-to-TypeScript.
