import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/prices",
  workers: 1,
  use: { baseURL: "http://localhost:3000", browserName: "chromium" },
  webServer: [
    {
      command: "dotnet run --project ../backend/tests/TradingAssistant.IntegrationTests --no-launch-profile -- --browser-host",
      url: "http://localhost:5080/api/v1/ready",
      timeout: 120_000,
      gracefulShutdown: { signal: "SIGTERM", timeout: 10_000 },
    },
    {
      command: "npm run dev -- --port 3000",
      url: "http://localhost:3000",
      env: { NEXT_PUBLIC_API_BASE_URL: "http://localhost:5080" },
      timeout: 120_000,
    },
  ],
});
