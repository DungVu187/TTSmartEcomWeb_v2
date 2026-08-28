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

## Quyết định kiến trúc dữ liệu hiện hành chốt ngày 2026-08-24

Quyết định này thay thế ownership ngày 2026-08-14 về việc không có `CompanyDb` và cho phép catalog độc lập ở từng Branch:

- `[ttsmart.com.vn]` là Platform DB, giữ Company/Branch, internal identity, membership, role/permission, feature/quota, database registry, provisioning và audit platform.
- Mỗi Company có một Company DB dùng chung; `[TTSmart]` giữ vai trò Company DB của chính TTSmart. Company DB sở hữu Product Master/ProductVariant/Brand/Category và cấu hình chung đã được duyệt.
- Product là thực thể chung cấp Company. Branch reference cùng `ProductId`; không tạo Product độc lập theo Branch rồi dùng `Code` để suy đoán quan hệ.
- Mỗi Branch có một Branch DB riêng theo convention `[{CompanyCode}_{BranchCode}_online]`, chứa đơn hàng, nhập/xuất/tồn, Station, file metadata và Activity History riêng. Các Branch DB không query ngang nhau.
- Company DB và Branch DB là hai schema family/version riêng, không được drift tùy tenant. Không có foreign key hoặc transaction nghiệp vụ xuyên database.
- Dashboard cấp Company có thể đọc tổng hợp nhiều Branch; mutation giao dịch luôn phải xác định một Branch cụ thể.
- Activity History chi tiết nằm cùng database với dữ liệu thay đổi: platform tại Platform DB, dữ liệu chung tại Company DB, vận hành Branch tại Branch DB.
- `ad` là một admin app có hai workspace Control Plane và Operational. Company/Branch là scope/entity, không phải role; backend áp dụng đồng thời authentication, membership, scope, permission, feature và resource ownership.
- Platform SuperAdmin có toàn quyền platform/company/branch theo chính sách bypass permission thông thường, nhưng mọi thao tác đặc quyền vẫn phải được audit và không bypass kiểm tra routing/input an toàn.
- Form Company bắt buộc `CompanyCode`. Form Branch có thông tin cơ bản cùng `DatabaseName` và `DatabasePassword`; login name được sinh server-side, password chỉ lưu qua secret manager và Platform DB chỉ giữ `SecretReference`.
- Chưa phát triển local/cloud sync. Không tự tạo cache/projection Product offline hoặc công tắc sync từ quyết định kiến trúc này.

Các câu hỏi triển khai còn mở từ quyết định này, không làm thay đổi ownership đã chốt:

- Company schema và Branch schema version đầu tiên gồm chính xác bảng/cột nào; baseline Operational hai tầng hiện hữu sẽ được supersede theo lộ trình nào?
- Dữ liệu hiện có trong `[TTSmart]` được phân loại và tách sang Company DB/Branch DB nào, đặc biệt Customer, cart/template, storefront, integration và file metadata?
- Khi Branch mất kết nối tới Company DB, nghiệp vụ đọc Product và tạo chứng từ hoạt động theo snapshot/read-only cache hay bị chặn? Đây cần thiết kế offline/sync riêng trước khi triển khai.
- Cơ chế chống tạo trùng Product Master đồng thời dùng normalized code/index và idempotency key nào?
- Dashboard “Tất cả chi nhánh” dùng fan-out trực tiếp, read model hay reporting store nào; timeout/partial failure được biểu diễn ra sao?
- Quy tắc naming cuối cùng cho Company DB và Branch DB, cùng template/release/provisioning migration tương ứng, cần được materialize và test trước khi dùng ngoài database test.

- Migration `008_RemoveSalesTables.sql` đã loại mười bảng Product/Customer rỗng khỏi `[ttsmart.com.vn]` sau preflight; không còn cần quyết định rebuild database tổng.
- Identity authoritative ở Platform DB; còn cần chốt projection/local identity tối thiểu trong Company DB/Branch DB và quy trình reconcile khi vận hành cô lập.
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

- Phase 3A đã khóa fallback theo identifier: cần quyết định chính sách xử lý tài khoản trùng giữa Control Plane và `TTSmart`, cùng quy trình đổi mật khẩu cho internal user (hiện endpoint legacy đổi mật khẩu vẫn dùng Operational profile repository).

- Các bước restore/build/test backend và kiểm tra frontend đã chạy cục bộ tại checkpoint 2026-08-13; xem `MIGRATION_STATUS.md` để biết lệnh và kết quả chính xác.
- Đã chạy integration test MongoDB biệt lập và protocol Socket.IO bằng dữ liệu tổng hợp; không gọi service production, không chạy staging smoke test, deployment, commit hoặc push. Coverage MongoDB/provider cho toàn bộ collection và thao tác ghi vẫn chưa có.
- Lệnh AD Vitest `3.2.6` có giới hạn: Not yet verified ở checkpoint Phase 3A vì chạy quá thời gian xác minh. Backend checkpoint Phase 3A có 362/362 test (Unit 245, Contract 53, Integration 27, Security 37) với SQL Server test cô lập.
- `SEC-H-003` đã đóng; `SEC-H-001` chưa được đóng/chấp thuận. Staging/provider/E2E vẫn chưa xác minh nên không được tuyên bố đạt Definition of Done hoặc sẵn sàng cutover.

## Quyết định phát sinh từ audit ngày 2026-08-24

- File MongoDB archive ở root V2 có phải dữ liệu tổng hợp được phê duyệt không? Owner phải chỉ định nơi lưu, retention và cách xóa an toàn; không được commit/chia sẻ workspace khi chưa xử lý (`SEC-H-004`).
- Resolver nào là nguồn duy nhất để map active Company/Branch sang `DatabaseServer`/`BranchDatabases` và secret reference? Chưa được bật đa chi nhánh trước khi có routing request-scoped và test hai database cô lập (`SEC-H-005`).
- Chính sách thu hồi phiên Control Plane sẽ dùng `SecurityStamp`, session version hay bảng session? `MustChangePassword` phải chặn quyền nào trước khi hoàn tất đổi mật khẩu (`SEC-H-006`)?
- Chuỗi DDL nào là authoritative cho Operational: `database/sqlserver/TTSmart` hay `database/sqlserver/v1/operational`? Cần loại bỏ wildcard destructive và checksum overwrite trước khi chạy ngoài database test (`SEC-H-007`).
- Company v1 mới materialize Product Master baseline; cần profile BSON được phê duyệt để chốt field-level mapping Product/Variant/Category/file, ownership Customer/Supplier/storefront/integration/warehouse và quy tắc bảng giá trước khi thêm `PriceLists`/`PriceListItems` hoặc chạy migration dữ liệu.
- Cần quyết định contract/routing runtime cho việc Branch resolve Product từ Company DB, kiểm tra logical reference và snapshot chứng từ Branch. Lát cắt hiện tại không đổi connection runtime, không tạo Branch schema hay sync/cache.
- Company Admin có được quản lý account/Zalo/Telegram trong Company scope không, và profile/password của Control Plane phải dùng contract mới hay giữ endpoint legacy hiện hữu (`SEC-M-010`)?
