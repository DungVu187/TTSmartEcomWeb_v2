# Câu hỏi mở

Các câu hỏi này cần quyết định của owner/product/security hoặc cần bằng chứng không thể có được qua việc kiểm tra source an toàn. Không được tự ngầm đoán.

## Baseline v1 cần quyết định trước khi dùng ngoài database test

- Có chấp thuận RCSI cho luồng tồn kho sau khi có kiểm thử concurrency, hay giữ transaction locking hiện tại?
- Quy trình materialize role template từ ControlPlane sang role thực của Company phải chạy ở application/provisioning nào?
- Chính sách secret manager cho `SecretReferenceId`, recipient reference và storage provider phải được chốt trước khi có dữ liệu thật.
- Operational local identity cần quy trình reconcile nào khi database branch chạy cô lập và sau này có yêu cầu đồng bộ?

## Khoảng trống field-level phát hiện khi rà mapping baseline v1

- Có phê duyệt migration schema version mới cho năm field Product tách biệt `solution`, `features`, `operatingMethod`, `advantages`, `specifications`, cùng `purchaseCount`, `totalRating`, `reviewCount` và `averageReviews` không? Không được gộp chúng vào cột mô tả chung.
- `ProductVariants.earn` có phải là `ProfitPercent` không? Nếu không, cột đích và cách giữ giá trị `buttonCount` dạng chuỗi không parse được là gì?
- Bảng mã chính thức cho boolean `orders.payment`, `iporders.status` và `eporders.status` là gì? Dữ liệu legacy Completed thiếu `completedAt` phải được đánh dấu thế nào khi header hiện không có `RecordOrigin`/`DataStatus` để nới constraint có kiểm soát?
- Ràng buộc nào sẽ bắt buộc `SalesOrderItems` do SQL tạo mới có `ProductName`, `ProductCode`, định danh Variant, `UnitPrice`, `LineTotal` và VAT khi áp dụng? Hiện `RecordOrigin` chỉ ở header và snapshot line chưa được constraint kiểm soát theo nguồn gốc.
- Với nhập/xuất kho, ba giá trị `quantity`, `quantityRe`/`quantityEx`, `stockAppliedQuantity` phải map độc lập vào cột nào? Cần giữ `importPriceSnapshot`, raw price/VAT parse lỗi và timestamp MongoDB header bằng schema nào?
- Có bổ sung model per-Station cho `allowPublicSignup` không? Đích singleton Storefront không thể giữ các giá trị khác nhau theo Station.
- Có phê duyệt các cột Storefront tách biệt cho footer, HomeCategory và Section (locale, type, link, icon, image, display/show flags) thay vì nén vào nội dung chung không?
- `voicevocabs.codeMap` cần model nào để giữ đồng thời code, keyword, brand, type, compact và toàn bộ pattern mà không flatten mất semantic?
- Có bổ sung `MigrationMappings.SourceKey`/`SourceKeyType` cho khóa chuỗi và manifest có cấu trúc để đối soát source database/collection, count, tổng tiền/quantity, disposition, file count, checksum và phiên bản công cụ không?
- Những entity nào phải bổ sung cặp `SourceCreatedAtUtc`/`SourceUpdatedAtUtc`? Không được tái sử dụng timestamp vòng đời SQL như timestamp MongoDB khi API còn trả timestamp nguồn.

## Quyết định kiến trúc dữ liệu đã chốt ngày 2026-08-14

- `[ttsmart.com.vn]` giữ Company/Branch, identity/permission/feature/quota/database registry và provisioning.
- `[TTSmart]` là database bán hàng đầy đủ của công ty TTSmart: Product/Variant, Customer, cart/template, sales/import/export, stock, Station, storefront, voice và integration metadata.
- Mỗi chi nhánh có một `[{BranchCode}_online]` được tạo từ cùng `BranchDbTemplate`; không có `CompanyDb` và không cho phép schema drift theo từng chi nhánh.
- Mỗi chi nhánh có tồn kho và chứng từ riêng, có thể có catalog riêng, không yêu cầu đồng bộ tức thời và ưu tiên khả năng cài local/cô lập dữ liệu. Product authoritative thuộc database bán hàng, không thuộc database tổng.
- Form Company bắt buộc `CompanyCode`. Form Branch có thông tin cơ bản cùng hai textbox `DatabaseName` và `DatabasePassword`; login name được sinh server-side, password chỉ lưu qua secret manager và `[ttsmart.com.vn]` chỉ giữ `SecretReference`.
- Chưa phát triển local/cloud sync. Bản Standalone hoặc Hybrid là phạm vi sau.

Các câu hỏi Đợt 2 còn mở từ quyết định này:

- Migration `008_RemoveSalesTables.sql` đã loại mười bảng Product/Customer rỗng khỏi `[ttsmart.com.vn]` sau preflight; không còn cần quyết định rebuild database tổng.
- Identity authoritative ở database tổng và `Users` projection ở `[TTSmart]`, hay TTSmart tự giữ cả auth?
- Có tạo Customer từ Sales Order không còn User hay chỉ giữ customer snapshot trên đơn?
- Retention/quyền xem đối với 3 `chatmessages` dormant và 383 ActivityLog là gì?
- Có revoke toàn bộ 9 `logInString` tại cutover không?
- Storage đích và quy trình checksum cho ít nhất 258 file reference là gì?
- Supplier, nhiều warehouse và payment transaction chưa có model/dữ liệu hiện tại sẽ nằm ở đợt mở rộng nào?
- Schema rollout/drift repair, partition/retention bảng lịch sử và RPO/RTO từng BranchDb được chốt thế nào?

1. `ADMIN_FULL_ACCESS=true` có nên được giữ trong Đợt 1 hay phải áp dụng permission admin qua một đợt rollout tương thích?
2. Chính sách CSRF Origin/Referer/Fetch Metadata hiện tại có được phê duyệt cho topology deployment dự kiến hay phải chuyển sang synchronizer/double-submit token (`SEC-H-001`)?
3. `/documents` và toàn bộ response tra cứu station public có được phép công khai các field hiện tại hay không?
4. Zalo OAuth state dùng một lần đã triển khai cần được xác minh với callback/provider và topology staging nào trước khi bật cấu hình thật?
5. Những format token AES autologin legacy nào phải tiếp tục đọc được, và ngày loại bỏ compatibility path là khi nào?
6. Cơ chế seed/backfill voice-vocabulary lúc startup đã triển khai có được phép chạy khi cutover hay phải chuyển thành task migration rõ ràng dành cho admin?
7. Index BSON chính xác và bảo đảm uniqueness của toàn bộ 21 collection là gì?
8. Những biến thể null/missing và string-number legacy nào phải được chấp nhận vô thời hạn?
9. Scanner chữ ký upload nào được phê duyệt và kích thước aggregate/request tối đa là bao nhiêu?
10. Semantics retry, timeout và failure nào của provider phải tiếp tục quan sát được từ FE/AD?
11. Chính sách retention và restore có tính authoritative cho upload, invoice, log và backup là gì?
12. Backup-task parameter contract đã sửa là gì? Ghi chú khảo sát cho thấy tham số không khớp và retention/path hardcode cần owner quyết định.
13. Repository GitHub target có ở chế độ private không? `gh` không khả dụng nên chưa xác minh visibility.
14. Deployment topology nào thay thế process legacy? Không tìm thấy PM2, Nginx, Docker hoặc cấu hình CI được theo dõi trong inventory đã đối chiếu.
15. Consumer route FE/AD chính xác nào có tính business-critical cho vertical slice đầu tiên?

## Ghi chú xác minh

- Các bước restore/build/test backend và kiểm tra frontend đã chạy cục bộ tại checkpoint 2026-08-13; xem `MIGRATION_STATUS.md` để biết lệnh và kết quả chính xác.
- Đã chạy integration test MongoDB biệt lập và protocol Socket.IO bằng dữ liệu tổng hợp; không gọi service production, không chạy staging smoke test, deployment, commit hoặc push. Coverage MongoDB/provider cho toàn bộ collection và thao tác ghi vẫn chưa có.
- Lệnh AD Vitest `3.2.6` có giới hạn đã pass 205/205 test. Backend checkpoint mới nhất có 332/332 test (Unit 231, Contract 53, Integration 16, Security 32); FE có 81 test đạt.
- `SEC-H-003` đã đóng; `SEC-H-001` chưa được đóng/chấp thuận. Staging/provider/E2E vẫn chưa xác minh nên không được tuyên bố đạt Definition of Done hoặc sẵn sàng cutover.
