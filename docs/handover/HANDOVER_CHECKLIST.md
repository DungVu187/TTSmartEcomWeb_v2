# Checklist bàn giao

Checklist này là tài liệu sống. Dấu `[x]` chỉ nghĩa là có bằng chứng ở checkpoint hiện tại; không đồng nghĩa sẵn sàng cutover. `SEC-H-001` vẫn là blocker bắt buộc; `SEC-H-003` đã đóng.

## Phạm vi và source

- [x] Phạm vi Đợt 1 được giữ: Express sang ASP.NET Core 10, tiếp tục MongoDB và frontend JavaScript/JSX.
- [x] Legacy `D:\TTSmartEcomWeb` được quy định chỉ đọc; branch/commit/status baseline đã được ghi nhận.
- [ ] Worktree legacy sạch — worktree đã dirty từ trước; xem [baseline legacy](../migration/LEGACY_BASELINE.md).
- [x] Không commit, push, deploy, kết nối provider production hoặc chạy migration dữ liệu trong checkpoint này.

## API, kiến trúc và contract

- [x] 201/201 handler legacy đã có implementation target.
- [ ] Có positive contract/end-to-end evidence cho từng handler; tổng handler không thay thế bằng chứng từng route.
- [x] Cả route gốc và alias `/api` được giữ trong pipeline khi `LegacyCompatibility:EnableApiPrefixAlias=true`.
- [x] Ranh giới Api/Application/Domain/Infrastructure.MongoDb và dependency direction đã được thiết lập.
- [ ] Review cuối xác nhận mọi controller mỏng, Api không truy cập Mongo trực tiếp và mọi BSON field được map rõ ràng.
- [x] Correlation ID, exception boundary, mã `TTS-*`, structured logging và redaction dùng chung đã có.

## Authentication và bảo mật

- [x] Cookie `authToken`, JWT 12 giờ mặc định, bcrypt legacy và reload identity đã có test.
- [x] Token cũ hơn lần đổi password bị vô hiệu hóa ở boundary identity.
- [x] Mutation Super Admin dùng distributed guard fail-closed trong `counters`.
- [x] Runbook guard orphan ghi đúng `_id=__ttsmart_v2_superadmin_mutation_guard` và chỉ cho xóa thủ công sau khi chắc chắn không có owner hoạt động.
- [ ] `SEC-H-001`: chính sách CSRF-origin được xác minh với reverse proxy/origin deployment thật và được đóng/chấp thuận — **đang chặn cutover**.
- [x] `SEC-H-003`: Vitest được ghim `3.2.6`; AD 205 test, lint, build và audit lại đạt, không còn High/Critical.
- [ ] Mọi finding High khác được đóng hoặc chấp thuận theo quy trình security owner hiện hành.

## MongoDB, file và static frontend

- [ ] 21 collection có fixture BSON/mixed type/null/ObjectId và integration test đủ cho mọi đường ghi quan trọng.
- [x] Validation upload theo size/MIME/extension/chữ ký/containment và route media chính đã có code/test.
- [ ] Consistency Mongo/reference/filesystem và backup/restore volume upload được diễn tập end-to-end.
- [x] API có thể host FE tại `/` và AD tại `/admin` từ hai bundle `dist`; fallback không áp dụng cho API/asset không tồn tại.
- [x] FE có 81 test, lint và production build đạt.
- [x] AD có 205 test bằng Vitest tuần tự có giới hạn, lint và production build đạt.
- [ ] Static FE/AD được smoke test qua reverse proxy/TLS/cache trong staging.

## Provider, notification và realtime

- [x] Adapter/runtime code đã có cho Zalo OAuth, Gemini, SMTP khôi phục mật khẩu, email/Telegram/Zalo notification đơn.
- [x] Notification scheduler bounded và best-effort sau commit đã có test; lỗi kênh không rollback đơn.
- [x] Gemini provider failure được ánh xạ 503 có chủ đích với response an toàn.
- [ ] Zalo OAuth, Gemini, SMTP, Telegram và Zalo notification được xác minh với provider thật bằng credential/recipient staging — hiện **chưa xác minh**.
- [x] Engine.IO v4/Socket.IO v5 được mount tại `/socket.io` và `/api/socket.io` với giới hạn session/payload/queue.
- [x] Bốn event order đã có code/test: `order_created`, `order_updated`, `order_cancelled`, `order_deleted`.
- [ ] Socket.IO được smoke test qua reverse proxy bằng polling, WebSocket upgrade, origin allowlist và client FE/AD staging.

## Build và test

- [x] Backend restore locked-mode/build đã được xác minh tại checkpoint.
- [x] Backend 332/332 test đạt: Unit 231, Contract 53, Integration 16, Security 32.
- [x] FE 81 test và AD 205 test đạt theo lệnh tái lập đã ghi trong runbook.
- [ ] Các dependency test biệt lập bao phủ toàn bộ Mongo/filesystem/provider/realtime quan trọng; số test pass không chứng minh provider production.
- [x] Dependency audit AD không còn finding High/Critical; còn 2 moderate tại `SEC-M-008`.
- [ ] Secret scan chuyên dụng và review artifact/config production đã đạt.

## Vận hành và go/no-go

- [x] Có runbook cấu hình, deployment, rollback, troubleshooting, backup/restore, upgrade và danh mục EventId.
- [ ] Deployment/rollback/backup/restore/upgrade được diễn tập trên staging biệt lập.
- [ ] Health/readiness Mongo, static FE/AD, auth/CSRF, upload, provider và Socket.IO smoke test đạt.
- [ ] Dashboard/cảnh báo dùng correlation ID và EventId cho Mongo, provider, notification, guard và Socket.IO.
- [ ] `SEC-H-001` đã đóng; nếu chưa, quyết định go là không hợp lệ. `SEC-H-003` đã đóng.
- [ ] Phê duyệt production cutover được ghi nhận bởi các owner cần thiết.

## Gói bàn giao

- [x] Tài liệu kiến trúc, migration, security và operations tồn tại bằng tiếng Việt.
- [ ] API contract matrix có bằng chứng endpoint-level cho mọi dòng.
- [ ] Mongo model map có fixture/round-trip evidence cho mọi collection và đường mutation quan trọng.
- [ ] Mọi câu hỏi mở/blocker có owner, severity, trạng thái và bước tiếp theo.
- [ ] Artifact bàn giao không chứa `.env`, credential, dump, upload production, log, backup, `node_modules`, `bin`, `obj` hoặc dữ liệu khách hàng.
- [ ] Checksum backend/FE/AD, cấu hình version, test output và biên bản go/no-go được đính kèm release.

Xem [runbook triển khai](../operations/DEPLOYMENT_RUNBOOK.md), [runbook rollback](../operations/ROLLBACK_RUNBOOK.md) và [danh mục lỗi](../operations/ERROR_CATALOG.md).
