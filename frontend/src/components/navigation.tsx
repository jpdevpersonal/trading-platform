"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { sections } from "../lib/sections";

export function Navigation() {
  const path = usePathname();
  return (
    <nav aria-label="Workspace" className="flex flex-wrap gap-1 pb-4">
      {[{ slug: "", name: "Dashboard" }, ...sections].map(({ slug, name }) => (
        <Link key={name} href={`/${slug}`} aria-current={path === `/${slug}` ? "page" : undefined}
          className="rounded px-3 py-2 text-sm font-semibold hover:bg-slate-100 aria-[current=page]:bg-teal-900 aria-[current=page]:text-white">
          {name}
        </Link>
      ))}
    </nav>
  );
}
