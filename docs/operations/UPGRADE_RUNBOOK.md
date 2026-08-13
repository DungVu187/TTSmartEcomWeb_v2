# Runbook nâng cấp

## Trạng thái

Quy trình đã được lập tài liệu nhưng chưa diễn tập với môi trường triển khai. SDK hiện được ghim bởi `global.json`; package backend dùng quản lý phiên bản tập trung và lockfile, FE/AD dùng npm lockfile. Không tự động nâng cấp package hoặc hệ thống production trong migration.

Checkpoint hồi quy trước nâng cấp:

| Bộ | Bằng chứng hiện tại |
|---|---:|
| Backend | 332 test: Unit 231, Contract 53, Integration 16, Security 32 |
| FE | 81 test; lint và production build đạt |
| AD | 205 test bằng Vitest tuần tự có giới hạn; lint và production build đạt |
| API | 201/201 handler legacy có implementation target |

Các số này là sàn phát hiện hồi quy, không phải tuyên bố cutover. `SEC-H-001` vẫn chặn cutover; `SEC-H-003` đã đóng bằng Vitest `3.2.6` và phải được giữ không tái xuất hiện.

## Chuẩn bị

1. Ghi SDK, package trực tiếp/transitive, Node/npm, lockfile hash và artifact checksum hiện tại.
2. Đọc release note/advisory chính thức; ghi breaking change, CVE, thay đổi runtime, browser support và database driver.
3. Chọn phạm vi nâng cấp nhỏ nhất có thể kiểm chứng; không trộn nâng cấp framework, Mongo driver, provider và redesign frontend trong một change set.
4. Tạo branch/checkpoint, backup Mongo/upload staging và kế hoạch rollback artifact.
5. Không dùng `dotnet add package`, `npm update`, `npm audit fix` hoặc `npm audit fix --force` trực tiếp trên release mà không review diff/lockfile.

## Xác minh backend

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore
```

- Kỳ vọng tối thiểu 332/332: Unit 231, Contract 53, Integration 16, Security 32, trừ khi change set chủ động thêm test.
- Kiểm tra `dotnet list package --vulnerable --include-transitive` ở chế độ chỉ đọc và lưu bằng chứng đã redact.
- Với MongoDB.Driver, chạy fixture BSON mixed type/null/ObjectId và integration database biệt lập; không probe production.
- Với ASP.NET Core/JWT, kiểm tra cookie `authToken`, CORS/CSRF-origin, `/api` alias, static file, upload và health.
- Với realtime, kiểm tra Engine.IO v4/Socket.IO v5, polling/WebSocket, queue/heartbeat và bốn event order.

## Xác minh FE và AD

```powershell
Push-Location .\fe
npm ci
npm test
npm run lint
npm run build
npm audit --omit=dev
Pop-Location

Push-Location .\ad
npm ci
npx vitest run --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1
npm run lint
npm run build
npm audit --omit=dev
Pop-Location
```

- FE không được thấp hơn 81 test; AD không được thấp hơn 205 test nếu chưa có giải thích/phê duyệt.
- Với AD, bảo đảm `SEC-H-003` không tái xuất hiện; đối chiếu advisory mới, path runtime và breaking change. Tiếp tục theo dõi 2 moderate ExcelJS/UUID tại `SEC-M-008`.
- Xác nhận base path, asset hash và fallback SPA: FE tại `/`, AD tại `/admin`. Không để bundle cũ dùng contract backend mới.

## Provider và side effect

Zalo OAuth, Gemini, SMTP, Telegram, notification và Socket.IO đã có adapter/runtime code; provider thật chưa được xác minh. Khi nâng package HTTP/JSON/mail hoặc đổi provider API:

1. Chạy test fake cho timeout, cancellation, response giới hạn và redaction.
2. Chạy smoke test bằng credential/recipient staging, không dùng production.
3. Giữ hành vi AI fail-closed: lỗi Gemini runtime trả 503 an toàn; không chuyển thành 200 hoặc lộ payload provider.
4. Xác minh notification/realtime vẫn best-effort sau mutation commit và không làm request nghiệp vụ thất bại.
5. Kiểm tra EventId trong [danh mục lỗi](ERROR_CATALOG.md) không bị đổi hoặc tái sử dụng ngoài chủ đích.

## Promote và rollback

1. Deploy canary staging cùng bộ backend/FE/AD bất biến.
2. Chạy health, auth/CSRF, route gốc + `/api`, static/upload, Mongo, provider và Socket.IO smoke test.
3. So sánh contract, latency, 4xx/5xx, EventId và browser console với baseline.
4. Không promote production khi `SEC-H-001` còn mở hoặc `SEC-H-003` tái xuất hiện.
5. Nếu có hồi quy, dùng [runbook rollback](ROLLBACK_RUNBOOK.md); không chỉnh lockfile hoặc database ad-hoc trên host.
6. Cập nhật changelog, dependency inventory, bằng chứng test và quyết định go/no-go sau khi nâng cấp được chấp thuận.
