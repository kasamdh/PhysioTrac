/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // Matches Cors:AllowedOrigins in src/PhysioTrac.Api/appsettings.json --
    // change both together if you move this off the default Vite port.
    port: 5173,
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    globals: true,
    // jsdom + userEvent tests run several times slower when the whole suite
    // runs in parallel on a busy machine; 5s (the default) caused spurious
    // timeouts on tests that pass in well under a second on their own.
    testTimeout: 30_000,
  },
});
