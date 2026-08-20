# Khảo sát SQL Server `dangnhap.net` và các database `_online`

## 1. Phạm vi

Khảo sát read-only ngày 2026-08-14 trên SQL Server local `DESKTOP-5O6VV3J\SQLEXPRESS`, SQL Server 2022 Express. Chỉ đọc metadata từ system catalog, row count và page allocation; không đọc row nghiệp vụ, credential hoặc PII, không chạy DDL/DML và không thay đổi database.

Ba database được khảo sát:

- `dangnhap.net`: database tổng hiện hữu;
- `hiephung2_online`: database chi nhánh;
- `petro8_online`: database chi nhánh.

## 2. Snapshot kiến trúc hiện hữu

| Database | Vai trò quan sát | Bảng | Compatibility | Recovery |
|---|---|---:|---:|---|
| `dangnhap.net` | tổng/đăng nhập/quản trị dùng chung | 54 | 120 | Full |
| `hiephung2_online` | vận hành chi nhánh | 43 | 120 | Simple |
| `petro8_online` | vận hành chi nhánh | 44 | 120 | Simple |

`dangnhap.net` có các khái niệm đáng tham khảo: `Company`, `Branch`, `User`, `Role`, `UserRole`, `Function`, `FunctionRole`, `Config`, `Website`, `Product`, `Customer`, `Order` và `Action`. `Branch` còn giữ tên database cùng thông tin kết nối, cho thấy database tổng đang làm registry/routing cho database chi nhánh.

Hai database `_online` có cùng 43 bảng cốt lõi, cùng định nghĩa cột, constraint, function, procedure và trigger. `petro8_online` có thêm duy nhất bảng `NhapKho` 17 cột. Đây là schema drift thực tế cần loại bỏ trong thiết kế mới.

## 3. Điểm có thể tận dụng hoặc tham khảo

### 3.1 Tận dụng ở mức khái niệm

- Một database tổng giữ Company, Branch, identity, role/quyền và registry database.
- Một database vật lý riêng cho từng chi nhánh.
- Các chi nhánh bắt đầu từ cùng một schema nghiệp vụ.
- Dùng ID ổn định để hỗ trợ đồng bộ giữa các database.
- Tách bảng hiện hành và bảng lịch sử/snapshot phục vụ vận hành.
- `SynInfo` thể hiện đúng nhu cầu theo dõi thay đổi để đồng bộ.

### 3.2 Không tái sử dụng trực tiếp implementation

- `dangnhap.net` có 54 bảng nhưng chỉ hai foreign key; toàn bộ index quan sát được ngoài heap chỉ là primary key. Các cột mang nghĩa quan hệ như `CompanyId`, `BranchId`, `RoleId`, `OrderId` phần lớn không được FK/index bảo vệ.
- Hai database `_online` không có foreign key nào.
- Nhiều bảng chi nhánh dùng song song natural integer key và một GUID `ID` unique, làm tăng index/storage nhưng ownership khóa không rõ.
- Dùng nhiều `money`, `real`, `float`, `ntext`, `nvarchar(max)` và `datetime`; không phù hợp cho precision, indexing và contract mới.
- Tên bảng/cột như `User`, `Order`, `Function`, `Status`, `Type`, `TEXT1..5`, `DECIMAL1..3` khó diễn đạt domain và dễ tạo phụ thuộc ngầm.
- Database tổng có các cột `Branch.Username`, `Branch.Password`, provider password/key trong `Config`; thiết kế mới tuyệt đối không lưu credential plaintext, chỉ lưu `SecretReference`.
- `User.Password varchar(50)` và các bảng User riêng trong từng `_online` không nên được dùng làm thiết kế identity mới.
- Trigger đồng bộ của `Function` dùng nested cursor trên từng record/từng Store và bỏ qua khi `SYSTEM_USER='synchronizer'`; chỉ nên giữ ý tưởng change feed, thay implementation bằng transactional outbox.
- Procedure `DELETE_CONSTRAINT_DefaultID` xây dynamic DDL và không gán kết quả vào biến constraint; không tái sử dụng.
- Không có cơ chế schema-version/drift guard, thể hiện qua việc chỉ `petro8_online` có `NhapKho`.

## 4. Bằng chứng về scale và vận hành

`petro8_online` có dữ liệu lịch sử lớn:

| Bảng | Row |
|---|---:|
| `LSCUAVL` | 2.876.611 |
| `LSLOAIVL` | 2.876.612 |
| `LSCHITIETMETRONLSCUAVL` | 1.461.890 |
| `LSTRONLSCUAVL` | 1.414.701 |
| `LSCHITIETMETRON` | 132.900 |
| `LSTRON` | 128.610 |
| `LSDATHANG` | 128.611 |

Data file của `petro8_online` khoảng 899,81 MB và gần đầy; autogrowth hiện là 1 MB. `hiephung2_online` có data file khoảng 408,81 MB nhưng chỉ dùng khoảng 15,56 MB, cho thấy cần chính sách capacity/shrink/archive được quản lý thay vì dựa vào file state tình cờ.

Template mới phải có:

- index theo business key và thời gian trên transaction/history;
- partition hoặc archive theo thời gian cho bảng lớn;
- retention rõ ràng;
- autogrowth theo MB hợp lý, pre-size file và alert dung lượng;
- compression phù hợp edition/môi trường;
- health metadata, backup/restore drill và RPO/RTO theo chi nhánh.

## 5. Quyết định kiến trúc ba loại database

Chủ dự án đã chốt không tạo CompanyDb. Kiến trúc vật lý mục tiêu:

```text
[ttsmart.com.vn]                   1 database tổng
[TTSmart]                          1 database đặc thù TTSmart
[{BranchCode}_online]              N database chi nhánh cùng template
```

Quy ước `{BranchCode}` là mã ổn định không dấu, không khoảng trắng và chỉ chứa ký tự được allowlist; không dùng trực tiếp tên hiển thị do người dùng nhập để tạo identifier SQL.

### 5.1 `[ttsmart.com.vn]`

Giữ:

- Company, Branch và `BranchDatabases`;
- identity, membership, role, permission;
- feature, số dư AI, phiên hỗ trợ và nhật ký hệ thống;
- Product/Customer master dùng chung theo `CompanyId`;
- database provisioning, schema version, drift report và outbox.

Database tổng mới có thể tham khảo logical boundary của `dangnhap.net`, nhưng phải thiết kế lại DDL, constraint, index, secret handling và audit. Không chuyển các bảng website/news/payment/branch transaction vào Platform Core chỉ vì database cũ đang trộn chúng.

### 5.2 `[TTSmart]`

Chỉ dành cho nghiệp vụ/cấu hình riêng TTSmart:

- Station, public link, Station–Product;
- customer–Station membership và order attribution;
- storefront, policy, home section;
- cấu hình provider dưới dạng `SecretReference`;
- cart/order-template hoặc module đặc thù TTSmart nếu còn dùng;
- audit/outbox riêng của extension.

`OrderTram`, `OrderItemTram`, `NhapKho` và `TC_XEVAORA` cũ cung cấp business vocabulary để phân tích, nhưng không được copy nguyên bảng sang PrivateDb.

### 5.3 `[{BranchCode}_online]` từ `BranchDbTemplate`

Mọi BranchDb, kể cả chi nhánh TTSmart, phải có cùng schema/version:

- `DatabaseMetadata`, `SchemaMigrations`;
- Product/Variant projection từ `[ttsmart.com.vn]` và branch price;
- Warehouse, location, stock ledger/balance/reservation;
- SalesOrder, ImportReceipt, ExportIssue và line;
- customer branch profile;
- business audit;
- outbox/inbox/idempotency;
- migration mapping/exception trong thời gian chuyển đổi.

Không được thêm bảng riêng vào một BranchDb. Tính năng tùy chọn dùng cùng schema đã version hóa và được bật qua `CompanyFeatures`/`BranchFeatures`; nếu một module không thể nằm trong template cố định thì phải đặt ở database/module boundary riêng, không sửa Core của một chi nhánh.

### 5.4 Form tạo Company và Branch

Tạo Company:

- `CompanyCode` bắt buộc, chuẩn hóa uppercase và unique;
- thông tin pháp nhân/liên hệ/trạng thái nhập theo form Company;
- `CompanyCode` không đổi khi đổi tên hiển thị.

Tạo Branch:

- nhập các thông tin cơ bản như `BranchCode`, tên, địa chỉ, điện thoại, email, timezone và trạng thái;
- textbox `DatabaseName` được gợi ý `{BranchCode}_online`, cho phép sửa nhưng bắt buộc qua allowlist, kết thúc `_online`, không trùng toàn SQL instance và dài không quá 128 ký tự;
- textbox `DatabasePassword` dùng input type password, không echo lại, không ghi log/telemetry/audit payload;
- SQL Login name được hệ thống sinh từ `BranchId`, không cần textbox thứ ba và không phụ thuộc tên hiển thị;
- password được dùng một lần để tạo/rotate SQL Login, sau đó lưu trong secret manager; `[ttsmart.com.vn]` chỉ lưu `SecretReference`.

SQL Server không có password gắn trực tiếp với database. `DatabasePassword` là password của SQL Login riêng cho BranchDb. Runtime principal chỉ có quyền tối thiểu trong đúng database chi nhánh; principal provisioning/migration tách riêng và không dùng làm runtime credential.

## 6. Cơ chế bảo đảm form cố định

- Một project migration duy nhất sinh `BranchDbTemplate`.
- `DatabaseMetadata` giữ `TemplateId`, `SchemaVersion`, `CompanyId`, `BranchId` và checksum.
- Provisioning chạy `Pending → Creating → Migrating → Seeding → Validating → Active`.
- Request tạo Branch chỉ tạo metadata và secret/provisioning job; không giữ HTTP request mở trong lúc tạo database.
- Branch chỉ chuyển `Active` sau khi tạo database/login/user, áp migration template, seed metadata và kiểm tra kết nối thành công.
- Mỗi lần deploy chạy drift detector qua system catalog; database lệch schema không được nhận migration tiếp theo cho tới khi xử lý.
- Migration áp dụng theo wave/canary và lưu checkpoint từng BranchDb.
- Không dùng script chỉnh tay trên một database chi nhánh.
- Outbox/inbox thay trigger cursor và distributed transaction.
- Backup/restore, health check và schema compatibility là gate trước khi Branch chuyển `Active`.

## 7. Kết luận

Mô hình SQL hiện hữu xác nhận kiến trúc database tổng + database chi nhánh là khả thi và đã vận hành với dữ liệu hàng triệu dòng. Giá trị tái sử dụng nằm ở boundary và vocabulary nghiệp vụ, không nằm ở DDL hiện tại. Thiết kế mới nên giữ ba loại database đã chốt, chuẩn hóa BranchDbTemplate và ngăn schema drift từ đầu.

Đặc tả bảng/cột/index chi tiết của database tổng nằm tại `SQLSERVER_TTSMART_COM_VN_SCHEMA_DESIGN.md`.
