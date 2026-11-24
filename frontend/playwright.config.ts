import { defineConfig } from "@playwright/test";

const web = "http://localhost:3000";
const api = "http://localhost:5080";

export default defineConfig({
  testDir: "./tests/browser",
  use: { baseURL: web, browserName: "chromium" },
  webServer: [
    {
      command: "dotnet run --project ../backend/src/TradingAssistant.Api --no-launch-profile",
      url: `${api}/api/v1/health`,
      env: { ASPNETCORE_ENVIRONMENT: "Development", ASPNETCORE_URLS: api, FrontendOrigin: web },
      timeout: 120_000,
    },
    {
      command: "npm run dev -- --port 3000",
      url: web,
      env: { NEXT_PUBLIC_API_BASE_URL: api },
      timeout: 120_000,
    },
  ],
});
