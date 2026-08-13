# Runbook triển khai

## Trạng thái và bằng chứng hiện tại

Runbook này chưa được diễn tập trên staging hoặc production. Checkpoint local hiện có:

- 201/201 handler legacy đã có implementation target; con số này không thay thế positive contract/end-to-end coverage cho từng route.
- Backend 332/332 test: Unit 231, Contract 53, Integration 16, Security 32.
- FE 81 test; lint và production build đạt.
- AD 205 test bằng Vitest tuần tự có giới hạn; lint và production build đạt.
- Zalo OAuth, Gemini, SMTP, Telegram và notification đơn hàng đã có code/test bằng fake hoặc dependency biệt lập, nhưng chưa gọi provider thật.
- Static hosting FE tại `/`, AD tại `/admin`, cùng Socket.IO tại `/socket.io` và `/api/socket.io` đã có trong pipeline.

Finding `SEC-H-001` vẫn chặn cutover. `SEC-H-003` đã đóng sau khi ghim Vitest `3.2.6`; không dùng các tổng số trên để tuyên bố hệ thống sẵn sàng production. Xem [các finding bảo mật](../security/SECURITY_FINDINGS.md).

## Điều kiện tiên quyết

- `SEC-H-001` đã được kiểm thử với origin/reverse proxy dự kiến và được đóng hoặc chấp thuận bằng văn bản.
- Giữ `SEC-H-003` ở trạng thái đã đóng: lockfile phải ghim Vitest `3.2.6`, audit không được tái xuất hiện High/Critical; không chạy `npm audit fix` tự động trên release.
- Backup/restore Mongo và volume upload đã được diễn tập trong môi trường biệt lập.
- Có artifact backend, FE và AD bất biến, checksum và commit nguồn tương ứng.
- Có database, upload root, credential và callback riêng cho staging; không dùng dữ liệu/secret production để kiểm thử.
- Owner xác nhận chiến lược traffic, TLS, WebSocket upgrade, sticky/session behavior nếu proxy yêu cầu, log retention và ngân sách shutdown.
- Có checkpoint rollback đã phê duyệt theo [runbook rollback](ROLLBACK_RUNBOOK.md).

## Tạo và kiểm tra artifact

Từ root repository, dùng SDK/lockfile đã ghim:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore

Push-Location .\fe
npm ci
npm test
npm run lint
npm run build
Pop-Location

Push-Location .\ad
npm ci
npx vitest run --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1
npm run lint
npm run build
Pop-Location
```

Không tiếp tục nếu số test thấp hơn checkpoint 332 backend (231/53/16/32), 81 FE hoặc 205 AD mà chưa có giải thích và phê duyệt. Publish backend bằng pipeline release đã duyệt; không đưa `bin`, `obj`, `node_modules` hoặc artifact local vào Git.

## Cấp cấu hình staging

1. Cấp các key theo [tham chiếu cấu hình](CONFIGURATION_REFERENCE.md), ghi đè placeholder JWT/Mongo.
2. Đặt `Cors:AllowedOrigins` đúng các origin trình duyệt thực tế; không wildcard với cookie.
3. Nếu có reverse proxy, bật `ReverseProxy:Enabled`, cấu hình đúng `KnownProxies`/`KnownNetworks` và `ForwardLimit`; startup phải từ chối cấu hình bật nhưng không có forwarder tin cậy.
4. Trỏ `FrontendHosting:CustomerDistPath` và `FrontendHosting:AdminDistPath` đến hai thư mục `dist` bất biến.
5. Mount `Uploads:RootPath` trên volume bền vững với quyền tối thiểu và backup riêng.
6. Đặt `ExternalServices:PublicAddress`, `ExternalServices:FrontendUrl` và callback Zalo bằng HTTPS.
7. Chỉ cấp credential provider staging sau khi test không-provider đã đạt. Không in giá trị khi kiểm tra.
8. Kiểm tra collection `counters` không có guard Super Admin orphan trước khi mở mutation; không xóa guard đang có owner hoạt động.

## Triển khai và smoke test

1. Freeze release, ghi checksum backend/FE/AD, cấu hình version và thời điểm triển khai.
2. Xác nhận backup Mongo và upload hoàn tất; ghi rõ restore point.
3. Deploy một instance canary, chưa mở traffic mutation ngoài nhóm kiểm thử.
4. Kiểm tra `GET /health/live` trả 200 và `GET /health/ready` chỉ trả 200 khi Mongo khỏe.
5. Kiểm tra FE `/` và route SPA sâu; kiểm tra AD `/admin/` và route SPA sâu. Một API/asset không tồn tại phải trả 404, không trả nhầm `index.html`.
6. Smoke test một tập route đại diện ở cả path gốc và `/api`; xác nhận cookie `authToken`, `X-Correlation-ID`, permission và CSRF-origin.
7. Kiểm tra upload bằng dữ liệu tổng hợp, quyền đọc invoice, content type, volume persistence và rollback reference/file.
8. Kiểm tra Engine.IO polling và WebSocket upgrade ở cả `/socket.io` và `/api/socket.io`, origin không tin cậy bị từ chối, rồi xác minh bốn event `order_created`, `order_updated`, `order_cancelled`, `order_deleted` bằng đơn tổng hợp.
9. Với Gemini, xác minh riêng ba trạng thái: chưa cấu hình, thành công qua provider staging và provider lỗi. Lỗi provider runtime phải trả 503 cùng mã `TTS-PRODUCT-SCAN-INVOICE-0503` hoặc `TTS-PRODUCT-VOICE-AUDIO-0503`; đây là fail-closed có chủ đích.
10. Với SMTP, Telegram và Zalo, dùng tài khoản/recipient staging. Xác minh timeout, redaction, retry/refresh token và việc lỗi notification không rollback đơn đã commit.
11. Theo dõi EventId trong [danh mục lỗi](ERROR_CATALOG.md), độ trễ Mongo, saturation notification/Socket.IO, 4xx/5xx và lỗi client.
12. Chỉ tăng traffic sau khi canary ổn định và có phê duyệt go/no-go.

## Tiêu chí dừng hoặc rollback

Rollback khi có sai contract quan trọng, lỗi authorization/CSRF, mất hoặc ghi sai dữ liệu, static bundle không khớp backend, queue Socket.IO quá tải kéo dài, provider làm lộ dữ liệu, hoặc tỷ lệ 5xx vượt ngưỡng đã phê duyệt. Không promote production khi `SEC-H-001` còn mở hoặc `SEC-H-003` tái xuất hiện, ngay cả khi mọi build/test local đều đạt.
