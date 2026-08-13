# TTSmartEcomWeb V2

TTSmartEcomWeb V2 là bản thay thế được xây dựng độc lập theo từng đợt cho hệ thống thương mại điện tử và tồn kho hiện tại.

1. Đợt 1: Chuyển Node.js/Express sang ASP.NET Core 10, tiếp tục sử dụng MongoDB và các frontend JavaScript/JSX.
2. Đợt 2: Chuyển MongoDB sang SQL Server trong khi vẫn giữ nguyên API contract của ASP.NET.
3. Đợt 3: Chuyển JavaScript/JSX sang TypeScript/TSX trong khi vẫn sử dụng ASP.NET Core và SQL Server.

Tại checkpoint này của repository, chỉ Đợt 1 thuộc phạm vi.

## Checkpoint hiện tại

Kiểm kê legacy đã đối chiếu gồm 201 handler đã được mount, 402 dạng URL có hiệu lực, 21 collection MongoDB được suy luận, 42 mẫu route FE và 144 mẫu route AD.

V2 hiện có xử lý substantive cho đủ 201/201 contract method/path của legacy; không còn route explicit `501` hoặc route absent. Middleware tương thích `/api` giúp cả hai dạng URL có hiệu lực đối với toàn bộ 201 contract. Các phần đã triển khai ở mức code và test checkpoint gồm AI/voice Products, Zalo OAuth, bốn sự kiện đơn hàng Socket.IO, notification đơn khách qua Gmail/Telegram/Zalo, ActivityLog mutation, runtime voice-vocabulary, và product listing tương thích `adjusted`/`stationId`. Đây vẫn không phải tuyên bố tương đương hoàn toàn: coverage MongoDB/provider/staging và E2E với FE/AD còn chọn lọc, và `SEC-H-001` chưa được đóng. Không sử dụng repository này để deploy hoặc cutover.

Xem [trạng thái migration](docs/migration/MIGRATION_STATUS.md), [ma trận API contract](docs/migration/API_CONTRACT_MATRIX.md) và [các finding bảo mật](docs/security/SECURITY_FINDINGS.md).

Bằng chứng số lượng route có thể tái lập nằm trong [ROUTE_RECONCILIATION.md](docs/migration/ROUTE_RECONCILIATION.md).

## Bố cục repository

```text
backend/
  src/
    TTSmartEcom.Api/
    TTSmartEcom.Application/
    TTSmartEcom.Domain/
    TTSmartEcom.Infrastructure.MongoDb/
  tests/
    TTSmartEcom.UnitTests/
    TTSmartEcom.ContractTests/
    TTSmartEcom.IntegrationTests/
    TTSmartEcom.SecurityTests/
fe/                         customer React/Vite JavaScript frontend
ad/                         admin React/Vite JavaScript frontend
docs/                       architecture, migration, operations and security
```

Chiều dependency:

```text
Api -> Application -> Domain
Api -> Infrastructure.MongoDb -> Application, Domain
```

## Bộ công cụ

- .NET SDK 10.0.302, được cố định bởi `global.json`.
- Node.js 24.16 và npm 11.13 đã được sử dụng cho các lần chạy frontend được ghi nhận.
- Phiên bản NuGet được quản lý tập trung trong `Directory.Packages.props`; nullable reference, analyzer, chế độ coi warning là error và lockfile đều được bật.

## Cấu hình local an toàn

Không bao giờ sử dụng thông tin xác thực production hoặc database MongoDB production để phát triển/test. Cung cấp cấu hình qua user secret hoặc biến môi trường. Các giá trị appsettings được lưu trong repository là placeholder dành cho development.

Tên cấu hình và alias legacy được ghi lại trong [CONFIGURATION_REFERENCE.md](docs/operations/CONFIGURATION_REFERENCE.md).

## Các lệnh backend đã được xác minh

Chạy từ thư mục gốc của repository:

```powershell
dotnet restore .\backend\TTSmartEcomWebV2.slnx --locked-mode
dotnet build .\backend\TTSmartEcomWebV2.slnx --no-restore
dotnet test .\backend\TTSmartEcomWebV2.slnx --no-build --no-restore
```

Kết quả checkpoint đã ghi nhận:

- Restore (`--locked-mode`): đạt.
- Build: đạt với 0 warning và 0 error.
- Test: 332/332 đạt (Unit 231, Contract 53, Integration 16, Security 32).
- Integration hiện bao phủ pipeline API, protocol Socket.IO và một số luồng MongoDB biệt lập bằng dữ liệu tổng hợp. Đây chưa phải bằng chứng tương thích MongoDB runtime cho toàn bộ collection/endpoint hoặc smoke test staging.

Việc chạy API đã được ghi tài liệu nhưng **chưa được xác minh như một quy trình end-to-end với MongoDB**:

```powershell
dotnet run --project .\backend\src\TTSmartEcom.Api\TTSmartEcom.Api.csproj
```

## Lệnh và bằng chứng frontend

Frontend khách hàng:

```powershell
cd .\fe
npm ci
npm test
npm run lint
npm run build
npm run dev
```

Bốn lệnh FE đầu tiên đã đạt trong lần chạy được ghi nhận: 81 test trong 12 bộ test, lint và production build. `npm run dev` với V2 **chưa được xác minh**.

Frontend quản trị:

```powershell
cd .\ad
npm ci
npm test
npm run lint
npm run build
npm run dev
```

Kết quả AD đã ghi nhận sau khi ghim Vitest `3.2.6`: `npm ci` và production build đạt; production build chỉ còn cảnh báo chunk lớn hiện có của Vite. Lần chạy Vitest tuần tự có giới hạn (`npm test -- --pool=threads --no-file-parallelism --maxWorkers=1 --minWorkers=1`) đạt toàn bộ 205 test trong 25 file. `npm run lint` thoát với mã 0, còn 27 warning hiện có và không có error. `npm run dev` với V2 **chưa được xác minh**.

## Kiểm tra dependency/bảo mật

- Audit lỗ hổng NuGet: 0 finding trong lần chạy được ghi nhận.
- FE `npm audit`: 0 finding.
- AD `npm audit --omit=dev` và audit toàn cây: còn 2 finding moderate qua `exceljs -> uuid`, 0 high/critical. `SEC-H-003` đã đóng sau khi ghim Vitest `3.2.6`, chạy sạch 205 test, lint và build; tồn dư moderate được theo dõi tại `SEC-M-008`.
- Việc rà soát thủ công văn bản source không tìm thấy giá trị secret production; **chưa** chạy công cụ quét secret chuyên dụng.

## Giới hạn phạm vi

- Không sử dụng SQL Server hoặc Entity Framework Core trong Đợt 1.
- Không migration JS/JSX sang TypeScript.
- Không redesign, microservices, queue, Redis hoặc chức năng nghiệp vụ mới.
- Repository không chứa dữ liệu production, secret, upload, dump, backup hoặc log.
- Checkpoint này không bao gồm commit, push, deployment hoặc thay đổi chế độ hiển thị GitHub.

Đọc [AGENTS.md](AGENTS.md) trước khi thực hiện thay đổi và [CONTRIBUTING.md](CONTRIBUTING.md) trước khi chuẩn bị công việc để review.
