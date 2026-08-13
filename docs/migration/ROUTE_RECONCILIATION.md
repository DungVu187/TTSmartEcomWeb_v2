# Đối chiếu route

Đối chiếu có tính authoritative của Đợt 1 tính đến 2026-08-13:

- 15 router được mount bởi legacy `be/index.js` expose 201 khai báo HTTP handler. 13 khai báo trong `components/drink.js` chưa mount bị loại vì không phải runtime endpoint.
- V2 có 175 raw attribute `[Http*]`. Mở rộng kế thừa inventory controller cho cả `/iporders` và `/eporders` thêm 17 route đã chuẩn hóa; mở rộng các section storefront có ràng buộc `update-section1..10` thêm 9, đạt 201 contract method/path đã chuẩn hóa.
- So sánh contract method/path đã chuẩn hóa cho kết quả **0 missing** và **0 extra** route.
- Middleware legacy khiến cả URL không prefix và URL có prefix `/api` đều có hiệu lực, nên 201 contract tương ứng 402 effective method/URL forms.

| Module / nhóm route đã mount | Legacy | Substantive | Explicit 501 | Absent |
|---|---:|---:|---:|---:|
| Users (`/users`) | 30 | 30 | 0 | 0 |
| Products (`/products`) | 35 | 35 | 0 | 0 |
| Orders (`/orders`) | 20 | 20 | 0 | 0 |
| Chips + chip types (`/chips`, `/chips/types`) | 21 | 21 | 0 | 0 |
| Cart (`/carts`) | 6 | 6 | 0 | 0 |
| Storefront/manage (`/manages`) | 24 | 24 | 0 | 0 |
| Import orders (`/iporders`) | 17 | 17 | 0 | 0 |
| Export orders (`/eporders`) | 17 | 17 | 0 | 0 |
| Stations (`/stations`) | 12 | 12 | 0 | 0 |
| Storage history (`/histories`) | 4 | 4 | 0 | 0 |
| Activity logs (`/activity-logs`) | 1 | 1 | 0 | 0 |
| Zalo (`/zalo`) | 4 | 4 | 0 | 0 |
| Telegram (`/telegram`) | 6 | 6 | 0 | 0 |
| Voice vocabulary (`/voice-vocabs`) | 4 | 4 | 0 | 0 |
| **Tổng** | **201** | **201** | **0** | **0** |

Các số liệu này xác lập độ phủ route substantive, không xác lập tương đương hành vi hoặc sẵn sàng cutover. `API_CONTRACT_MATRIX.md` vẫn nhóm theo family thay vì một dòng cho mỗi endpoint; test endpoint theo luồng thành công, MongoDB biệt lập, provider và staging vẫn được chọn lọc. Checkpoint backend hiện có 332 test (Unit 231, Contract 53, Integration 16, Security 32).
