import Link from "next/link";
import { notFound } from "next/navigation";
import { sections } from "../../lib/sections";

export const dynamicParams = false;
export function generateStaticParams() {
  return sections.map(({ slug }) => ({ section: slug }));
}

export default async function Section({ params }: { params: Promise<{ section: string }> }) {
  const { section } = await params;
  const entry = sections.find(({ slug }) => slug === section);
  if (!entry) notFound();
  return (
    <>
      <p className="eyebrow">Workspace section</p>
      <h1>{entry.name}</h1>
      <div className="panel mt-6">
        <p>{entry.description}</p>
        <Link href="/" className="mt-4 inline-block font-semibold text-teal-800 underline">Back to Dashboard</Link>
      </div>
    </>
  );
}
