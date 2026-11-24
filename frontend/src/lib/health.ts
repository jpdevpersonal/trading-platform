export type HealthResponse = { status: "healthy" };

export async function getHealth(signal: AbortSignal): Promise<HealthResponse> {
  const base = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (!base) throw new Error("Set NEXT_PUBLIC_API_BASE_URL before starting the web app.");

  const response = await fetch(new URL("/api/v1/health", base), {
    signal: AbortSignal.any([signal, AbortSignal.timeout(5000)]),
    headers: { Accept: "application/json" },
    cache: "no-store",
    credentials: "omit",
  });
  if (!response.ok) throw new Error(`Health check returned HTTP ${response.status}.`);
  const body: unknown = await response.json();
  if (typeof body !== "object" || body === null || !("status" in body) || body.status !== "healthy") {
    throw new Error("Unexpected API health response.");
  }
  return { status: body.status };
}
