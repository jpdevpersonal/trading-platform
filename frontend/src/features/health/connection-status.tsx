"use client";

import { useEffect, useState } from "react";
import { getHealth } from "../../lib/health";

const labels = {
  loading: "Checking API connection…",
  healthy: "Connected to API",
  unavailable: "API unavailable",
};

export function ConnectionStatus() {
  const [state, setState] = useState<keyof typeof labels>("loading");
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    getHealth(controller.signal).then(
      ({ status }) => { if (!controller.signal.aborted) setState(status); },
      () => { if (!controller.signal.aborted) setState("unavailable"); },
    );
    return () => controller.abort();
  }, [attempt]);

  return (
    <section aria-labelledby="connection-title" className="panel mt-8">
      <p className="eyebrow">System check</p>
      <h2 id="connection-title" className="mt-2 text-xl font-semibold">API connection</h2>
      <p className="mt-2 text-slate-600">Checks the local API process, not database readiness.</p>
      <div className="mt-6 flex flex-wrap items-center justify-between gap-4 border-t border-slate-200 pt-4">
        <p role="status" className="flex items-center gap-2 font-semibold">
          <span aria-hidden="true" className={`indicator ${state}`} />{labels[state]}
        </p>
        <button type="button" disabled={state === "loading"} onClick={() => {
          setState("loading");
          setAttempt((value) => value + 1);
        }}>Check again</button>
      </div>
      {state === "unavailable" && <p className="mt-3 text-sm">Check that the API is running and the local URL is configured, then try again.</p>}
    </section>
  );
}
