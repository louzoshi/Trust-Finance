import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// Dev server proxies /api to the ASP.NET Core API so no CORS setup is
// needed locally. In production, set VITE_API_URL to the deployed API.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:5151",
        changeOrigin: true,
      },
    },
  },
});
