"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  findInstrument, importDailyPrices, listInstruments, PriceApiError, readDailyPrices,
  type DateRange, type Instrument, type InstrumentPage, type PriceHistory,
} from "../../lib/daily-prices";

type Selection = DateRange & { id: string };
type Snapshot = { key: string; instrument: Instrument; history: PriceHistory };
const label = (stock: Instrument) => `${stock.symbol} · ${stock.exchangeMic} · ${stock.currency}${stock.isSample ? " · Sample" : ""}`;
const message = (error: unknown) => error instanceof PriceApiError
  ? `${error.message}${error.retryAfter ? ` Retry after ${error.retryAfter} seconds.` : ""}`
  : error instanceof Error && error.message.startsWith("Set NEXT_PUBLIC") ? error.message
    : "The local API could not complete this request. Check the connection and retry.";

export function PriceHistoryView({ initial }: { initial: Selection }) {
  const [selection, setSelection] = useState(initial);
  const [offset, setOffset] = useState(0);
  const [catalogue, setCatalogue] = useState<InstrumentPage>();
  const [catalogueError, setCatalogueError] = useState("");
  const [catalogueRetry, setCatalogueRetry] = useState(0);
  const [snapshot, setSnapshot] = useState<Snapshot>();
  const [readError, setReadError] = useState<{ key: string; message: string }>();
  const [readRetry, setReadRetry] = useState(0);
  const [importStatus, setImportStatus] = useState("");
  const [importError, setImportError] = useState("");
  const [importing, setImporting] = useState(false);
  const importController = useRef<AbortController | null>(null);
  const readController = useRef<AbortController | null>(null);
  const key = JSON.stringify(selection);
  const current = snapshot?.key === key ? snapshot : undefined;
  const error = readError?.key === key ? readError.message : "";
  const stocks = catalogue?.offset === offset ? catalogue.items : [];

  useEffect(() => {
    const controller = new AbortController();
    listInstruments(offset, controller.signal).then((result) => {
      if (controller.signal.aborted) return;
      setCatalogue(result);
      setCatalogueError("");
      setSelection((selected) => selected.id || !result.items.length ? selected : { ...selected, id: result.items[0].id });
    }).catch((failure: unknown) => {
      if (!controller.signal.aborted) setCatalogueError(message(failure));
    });
    return () => controller.abort();
  }, [offset, catalogueRetry]);

  useEffect(() => {
    const url = new URL(window.location.href);
    for (const [name, value] of Object.entries({ instrument: selection.id, from: selection.from, to: selection.to })) {
      if (value) url.searchParams.set(name, value);
      else url.searchParams.delete(name);
    }
    window.history.replaceState(null, "", url);
  }, [selection]);

  useEffect(() => {
    const onPop = () => {
      importController.current?.abort();
      setImporting(false);
      setImportError("");
      setImportStatus("");
      const params = new URLSearchParams(window.location.search);
      setSelection({ id: params.get("instrument") ?? initial.id, from: params.get("from") ?? initial.from, to: params.get("to") ?? initial.to });
    };
    window.addEventListener("popstate", onPop);
    return () => { window.removeEventListener("popstate", onPop); importController.current?.abort(); };
  }, [initial]);

  useEffect(() => {
    if (!selection.id) return;
    const controller = new AbortController();
    readController.current = controller;
    Promise.all([findInstrument(selection.id, controller.signal), readDailyPrices(selection.id, selection, controller.signal)])
      .then(([instrument, history]) => {
        if (controller.signal.aborted) return;
        setSnapshot({ key, instrument, history });
        setReadError(undefined);
      }).catch((failure: unknown) => {
        if (!controller.signal.aborted) setReadError({ key, message: message(failure) });
      });
    return () => controller.abort();
  }, [selection, key, readRetry]);

  function change(next: Selection) {
    importController.current?.abort();
    setImporting(false);
    setImportStatus("");
    setImportError("");
    setSelection(next);
  }

  async function importPrices(event: FormEvent) {
    event.preventDefault();
    if (importing || !current?.history.canImport) return;
    const controller = new AbortController();
    readController.current?.abort();
    importController.current = controller;
    setImporting(true);
    setImportError("");
    setImportStatus("Importing daily prices…");
    try {
      const result = await importDailyPrices(selection.id, selection, controller.signal);
      if (controller.signal.aborted) return;
      setImportStatus(`Import complete: ${result.inserted} inserted, ${result.updated} updated, ${result.unchanged} unchanged.`);
      const history = await readDailyPrices(selection.id, selection, controller.signal);
      if (!controller.signal.aborted) {
        setSnapshot({ key, instrument: current.instrument, history });
        setReadError(undefined);
      }
    } catch (failure) {
      if (!controller.signal.aborted) {
        setImportStatus("");
        setImportError(message(failure));
      }
    } finally {
      if (!controller.signal.aborted) setImporting(false);
    }
  }

  const history = current?.history;
  const options = current && !stocks.some((stock) => stock.id === current.instrument.id)
    ? [current.instrument, ...stocks] : stocks;
  return (
    <>
      <p className="eyebrow">Stored market data</p>
      <h1>Price history</h1>
      <p>Manual daily imports for stock research. No orders or automatic refresh.</p>
      <p className="mt-2"><Link href="/" className="text-teal-800 underline">Back to Dashboard</Link></p>
      <form className="panel mt-6 space-y-4" onSubmit={importPrices} aria-label="Daily price import">
        <label className="block">Listing
          <select className="mt-1 block w-full rounded border p-2" value={selection.id} onChange={(event) => change({ ...selection, id: event.target.value })}>
            <option value="">Choose a listing</option>
            {selection.id && !options.some((stock) => stock.id === selection.id) && <option value={selection.id}>Selected listing (loading)</option>}
            {options.map((stock) => <option key={stock.id} value={stock.id}>{label(stock)}</option>)}
          </select>
        </label>
        <div className="flex flex-wrap gap-2">
          <button type="button" disabled={offset === 0} onClick={() => setOffset(Math.max(0, offset - 50))}>Previous listings</button>
          <button type="button" disabled={stocks.length < 50} onClick={() => setOffset(offset + 50)}>Next listings</button>
        </div>
        <div className="flex flex-wrap gap-4">
          <label>From (inclusive)<input required type="date" className="mt-1 block rounded border p-2" value={selection.from} onChange={(event) => change({ ...selection, from: event.target.value })} /></label>
          <label>To (inclusive)<input required type="date" className="mt-1 block rounded border p-2" value={selection.to} onChange={(event) => change({ ...selection, to: event.target.value })} /></label>
        </div>
        <p className="text-sm">At most 366 calendar days. Imports start within two years and end yesterday or earlier in America/New_York. Older saved history can be read.</p>
        <p>Configured import provider: Massive Stocks Basic (US stocks).</p>
        <button type="submit" disabled={importing || !history?.canImport || !selection.from || !selection.to}>Import daily prices</button>
      </form>
      {catalogueError && <div className="mt-4"><p role="alert">{catalogueError}</p><button onClick={() => setCatalogueRetry((value) => value + 1)}>Retry catalogue</button></div>}
      {catalogue?.offset === 0 && !catalogue.items.length && <p className="mt-4">The catalogue is empty. Apply migrations and the opt-in real-instruments.sql setup from the README, then retry the catalogue.</p>}
      {catalogue?.offset === 0 && !catalogue.items.length && <button onClick={() => setCatalogueRetry((value) => value + 1)}>Reload catalogue</button>}
      <p role="status" aria-live="polite" className="mt-4">
        {importStatus || (!catalogue && !catalogueError ? "Loading catalogue…" : selection.id && !current && !error ? "Loading saved daily prices…" : current ? "Saved daily prices loaded." : "Choose a listing to read saved prices.")}
      </p>
      {importError && <p role="alert" className="mt-3">{importError} Previously saved results remain below. Retry with Import daily prices.</p>}
      {error && <div className="mt-3"><p role="alert">{error}</p><button disabled={importing} onClick={() => setReadRetry((value) => value + 1)}>Retry saved prices</button></div>}
      {current && history && <section className="mt-6" aria-labelledby="saved-title">
        <h2 id="saved-title" className="text-xl font-semibold">{current.instrument.name} ({label(current.instrument)})</h2>
        {!history.canImport && <p>This listing is sample or unsupported. Use the opt-in real-instruments.sql setup for non-sample XNAS AAPL/MSFT USD listings.</p>}
        <p>{history.source ? `${history.source} · ${history.priceBasis === "raw" ? "Unadjusted daily prices" : history.priceBasis}` : "No saved source yet."}</p>
        <p>Quote currency: {history.currency}</p>
        <p>Last successful refresh for this stock: {history.lastRefreshedAtUtc
          ? <><time dateTime={history.lastRefreshedAtUtc}>{history.lastRefreshedAtUtc}</time> (UTC), range {history.lastRefreshFrom} to {history.lastRefreshTo}</>
          : "Not imported yet."}</p>
        <p className="text-sm">A refresh describes the last checked range, not the freshness of every saved date.</p>
        <p>Latest trading date in this view: {history.items[0]?.tradingDate ?? "None"}</p>
        {!history.items.length ? <p className="mt-4">{history.lastRefreshedAtUtc
          ? "No saved rows in this range. A previous refresh may cover a different range."
          : "No imports yet. Select a completed range and import daily prices."}</p>
          : <div className="mt-4 max-w-full overflow-x-auto" role="region" aria-label="Scrollable daily prices" tabIndex={0}>
            <table className="w-full border-collapse text-left text-sm">
              <caption className="mb-2 text-left">Saved daily OHLCV, newest first. Provider daily aggregates, not official closing auction prices.</caption>
              <thead><tr>{["Trading date", "Open", "High", "Low", "Close", "Volume"].map((name) => <th scope="col" className="border-b p-2" key={name}>{name}</th>)}</tr></thead>
              <tbody>{history.items.map((bar) => <tr key={bar.tradingDate}>{[bar.tradingDate, bar.open, bar.high, bar.low, bar.close, bar.volume].map((value, index) =>
                index === 0 ? <th scope="row" className="whitespace-nowrap border-b p-2" key={index}>{value}</th> : <td className="whitespace-nowrap border-b p-2" key={index}>{value}</td>)}</tr>)}</tbody>
            </table>
          </div>}
      </section>}
    </>
  );
}
