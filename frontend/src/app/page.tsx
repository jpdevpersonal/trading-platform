import Link from "next/link";
import { ConnectionStatus } from "../features/health/connection-status";
import { sections } from "../lib/sections";

export default function Dashboard() {
  return (
    <>
      <p className="eyebrow">Workspace overview</p>
      <h1>Dashboard</h1>
      <p className="text-slate-600">Your starting point for stock research and portfolio review.</p>
      <section aria-labelledby="previews-title" className="mt-8 max-w-2xl">
        <h2 id="previews-title" className="text-xl font-semibold">Workspace previews</h2>
        <ul className="mt-4 grid gap-3 sm:grid-cols-2">
          {sections.map(({ slug, name, description }) => (
            <li key={slug} className="panel">
              <Link href={`/${slug}`} className="font-semibold text-teal-800 underline">{name}</Link>
              <p className="mt-1 text-slate-600">{description}</p>
            </li>
          ))}
        </ul>
      </section>
      <ConnectionStatus />
    </>
  );
}
