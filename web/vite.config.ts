import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://127.0.0.1:5288",
    },
  },
  preview: {
    port: 5173,
    proxy: {
      // Match the dev-server proxy so the production build can be served by
      // `vite preview` and still reach the API on the local port. Production
      // deployments are expected to front the SPA with a reverse proxy that
      // routes `/api` to the API service.
      "/api": "http://127.0.0.1:5288",
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
  },
});
