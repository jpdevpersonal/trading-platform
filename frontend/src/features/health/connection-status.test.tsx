import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, expect, it, vi } from "vitest";
import { ConnectionStatus } from "./connection-status";

beforeEach(() => vi.stubEnv("NEXT_PUBLIC_API_BASE_URL", "http://localhost:5099"));

it("shows loading until the configured health endpoint returns a healthy response", async () => {
  const request = Promise.withResolvers<Response>();
  const fetch = vi.fn().mockReturnValue(request.promise);
  vi.stubGlobal("fetch", fetch);
  render(<ConnectionStatus />);
  expect(screen.getByRole("status")).toHaveTextContent("Checking API connection");
  expect(fetch.mock.calls[0][0].href).toBe("http://localhost:5099/api/v1/health");
  await act(async () => request.resolve(Response.json({ status: "healthy" })));
  expect(screen.getByRole("status")).toHaveTextContent("Connected to API");
});

it.each([
  ["incorrect contract", () => Response.json({ status: "ready" })],
  ["HTTP failure", () => Response.json({ status: "healthy" }, { status: 503 })],
  ["network failure", () => Promise.reject(new TypeError("offline"))],
] as const)("shows unavailable after %s and recovers on retry", async (_, firstResponse) => {
  vi.stubGlobal("fetch", vi.fn().mockImplementationOnce(firstResponse)
    .mockResolvedValueOnce(Response.json({ status: "healthy" })));
  render(<ConnectionStatus />);
  expect(await screen.findByText("API unavailable")).toBeVisible();
  fireEvent.click(screen.getByRole("button", { name: "Check again" }));
  expect(await screen.findByText("Connected to API")).toBeVisible();
});
