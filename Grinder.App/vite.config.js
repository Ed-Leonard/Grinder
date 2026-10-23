import { sveltekit } from "@sveltejs/kit/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [tailwindcss(), sveltekit()],
  ssr: {
    noExternal: ["@thisux/sveltednd"],
  },
  server: {
    host: true,
    port: 5173, // Development server port
    proxy: {
      // Development API proxy (i.e. requests to localhost:5173/api will be proxied to localhost:5000)
      "/api": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false,
      },
      "/uploads": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false,
      },
      "/api/hubs": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false,
        ws: true, // Enable WebSocket proxying for SignalR. This is needed for SignalR to work correctly in development mode (so that Vite knows that /hubs may contain a WebSocket connection, and not just a normal HTTP request).
      },
    },
    allowedHosts: true,
  },
});
