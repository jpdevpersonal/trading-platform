import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { PriceHistoryView } from "./price-history";
import { findInstrument, importDailyPrices, listInstruments, PriceApiError, readDailyPrices, type PriceHistory, type Instrument } from "../../lib/daily-prices";

vi.mock("../../lib/daily-prices", async (original) => ({
  ...await original<typeof import("../../lib/daily-prices")>(),
  findInstrument: vi.fn(), importDailyPrices: vi.fn(), listInstruments: vi.fn(), readDailyPrices: vi.fn(),
}));

const apple: Instrument = { id: "apple", symbol: "AAPL", exchangeMic: "XNAS", name: "Apple", currency: "USD", isSample: false };
const microsoft: Instrument = { ...apple, id: "microsoft", symbol: "MSFT", name: "Microsoft" };
const initial = { id: "apple", from: "2026-11-23", to: "2026-11-24" };
const saved: PriceHistory = {
  instrumentId: "apple", currency: "USD", canImport: true, source: "Massive", priceBasis: "raw",
  lastRefreshedAtUtc: "2026-11-28T15:00:00Z", lastRefreshFrom: initial.from, lastRefreshTo: initial.to,
  items: [{ tradingDate: "2026-11-24", open: "10.1234567891", high: "12", low: "9", close: "11", volume: "123.0000000001" }],
};

beforeEach(() => {
  vi.resetAllMocks();
  window.history.replaceState(null, "", "/price-history");
  vi.mocked(listInstruments).mockResolvedValue({ items: [apple, microsoft], offset: 0, limit: 50 });
  vi.mocked(findInstrument).mockImplementation(async (id) => id === "apple" ? apple : microsoft);
  vi.mocked(readDailyPrices).mockResolvedValue(saved);
  vi.mocked(importDailyPrices).mockResolvedValue({
    instrumentId: "apple", source: "Massive", ...initial, inserted: 0, updated: 0, unchanged: 1, lastRefreshedAtUtc: saved.lastRefreshedAtUtc!,
  });
});

it("reads saved data with provenance and persists filters without importing", async () => {
  render(<PriceHistoryView initial={initial} />);
  expect(await screen.findByRole("table")).toBeVisible();
  expect(screen.getByText("Massive · Unadjusted daily prices")).toBeVisible();
  expect(screen.getByText(/Last successful refresh for this stock:/)).toHaveTextContent("(UTC), range 2026-11-23 to 2026-11-24");
  expect(screen.getByText("Latest trading date in this view: 2026-11-24")).toBeVisible();
  fireEvent.change(screen.getByLabelText("From (inclusive)"), { target: { value: "2026-11-22" } });
  await waitFor(() => expect(new URLSearchParams(window.location.search).get("from")).toBe("2026-11-22"));
  expect(importDailyPrices).not.toHaveBeenCalled();
});

it.each([503, 429])("preserves saved rows on HTTP %s and offers manual retry", async (status) => {
  vi.mocked(importDailyPrices).mockRejectedValueOnce(new PriceApiError(status === 503 ? "Configure the backend key." : "Quota exhausted.", status, status === 429 ? "60" : null));
  render(<PriceHistoryView initial={initial} />);
  await screen.findByRole("table");
  const button = screen.getByRole("button", { name: "Import daily prices" });
  fireEvent.click(button);
  expect(await screen.findByRole("alert")).toHaveTextContent(status === 503 ? "Configure the backend key." : "Retry after 60 seconds.");
  expect(screen.getByRole("table")).toBeVisible();
  fireEvent.click(button);
  await waitFor(() => expect(screen.getByRole("status")).toHaveTextContent("0 inserted, 0 updated, 1 unchanged"));
  expect(readDailyPrices).toHaveBeenCalledTimes(2);
});

it("prevents overlapping submissions and cancels an old import when switching stocks", async () => {
  const pending = Promise.withResolvers<Awaited<ReturnType<typeof importDailyPrices>>>();
  vi.mocked(importDailyPrices).mockReturnValue(pending.promise);
  render(<PriceHistoryView initial={initial} />);
  await screen.findByRole("table");
  const button = screen.getByRole("button", { name: "Import daily prices" });
  fireEvent.click(button);
  expect(button).toBeDisabled();
  expect(screen.getByRole("status")).toHaveTextContent("Importing daily prices");
  fireEvent.click(button);
  expect(importDailyPrices).toHaveBeenCalledTimes(1);
  vi.mocked(readDailyPrices).mockResolvedValue({ ...saved, instrumentId: "microsoft", items: [], lastRefreshedAtUtc: null });
  fireEvent.change(screen.getByLabelText("Listing"), { target: { value: "microsoft" } });
  expect(vi.mocked(importDailyPrices).mock.calls[0][2].aborted).toBe(true);
  expect(screen.queryByRole("table")).not.toBeInTheDocument();
  await screen.findByText(/No imports yet/);
  await act(async () => pending.resolve({ instrumentId: "apple", source: "Massive", ...initial, inserted: 9, updated: 0, unchanged: 0, lastRefreshedAtUtc: saved.lastRefreshedAtUtc! }));
  expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Microsoft");
  expect(screen.getByRole("status")).not.toHaveTextContent("9 inserted");
  expect(screen.queryByRole("table")).not.toBeInTheDocument();
});

it("ignores late reads for another listing", async () => {
  const pending = Promise.withResolvers<PriceHistory>();
  vi.mocked(readDailyPrices).mockReturnValueOnce(pending.promise).mockResolvedValue({ ...saved, instrumentId: "microsoft", items: [], lastRefreshedAtUtc: null });
  render(<PriceHistoryView initial={initial} />);
  await waitFor(() => expect(screen.getByLabelText("Listing")).toHaveTextContent("MSFT"));
  fireEvent.change(screen.getByLabelText("Listing"), { target: { value: "microsoft" } });
  await screen.findByText(/No imports yet/);
  await act(async () => pending.resolve(saved));
  expect(screen.getByRole("heading", { level: 2 })).toHaveTextContent("Microsoft");
  expect(screen.queryByRole("table")).not.toBeInTheDocument();
});

it("guides an empty catalogue and recovers from a read error", async () => {
  vi.mocked(listInstruments).mockResolvedValueOnce({ items: [], offset: 0, limit: 50 });
  const { unmount } = render(<PriceHistoryView initial={{ ...initial, id: "" }} />);
  expect(await screen.findByText(/catalogue is empty/)).toHaveTextContent("real-instruments.sql");
  expect(importDailyPrices).not.toHaveBeenCalled();
  unmount();
  vi.mocked(readDailyPrices).mockRejectedValueOnce(new Error("offline"));
  render(<PriceHistoryView initial={initial} />);
  await screen.findByRole("alert");
  fireEvent.click(screen.getByRole("button", { name: "Retry saved prices" }));
  expect(await screen.findByRole("table")).toBeVisible();
});

it("shows an empty view separately from an earlier successful stock refresh", async () => {
  vi.mocked(readDailyPrices).mockResolvedValue({ ...saved, items: [] });
  render(<PriceHistoryView initial={initial} />);
  expect(await screen.findByText(/No saved rows in this range/)).toBeVisible();
  expect(screen.getByText(/Last successful refresh for this stock:/)).toHaveTextContent("2026-11-28T15:00:00Z");
  expect(screen.getByText("Latest trading date in this view: None")).toBeVisible();
});

it("pages catalogue listings and labels unsupported samples without enabling import", async () => {
  vi.mocked(listInstruments).mockResolvedValueOnce({
    items: Array.from({ length: 50 }, (_, index) => ({ ...apple, id: `sample-${index}`, isSample: true })),
    offset: 0, limit: 50,
  }).mockResolvedValueOnce({ items: [microsoft], offset: 50, limit: 50 });
  vi.mocked(readDailyPrices).mockResolvedValue({ ...saved, canImport: false, items: [] });
  render(<PriceHistoryView initial={initial} />);
  await screen.findByText(/This listing is sample or unsupported/);
  expect(screen.getByLabelText("Listing")).toHaveTextContent("Sample");
  expect(screen.getByRole("button", { name: "Import daily prices" })).toBeDisabled();
  fireEvent.click(screen.getByRole("button", { name: "Next listings" }));
  await waitFor(() => expect(listInstruments).toHaveBeenLastCalledWith(50, expect.any(AbortSignal)));
  await waitFor(() => expect(screen.getByLabelText("Listing")).toHaveTextContent("MSFT"));
});
