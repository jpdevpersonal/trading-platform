type Instrument = Readonly<{ name: string; symbol: string; sector: string }>;

const alder = { name: "Alder Orbit", symbol: "S-AO", sector: "Technology" } satisfies Instrument;
const bracken = { name: "Bracken Grid", symbol: "S-BG", sector: "Utilities" } satisfies Instrument;
const copper = { name: "Copper Vale", symbol: "S-CV", sector: "Industrials" } satisfies Instrument;

export const sampleUniverse: readonly Instrument[] = [alder, bracken, copper];

export const sampleWatchlist = { name: "Example watchlist: Companies to explore", members: sampleUniverse };

export const samplePositions: readonly { instrument: Instrument; quantity: number }[] = [
  { instrument: alder, quantity: 12 },
  { instrument: bracken, quantity: 8 },
  { instrument: copper, quantity: 20 },
];

export const sampleNotes: readonly { instrument: Instrument; title: string; excerpt: string }[] = [
  { instrument: alder, title: "Customer concentration", excerpt: "What evidence would show whether revenue depends on a few customers?" },
  { instrument: copper, title: "Cost exposure", excerpt: "Which operating costs would be most sensitive to a change in input prices?" },
];
