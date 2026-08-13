# Frontend khách hàng TTSmart

Frontend này chạy trên Vite.

Sử dụng Node.js 22.13 trở lên (Node 22 LTS hoặc Node 24+) để môi trường test Vite và jsdom dùng runtime được hỗ trợ.

## Các lệnh

- `npm run dev` (hoặc `npm start`) khởi động development server tại http://localhost:3000.
- `npm run build` ghi production bundle vào `dist/`.
- `npm run preview` phục vụ production bundle trên máy local.
- `npm test` chạy bộ test hiện có tương thích Jest bằng Vitest.

Sao chép `.env.example` thành `.env` để phát triển local. Development server và preview server proxy `/api` tới `http://localhost:5000`, đồng thời loại bỏ tiền tố `/api` trước khi chuyển tiếp. Sử dụng tiền tố/URL API production do môi trường deployment cung cấp nếu khác cấu hình này.

## Công việc tiếp theo về deployment

Backend phục vụ `fe/dist` và giữ cơ chế SPA fallback về `index.html`.
