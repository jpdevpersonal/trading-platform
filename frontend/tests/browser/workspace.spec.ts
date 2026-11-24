import { expect, test } from "@playwright/test";

for (const origin of ["http://localhost:3000", "http://127.0.0.1:3000"]) {
  test(`dashboard connects to the API and dev WebSocket from ${origin}`, async ({ page }) => {
    const health = page.waitForResponse(async (response) =>
      response.url() === "http://localhost:5080/api/v1/health" && await response.finished() === null);
    const hmr = page.waitForEvent("websocket", (socket) => socket.url().includes("/_next/hmr"))
      .then((socket) => socket.waitForEvent("framereceived"));
    await page.goto(origin);
    await hmr;
    await expect(page.getByRole("banner")).toBeVisible();
    await expect(page.getByRole("main").getByRole("heading", { name: "Dashboard" })).toBeVisible();
    expect(await (await health).json()).toEqual({ status: "healthy" });
    await expect(page.getByRole("status").filter({ hasText: "Connected to API" })).toBeVisible();
  });
}

test("narrow shell supports keyboard entry and each destination", async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 740 });
  await page.goto("/");
  await page.keyboard.press("Tab");
  await expect(page.getByRole("link", { name: "Skip to main content" })).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.getByRole("main")).toBeFocused();
  const nav = page.getByRole("navigation", { name: "Workspace" });
  for (const name of ["Screener", "Watchlists", "Portfolio", "Research", "Dashboard"]) {
    const link = nav.getByRole("link", { name, exact: true });
    await link.focus();
    await page.keyboard.press("Enter");
    await expect(page.getByRole("heading", { level: 1, name })).toBeVisible();
    await expect(link).toHaveAttribute("aria-current", "page");
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
});
