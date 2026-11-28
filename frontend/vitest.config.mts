import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.{ts,tsx}"],
    setupFiles: ["./tests/setup.ts"],
    unstubGlobals: true,
    unstubEnvs: true,
  },
});
