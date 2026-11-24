import type { ReactNode } from "react";
import { sampleNotes, samplePositions, sampleUniverse, sampleWatchlist } from "./fixtures";

function PreviewTable({ title, headings, children }: { title: string; headings: string[]; children: ReactNode }) {
  return (
    <table className="w-full text-left text-sm [&_td]:px-1 [&_td]:py-2 [&_th]:px-1 [&_th]:py-2 [&_tr]:border-b [&_tr]:border-slate-200">
      <caption className="mb-2 text-left text-xl font-semibold">{title}</caption>
      <thead>
        <tr>{headings.map((heading) => <th key={heading} scope="col">{heading}</th>)}</tr>
      </thead>
      <tbody>{children}</tbody>
    </table>
  );
}

export function ScreenerPreview() {
  return (
    <PreviewTable title="Sample stock universe" headings={["Instrument", "Symbol", "Sector"]}>
      {sampleUniverse.map(({ name, symbol, sector }) => (
        <tr key={symbol}><td>{name}</td><td>{symbol}</td><td>{sector}</td></tr>
      ))}
    </PreviewTable>
  );
}

export function WatchlistsPreview() {
  return (
    <>
      <h2 className="text-xl font-semibold">{sampleWatchlist.name}</h2>
      <ul className="mt-3 list-disc space-y-2 pl-5">
        {sampleWatchlist.members.map(({ name, symbol }) => <li key={symbol}>{name} ({symbol})</li>)}
      </ul>
    </>
  );
}

export function PortfolioPreview() {
  return (
    <PreviewTable title="Illustrative positions" headings={["Instrument", "Sample quantity"]}>
      {samplePositions.map(({ instrument, quantity }) => (
        <tr key={instrument.symbol}><td>{instrument.name}</td><td>{quantity}</td></tr>
      ))}
    </PreviewTable>
  );
}

export function ResearchPreview() {
  return (
    <>
      <h2 className="text-xl font-semibold">Example research notes</h2>
      <ul className="mt-3 list-disc space-y-4 pl-5">
        {sampleNotes.map(({ instrument, title, excerpt }) => (
          <li key={title}>
            <h3 className="font-semibold">{title}</h3>
            <p className="text-sm text-slate-600">{instrument.name} ({instrument.symbol})</p>
            <p className="mt-1">{excerpt}</p>
          </li>
        ))}
      </ul>
    </>
  );
}
