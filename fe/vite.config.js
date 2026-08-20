import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const apiTarget = globalThis.process.env.VITE_DEV_API_TARGET || "http://localhost:5112";

const createApiProxy = () => ({
  "^/api(?:/|$)": {
    target: apiTarget,
    changeOrigin: true,
    rewrite: (path) => path.replace(/^\/api(?=\/|$)/, ""),
  },
});

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: createApiProxy(),
  },
  preview: {
    proxy: createApiProxy(),
  },
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes("node_modules")) return undefined;
          const normalizedId = id.replaceAll("\\", "/");
          if (/node_modules\/(?:react|react-dom|react-router|react-router-dom|scheduler)\//.test(normalizedId)) {
            return "react-vendor";
          }
          if (normalizedId.includes("node_modules/@mui/icons-material/")) {
            return "mui-icons";
          }
          if (/node_modules\/(?:@mui|@emotion)\//.test(normalizedId)) {
            return "mui-vendor";
          }
          if (normalizedId.includes("node_modules/swiper/")) return "swiper-vendor";
          if (normalizedId.includes("node_modules/react-icons/")) return "icons-vendor";
          return "vendor";
        },
      },
    },
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/setupTests.js",
  },
});
