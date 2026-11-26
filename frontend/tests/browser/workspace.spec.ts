import { expect, test, type Page } from "@playwright/test";

const destinations = [
  { path: "/", name: "Dashboard" },
  { path: "/screener", name: "Screener", purpose: "Explore how a stock universe is organized." },
  { path: "/watchlists", name: "Watchlists", purpose: "See how instruments can be grouped into a watchlist." },
  { path: "/portfolio", name: "Portfolio", purpose: "Preview a list of positions and their quantities." },
  { path: "/research", name: "Research", purpose: "See how research notes can be linked to instruments." },
] as const;

async function expectDestination(page: Page, name: string) {
  const nav = page.getByRole("navigation", { name: "Workspace" });
  const main = page.getByRole("main");
  await expect(page.getByRole("banner")).toHaveCount(1);
  await expect(nav).toHaveCount(1);
  await expect(main).toHaveCount(1);
  await expect(page.getByRole("heading", { level: 1 })).toHaveCount(1);
  await expect(main.getByRole("heading", { level: 1, name, exact: true })).toBeVisible();
  await expect(nav.locator('a[aria-current="page"]')).toHaveCount(1);
  await expect(nav.getByRole("link", { name, exact: true })).toHaveAttribute("aria-current", "page");
}

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
  const skip = page.getByRole("link", { name: "Skip to main content" });
  await expect(skip).toBeFocused();
  await expect(skip).toBeInViewport();
  await expect(skip).toHaveCSS("outline-style", "solid");
  const nav = page.getByRole("navigation", { name: "Workspace" });
  await page.keyboard.press("Tab");
  await expect(nav.getByRole("link", { name: "Dashboard" })).toBeFocused();
  await page.keyboard.press("Shift+Tab");
  await page.keyboard.press("Enter");
  const main = page.getByRole("main");
  await expect(main).toBeFocused();
  const retry = main.getByRole("button", { name: "Check again" });
  await expect(retry).toBeEnabled();
  for (const { name } of destinations.slice(1)) {
    await page.keyboard.press("Tab");
    const link = main.getByRole("link", { name, exact: true });
    await expect(link).toBeFocused();
    await expect(link).toHaveCSS("outline-style", "solid");
  }
  await page.keyboard.press("Tab");
  await expect(retry).toBeFocused();
  await expect(retry).toHaveCSS("outline-style", "solid");
  for (const name of ["Screener", "Watchlists", "Portfolio", "Research", "Dashboard"]) {
    const link = nav.getByRole("link", { name, exact: true });
    await link.focus();
    await page.keyboard.press("Enter");
    await expectDestination(page, name);
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
});

for (const width of [360, 1280]) {
  test(`direct workspace routes fit at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 });
    for (const { path, name } of destinations) {
      expect((await page.goto(path))?.status()).toBe(200);
      await expectDestination(page, name);
      const nav = page.getByRole("navigation", { name: "Workspace" });
      const main = page.getByRole("main");
      const navBox = await nav.boundingBox();
      const mainBox = await main.boundingBox();
      if (!navBox || !mainBox) throw new Error("Workspace navigation and main must be visible");
      if (width === 360) {
        expect(navBox.y + navBox.height).toBeLessThanOrEqual(mainBox.y);
        const firstLink = await nav.getByRole("link", { name: "Dashboard" }).boundingBox();
        const lastLink = await nav.getByRole("link", { name: "Research" }).boundingBox();
        if (!firstLink || !lastLink) throw new Error("Workspace links must be visible");
        expect(lastLink.y).toBeGreaterThan(firstLink.y);
      } else {
        expect(navBox.x + navBox.width).toBeLessThanOrEqual(mainBox.x);
        expect(navBox.y).toBe(mainBox.y);
      }
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
      if (path !== "/") {
        await expect(main.getByText("Sample data", { exact: true })).toBeVisible();
        await expect(main.getByText("These examples are fictional", { exact: false })).toBeVisible();
        await expect(main.getByRole("button")).toHaveCount(0);
        await expect(main.getByRole("link", { name: "Back to Dashboard" })).toBeVisible();
      }
    }
  });
}

test("unknown workspace path returns 404", async ({ page }) => {
  expect((await page.goto("/unknown-workspace"))?.status()).toBe(404);
  await expect(page.getByText("This page could not be found.")).toBeVisible();
});

test("Dashboard previews lead to each destination and retain browser history", async ({ page }) => {
  await page.goto("/");
  const previews = page.getByRole("main").getByRole("region", { name: "Workspace previews" });
  for (const destination of destinations) {
    if (destination.path === "/") continue;
    await expect(previews.getByText(destination.purpose)).toBeVisible();
    await previews.getByRole("link", { name: destination.name, exact: true }).click();
    await expect(page).toHaveURL(destination.path);
    await expectDestination(page, destination.name);
    await page.goBack();
    await expectDestination(page, "Dashboard");
    await page.goForward();
    await expectDestination(page, destination.name);
    await page.goBack();
    await expectDestination(page, "Dashboard");
  }
});

test("each preview renders the expected read-only sample content", async ({ page }) => {
  const main = page.getByRole("main");
  await page.goto("/screener");
  const screener = main.getByRole("table", { name: "Sample stock universe" });
  await expect(screener.getByRole("columnheader")).toHaveText(["Instrument", "Symbol", "Sector"]);
  await expect(screener.getByRole("cell")).toHaveText([
    "Alder Orbit", "S-AO", "Technology", "Bracken Grid", "S-BG", "Utilities", "Copper Vale", "S-CV", "Industrials",
  ]);

  await page.goto("/watchlists");
  await expect(main.getByRole("heading", { name: "Example watchlist: Companies to explore" })).toBeVisible();
  await expect(main.getByRole("listitem")).toHaveText([
    "Alder Orbit (S-AO)", "Bracken Grid (S-BG)", "Copper Vale (S-CV)",
  ]);

  await page.goto("/portfolio");
  const portfolio = main.getByRole("table", { name: "Illustrative positions" });
  await expect(portfolio.getByRole("columnheader")).toHaveText(["Instrument", "Sample quantity"]);
  await expect(portfolio.getByRole("cell")).toHaveText(["Alder Orbit", "12", "Bracken Grid", "8", "Copper Vale", "20"]);

  await page.goto("/research");
  await expect(main.getByRole("listitem")).toHaveCount(2);
  for (const [title, instrument, excerpt] of [
    ["Customer concentration", "Alder Orbit (S-AO)", "What evidence would show whether revenue depends on a few customers?"],
    ["Cost exposure", "Copper Vale (S-CV)", "Which operating costs would be most sensitive to a change in input prices?"],
  ]) {
    const note = main.getByRole("listitem").filter({ has: page.getByRole("heading", { name: title }) });
    await expect(note.getByText(instrument, { exact: true })).toBeVisible();
    await expect(note.getByText(excerpt, { exact: true })).toBeVisible();
  }
});
