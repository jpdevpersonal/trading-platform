import Link from "next/link";
import { notFound } from "next/navigation";
import { PortfolioPreview, ResearchPreview, ScreenerPreview, WatchlistsPreview } from "../../features/previews/section-previews";
import { sections } from "../../lib/sections";

const previews = {
  screener: ScreenerPreview,
  watchlists: WatchlistsPreview,
  portfolio: PortfolioPreview,
  research: ResearchPreview,
};

export const dynamicParams = false;
export function generateStaticParams() {
  return sections.map(({ slug }) => ({ section: slug }));
}

export default async function Section({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const entry = sections.find(({ slug }) => slug === section);
  if (!entry) notFound();
  const Preview = previews[entry.slug];
  return (
    <>
      <p className="eyebrow">Workspace section</p>
      <h1>{entry.name}</h1>
      <p className="text-slate-600">{entry.description}</p>
      <p className="mt-6 inline-block rounded bg-teal-100 px-2 py-1 text-sm font-semibold text-teal-900">Sample data</p>
      <p className="mt-2 text-slate-600">These examples are fictional and do not represent real market or account data.</p>
      <div className="panel mt-4"><Preview /></div>
      <Link href="/" className="mt-4 inline-block font-semibold text-teal-800 underline">Back to Dashboard</Link>
    </>
  );
}
