import { PriceHistoryView } from "../../features/prices/price-history";
import { defaultPriceRange } from "../../lib/daily-prices";

export default async function PriceHistoryPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const params = await searchParams;
  const defaults = defaultPriceRange();
  return <PriceHistoryView initial={{
    id: typeof params.instrument === "string" ? params.instrument : "",
    from: typeof params.from === "string" ? params.from : defaults.from,
    to: typeof params.to === "string" ? params.to : defaults.to,
  }} />;
}
