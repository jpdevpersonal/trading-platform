import { render, screen, within } from "@testing-library/react";
import { expect, it } from "vitest";
import { PortfolioPreview, ScreenerPreview } from "./section-previews";

it.each([
  [ScreenerPreview, "Sample stock universe", ["Instrument", "Symbol", "Sector"], ["Alder Orbit", "S-AO", "Technology"]],
  [PortfolioPreview, "Illustrative positions", ["Instrument", "Sample quantity"], ["Alder Orbit", "12"]],
] as const)("presents a named table with column headers and sample cells (%s)", (Preview, name, headers, cells) => {
  render(<Preview />);
  const table = within(screen.getByRole("table", { name }));
  expect(table.getAllByRole("columnheader").map((header) => header.textContent)).toEqual(headers);
  expect(table.getAllByRole("columnheader").every((header) => header.getAttribute("scope") === "col")).toBe(true);
  expect(within(table.getAllByRole("row")[1]).getAllByRole("cell").map((cell) => cell.textContent)).toEqual(cells);
});
