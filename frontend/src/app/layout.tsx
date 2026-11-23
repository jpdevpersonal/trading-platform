import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Navigation } from "../components/navigation";
import "./globals.css";

export const metadata: Metadata = {
  title: "Trading Assistant | Local workspace",
  description: "A local workspace for human-led stock research.",
};

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <a href="#main" className="skip-link">Skip to main content</a>
        <header className="border-b border-slate-200 bg-white">
          <div className="mx-auto max-w-5xl px-5">
            <div className="flex flex-wrap items-center justify-between gap-3 py-5">
              <span className="text-xl font-bold tracking-tight">Trading Assistant</span>
              <span className="eyebrow">Local workspace</span>
            </div>
            <Navigation />
          </div>
        </header>
        <main id="main" tabIndex={-1} className="mx-auto max-w-5xl px-5 py-10 sm:py-16">{children}</main>
      </body>
    </html>
  );
}
