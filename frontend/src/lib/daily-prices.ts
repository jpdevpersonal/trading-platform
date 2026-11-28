export type Instrument = { id: string; symbol: string; exchangeMic: string; name: string; currency: string; isSample: boolean };
export type InstrumentPage = { items: Instrument[]; offset: number; limit: number };
export type DateRange = { from: string; to: string };
export type DailyBar = { tradingDate: string; open: string; high: string; low: string; close: string; volume: string };
export type PriceHistory = {
  instrumentId: string; currency: string; canImport: boolean; items: DailyBar[];
  source: string | null; priceBasis: string | null; lastRefreshedAtUtc: string | null;
  lastRefreshFrom: string | null; lastRefreshTo: string | null;
};
export type PriceImport = {
  instrumentId: string; source: string; from: string; to: string;
  inserted: number; updated: number; unchanged: number; lastRefreshedAtUtc: string;
};

export class PriceApiError extends Error {
  constructor(message: string, public readonly status: number, public readonly retryAfter: string | null) { super(message); }
}

const object = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null;
const text = (value: unknown): value is string => typeof value === "string";
const nullableText = (value: unknown): value is string | null => value === null || text(value);
const count = (value: unknown): value is number => typeof value === "number" && Number.isSafeInteger(value) && value >= 0;
const decimal = (value: unknown): value is string => text(value) && /^\d+(\.\d{1,10})?$/.test(value);
const instrument = (value: unknown): value is Instrument => object(value)
  && ["id", "symbol", "exchangeMic", "name", "currency"].every((key) => text(value[key])) && typeof value.isSample === "boolean";
const page = (value: unknown): value is InstrumentPage => object(value) && Array.isArray(value.items)
  && value.items.every(instrument) && count(value.offset) && count(value.limit);
const bar = (value: unknown): value is DailyBar => object(value) && text(value.tradingDate)
  && ["open", "high", "low", "close", "volume"].every((key) => decimal(value[key]));
const history = (value: unknown): value is PriceHistory => object(value) && text(value.instrumentId)
  && text(value.currency) && typeof value.canImport === "boolean" && Array.isArray(value.items) && value.items.every(bar)
  && ["source", "priceBasis", "lastRefreshedAtUtc", "lastRefreshFrom", "lastRefreshTo"].every((key) => nullableText(value[key]));
const summary = (value: unknown): value is PriceImport => object(value)
  && ["instrumentId", "source", "from", "to", "lastRefreshedAtUtc"].every((key) => text(value[key]))
  && ["inserted", "updated", "unchanged"].every((key) => count(value[key]));

async function request<T>(path: string, valid: (value: unknown) => value is T, signal: AbortSignal, body?: DateRange): Promise<T> {
  const base = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) throw new Error("Set NEXT_PUBLIC_API_BASE_URL before starting the web app.");
  const response = await fetch(new URL(path, base), {
    method: body ? "POST" : "GET",
    signal: AbortSignal.any([signal, AbortSignal.timeout(body ? 20_000 : 8_000)]),
    headers: body ? { Accept: "application/json", "Content-Type": "application/json" } : { Accept: "application/json" },
    body: body ? JSON.stringify(body) : undefined,
    cache: "no-store", credentials: "omit",
  });
  const value: unknown = await response.json();
  if (!response.ok) {
    const title = object(value) && text(value.title) ? value.title : `Request returned HTTP ${response.status}.`;
    const detail = object(value) && text(value.detail) ? ` ${value.detail}` : "";
    throw new PriceApiError(title + detail, response.status, response.headers.get("Retry-After"));
  }
  if (!valid(value)) throw new Error("Unexpected daily price API response.");
  return value;
}

export const listInstruments = (offset: number, signal: AbortSignal) =>
  request(`/api/v1/instruments?offset=${offset}&limit=50`, page, signal);
export const findInstrument = (id: string, signal: AbortSignal) =>
  request(`/api/v1/instruments/${encodeURIComponent(id)}`, instrument, signal);
export const readDailyPrices = (id: string, range: DateRange, signal: AbortSignal) =>
  request(`/api/v1/instruments/${encodeURIComponent(id)}/daily-prices?${new URLSearchParams({ from: range.from, to: range.to })}`, history, signal);
export const importDailyPrices = (id: string, range: DateRange, signal: AbortSignal) =>
  request(`/api/v1/instruments/${encodeURIComponent(id)}/daily-prices/import`, summary, signal, { from: range.from, to: range.to });

export function defaultPriceRange(now = new Date()): DateRange {
  const parts = new Intl.DateTimeFormat("en-US", { timeZone: "America/New_York", year: "numeric", month: "2-digit", day: "2-digit" }).formatToParts(now);
  const part = (name: string) => parts.find((item) => item.type === name)!.value;
  const end = new Date(`${part("year")}-${part("month")}-${part("day")}T00:00:00Z`);
  end.setUTCDate(end.getUTCDate() - 1);
  const start = new Date(end);
  start.setUTCDate(start.getUTCDate() - 29);
  return { from: start.toISOString().slice(0, 10), to: end.toISOString().slice(0, 10) };
}
