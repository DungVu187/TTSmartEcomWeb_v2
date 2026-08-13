# Runbook rollback

## Trạng thái

Quy trình đã được lập tài liệu nhưng chưa diễn tập trên staging/production. Rollback ứng dụng không mặc nhiên đồng nghĩa rollback Mongo hoặc volume upload.

## Kích hoạt rollback

1. Tuyên bố incident, dừng promote và ghi `X-Correlation-ID`, EventId, release checksum, cấu hình version, thời điểm bắt đầu và phạm vi traffic.
2. Chặn mutation nếu có dấu hiệu sai authorization/CSRF, mất dữ liệu, ghi tồn kho sai hoặc backend/frontend lệch contract.
3. Giữ log đã redaction và metric; không lưu token, OTP, connection string, payload provider hoặc dữ liệu upload của khách hàng vào ticket.
4. Chỉ định incident owner, database owner và người có quyền chuyển traffic.

## Rollback artifact

1. Chuyển traffic về bộ artifact backend, FE và AD đã được phê duyệt cùng nhau. Không rollback riêng frontend nếu contract backend đã thay đổi.
2. Khôi phục cấu hình version trước nhưng không khôi phục secret đã bị thu hồi hoặc credential nghi bị lộ.
3. Xác nhận root static FE `/`, AD `/admin`, asset hash và fallback SPA thuộc cùng release.
4. Xác nhận reverse proxy vẫn chuyển polling/WebSocket cho `/socket.io` và `/api/socket.io`.
5. Chạy `GET /health/live`, `GET /health/ready`, smoke test auth chỉ đọc, route gốc và `/api`, rồi kiểm tra một kết nối Socket.IO tổng hợp.

## MongoDB và upload

- Không chạy migration ngược, `mongorestore --drop`, xóa collection hoặc ghi sửa hàng loạt theo phản xạ.
- Xác định release có thay đổi document, order/inventory, ActivityLog, provider credential hoặc reference file nào hay không.
- Nếu chỉ artifact lỗi và dữ liệu vẫn tương thích, ưu tiên rollback traffic mà không đụng dữ liệu.
- Chỉ restore Mongo/volume upload từ checkpoint đã phê duyệt sau khi database owner xác nhận phạm vi mất dữ liệu chấp nhận được.
- Khôi phục vào database/object prefix biệt lập trước; đối chiếu số collection, order, stock, upload/reference và API read representative trước khi promote.
- Notification và Socket.IO là side effect best-effort sau commit; rollback không được phát lại mù quáng vì có thể gửi trùng hoặc phát event trùng.

## Guard mutation Super Admin bị orphan

V2 dùng document mutex không TTL trong collection `counters`:

```text
_id = __ttsmart_v2_superadmin_mutation_guard
```

EventId `1291` nghĩa là guard đang được giữ; `1292` nghĩa là release thất bại và cần kiểm tra thủ công. Không xóa document chỉ vì nó cũ. Trình tự bắt buộc:

1. Dừng hoặc drain mọi instance V2 có thể đang thực hiện mutation Super Admin, hoặc chứng minh bằng deployment ownership/log rằng không còn owner hoạt động.
2. Đọc document theo `_id`, ghi riêng `owner` và `createdAt` trong kênh vận hành được bảo vệ; không đưa giá trị owner vào log công khai.
3. Đối chiếu thời điểm, instance lifecycle và request đang chạy. Nếu chưa chắc owner đã chết, giữ nguyên guard và điều tra tiếp.
4. Chỉ sau khi chắc chắn không có owner hoạt động, database owner mới được xóa thủ công bằng điều kiện cả `_id` và `owner` đã quan sát:

```javascript
db.counters.deleteOne({
  _id: "__ttsmart_v2_superadmin_mutation_guard",
  owner: "<owner-đã-xác-minh-bị-orphan>"
})
```

5. Yêu cầu `deletedCount` bằng 1; nếu bằng 0, không thử xóa rộng hơn vì owner có thể đã thay đổi.
6. Khởi động lại một instance, chạy mutation tổng hợp có kiểm soát và xác nhận guard được tạo rồi giải phóng. Ghi đầy đủ phê duyệt và bằng chứng.

## Xác minh sau rollback

- So sánh contract response, cookie, permission, CSRF-origin và lỗi provider với release trước.
- Kiểm tra order/inventory, upload, ActivityLog, notification và bốn event `order_created`, `order_updated`, `order_cancelled`, `order_deleted` bằng dữ liệu tổng hợp.
- Theo dõi 4xx/5xx, Mongo readiness, EventId provider/notification/Socket.IO và lỗi browser.
- Ghi nguyên nhân gốc, ảnh hưởng dữ liệu, thao tác thủ công và test hồi quy cần bổ sung.

Không dùng lệnh Git hoặc database có tính phá hủy làm rollback ad-hoc. Xem [Runbook sao lưu và khôi phục](BACKUP_RESTORE_RUNBOOK.md), [Khắc phục sự cố](TROUBLESHOOTING.md) và [Danh mục lỗi](ERROR_CATALOG.md).
