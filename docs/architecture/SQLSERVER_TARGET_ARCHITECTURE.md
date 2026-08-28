# Kiến trúc đích SQL Server

## Quyết định kiến trúc hiện hành

Kiến trúc đích là modular monolith, multi-company và multi-branch với ba vai trò database vật lý. TTSmart vừa là chủ platform, vừa là một Company vận hành trên platform.

```text
TTSMART PLATFORM
└── Platform DB: [ttsmart.com.vn]
    ├── Company ABC → Company DB: [ABC]
    │   ├── Branch HN → Branch DB: [ABC_HN_online]
    │   ├── Branch DN → Branch DB: [ABC_DN_online]
    │   └── Branch SG → Branch DB: [ABC_SG_online]
    └── Company TTSmart → Company DB: [TTSmart]
        └── các Branch DB của TTSmart
```

`Company` và `Branch` là scope/entity, không phải role. Platform SuperAdmin là role cấp platform. Company Admin và Employee hoạt động trong membership và scope được cấp.

## Quyền sở hữu dữ liệu

| Vai trò database | Ví dụ | Dữ liệu authoritative |
|---|---|---|
| Platform / ControlPlane | `[ttsmart.com.vn]` | Company, Branch, internal identity, membership, role/permission, feature/quota/AI, database registry, provisioning và audit platform |
| Company Shared | `[TTSmart]`, `[ABC]` | Product Master, ProductVariant, Brand, Category và cấu hình/dữ liệu thực sự dùng chung giữa các Branch của một Company |
| Branch Operational | `[ABC_HN_online]` | Orders, Inventory, Stock, Import/Export, Station, file metadata và hoạt động riêng của một Branch |

Platform DB không chứa Product Master, đơn hàng hoặc Stock. Company DB không gom chứng từ và tồn kho của các Branch. Branch DB không tự tạo một Product độc lập rồi dùng `Code` để suy đoán quan hệ với Product ở Branch khác.

Các Company DB phải dùng chung một Company schema/version. Các Branch DB phải dùng chung một Branch schema/version. Không cho phép schema drift tùy Company/Branch và không gom nhiều Company/Branch vào cùng database nghiệp vụ bằng `TenantId`.

## Product Master và tồn kho Branch

Một sản phẩm là một thực thể dùng chung trong phạm vi Company:

```text
ABC.Products: P001 / SP-A
├── ABC_HN_online → P001 → stock 10
├── ABC_DN_online → P001 → stock 20
└── ABC_SG_online → P001 → stock 30
```

Khi một Branch muốn tạo `SP-A`, application phải:

1. resolve Company và Branch từ trusted identity/scope;
2. kiểm tra Product Master trong Company DB, không query ngang các Branch DB;
3. dùng `ProductId` hiện hữu nếu đã có;
4. chỉ tạo Product Master trong Company DB nếu actor có permission cấp Company phù hợp, ví dụ `product.master.create`;
5. tạo hoặc cập nhật dữ liệu tồn/vận hành tương ứng trong đúng Branch DB.

Không có foreign key hoặc transaction ACID xuyên database. Application kiểm tra logical reference. Chứng từ Branch phải lưu snapshot tên/mã/giá/VAT và dữ liệu lịch sử cần thiết để việc đọc lịch sử không phụ thuộc Product Master hiện tại.

## Routing, dashboard và mutation

Database registry trong Platform DB là nguồn server-side để map active Company/Branch sang Company DB và Branch DB. Client có thể gửi lựa chọn scope cho UX, nhưng backend phải kiểm tra membership, permission, feature và resource ownership trước khi mở connection.

Company dashboard được phép có scope “Tất cả chi nhánh” và tổng hợp dữ liệu đọc từ nhiều Branch qua application/read model. Mọi mutation giao dịch như tạo đơn, nhập kho, xuất kho hoặc điều chỉnh tồn phải xác định chính xác một Branch; không có ghi dữ liệu vào scope “Tất cả chi nhánh”.

Không query trực tiếp Branch DB này sang Branch DB khác. Không có application account `sysadmin`/`db_owner` thường trực. Dynamic database/login identifier chỉ được tạo từ registry/allowlist và `QUOTENAME`; secret chỉ tồn tại qua secret reference/secret manager.

## Authorization và workspace quản trị

Frontend `ad` vẫn là một admin app nhưng có hai workspace:

- **Control Plane**: quản trị toàn platform;
- **Operational**: vận hành Company/Branch.

Platform SuperAdmin có thể chuyển giữa hai workspace. Company Admin/Employee chủ yếu chỉ thấy Operational. Frontend ẩn menu/button chỉ là UX; backend thực thi quyền theo công thức:

```text
ALLOW
= Authentication
∩ Membership
∩ Scope
∩ Permission
∩ Feature
∩ Resource ownership
```

Platform SuperAdmin được bypass permission thông thường nhưng mọi thao tác đặc quyền vẫn phải kiểm tra routing/input an toàn và ghi Activity History. Customer/storefront chưa phải trọng tâm của lát cắt runtime quản trị hiện tại.

## Activity History

Activity History là audit trail nghiệp vụ, không phải technical log. Ownership theo nơi dữ liệu thay đổi:

- Platform DB ghi lịch sử operation và dữ liệu platform;
- Company DB ghi lịch sử Product Master/cấu hình chung Company;
- Branch DB ghi lịch sử đơn hàng, nhập/xuất/tồn, Station và mọi can thiệp vào Branch, kể cả do Company Admin hay Platform SuperAdmin thực hiện.

Không sao chép toàn bộ audit chi tiết Branch lên Company DB. Bản ghi đặc quyền phải đủ actor, Company, Branch, action, resource, thời điểm và before/after khi phù hợp, đồng thời không chứa secret hoặc PII không cần thiết.

## Định danh và lưu trữ

- GUID là khóa nội bộ SQL.
- `PublicId char(24)` lowercase hexadecimal giữ tương thích API đối với entity legacy còn lộ MongoDB ObjectId.
- `Version = 0` hợp lệ cho dữ liệu migrate; `rowversion` là concurrency token SQL riêng.
- Company/Branch schema dùng collation nghiệp vụ đã kiểm thử; `PublicId`, checksum và khóa kỹ thuật cần so sánh nhị phân chính xác.
- Password ứng dụng chỉ lưu adaptive hash; token chỉ lưu hash. SQL Login password và provider secret chỉ được tham chiếu qua secret manager.
- File nằm ngoài SQL Server. Database chỉ giữ metadata, checksum và storage key tương đối đã canonicalize.

## Trạng thái chuyển tiếp của baseline hiện tại

`database/sqlserver/v1/` hiện chỉ materialize hai database test `TTSmart_Control_V1_Test` và `TTSmart_Operational_V1_Test`. Baseline Operational hiện hữu chứa cả catalog lẫn giao dịch và từng được thiết kế để dùng lại cho `[TTSmart]`/Branch DB. Đây là implementation lịch sử trước quyết định ba tầng, không phải bằng chứng kiến trúc đích đã được triển khai.

Runner schema mới vẫn phải kết nối trực tiếp database đích, kiểm tra `DatabaseInfo.DatabaseKind`, dùng version/checksum SHA-256, transaction, `XACT_ABORT`, application lock và phát hiện drift. Không hardcode `USE` database prototype.

Các bước còn phải thiết kế và xác minh riêng gồm:

- tách Company schema và Branch schema có version;
- phân loại/migrate bảng và dữ liệu hiện có trong `[TTSmart]` sang đúng ownership Company/Branch mà không bịa lịch sử;
- routing request-scoped tới cả Company DB và Branch DB;
- xử lý concurrency khi tạo Product Master và logical reference Product giữa database;
- dashboard fan-out/read model, audit ownership và test cô lập nhiều database;
- hành vi khi Branch mất kết nối tới Company DB; không tự thêm sync/cache offline khi chưa có thiết kế được duyệt.

Không chạy DDL/recreate/migration trên `[ttsmart.com.vn]`, `[TTSmart]`, database `_online` hiện hữu hoặc production chỉ vì tài liệu kiến trúc đã được cập nhật.

## Baseline Company v1 (2026-08-24)

`database/sqlserver/v1/company/` là family schema độc lập cho Company Shared, chỉ materialize trên `TTSmart_Company_V1_Test`. Nó sở hữu Product Master (`Brands`, `Categories`, `Units`, `Products`, `ProductVariants`, `ProductCodes`, gán nhiều Category), metadata file ngoài SQL (`Files`, `ProductFiles`), setting Company allowlist, audit Product Master/cấu hình và metadata migration.

Company v1 không chứa identity, role/permission/membership, đơn hàng, nhập/xuất/tồn, Station, lịch sử vận hành Branch, Outbox/Inbox hoặc đồng bộ. Các bảng đó tiếp tục thuộc Platform hoặc Branch theo ownership đã chốt. Baseline Operational hai tầng vẫn là bằng chứng lịch sử, bị supersede về ownership catalog/file/audit Company; nó không biến thành Company schema.
