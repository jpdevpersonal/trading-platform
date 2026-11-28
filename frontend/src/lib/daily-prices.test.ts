import { beforeEach, expect, it, vi } from "vitest";
import { defaultPriceRange, importDailyPrices, readDailyPrices } from "./daily-prices";

beforeEach(() => vi.stubEnv("NEXT_PUBLIC_API_BASE_URL", "http://localhost:5080"));

it("defaults to exactly 30 completed New York dates across DST", () => {
  expect(defaultPriceRange(new Date("2026-03-09T02:00:00Z"))).toEqual({ from: "2026-02-06", to: "2026-03-07" });
  expect(defaultPriceRange(new Date("2026-11-02T04:00:00Z"))).toEqual({ from: "2026-10-02", to: "2026-10-31" });
});

it("sends only explicit JSON dates and never credentials, and disables caching", async () => {
  const range = { from: "2026-11-23", to: "2026-11-24" };
  const fetch = vi.fn().mockResolvedValue(Response.json({
    instrumentId: "apple", source: "Massive", ...range, inserted: 0, updated: 0, unchanged: 0, lastRefreshedAtUtc: "2026-11-28T00:00:00Z",
  }));
  vi.stubGlobal("fetch", fetch);
  await importDailyPrices("apple", range, new AbortController().signal);
  expect(fetch).toHaveBeenCalledWith(new URL("http://localhost:5080/api/v1/instruments/apple/daily-prices/import"), expect.objectContaining({
    method: "POST", credentials: "omit", cache: "no-store", body: JSON.stringify(range),
    headers: { Accept: "application/json", "Content-Type": "application/json" },
  }));
});

it("rejects numeric rather than string-valued price contracts", async () => {
  vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({
    instrumentId: "apple", currency: "USD", canImport: true, source: null, priceBasis: null, lastRefreshedAtUtc: null,
    lastRefreshFrom: null, lastRefreshTo: null, items: [{ tradingDate: "2026-11-23", open: 10, high: "11", low: "9", close: "10", volume: "1" }],
  })));
  await expect(readDailyPrices("apple", { from: "2026-11-23", to: "2026-11-24" }, new AbortController().signal)).rejects.toThrow("Unexpected daily price API response");
});
