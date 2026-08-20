import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget = globalThis.process.env.VITE_DEV_API_TARGET || 'http://localhost:5112';
const apiRoots = [
  'users',
  'products',
  'orders',
  'chips',
  'carts',
  'manages',
  'iporders',
  'eporders',
  'stations',
  'histories',
  'activity-logs',
  'images',
  'documents',
  'section-images',
  'invoice-images',
  'zalo',
  'telegram',
  'voice-vocabs',
  'health',
];
const apiProxy = Object.fromEntries(
  apiRoots.map((root) => [`/${root}`, { target: apiTarget, changeOrigin: true }]),
);
apiProxy['/socket.io'] = { target: apiTarget, changeOrigin: true, ws: true };

// https://vite.dev/config/
export default defineConfig({
  server: {
    port: 5173,
    host: true, // Expose to local network
    proxy: apiProxy,
  },
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.js',
  },
  base: '/admin/', // Đảm bảo tài nguyên tĩnh dùng /admin
});
