# Milestone 4 — Import and view daily stock prices

**ID** M04 · **Specification** 1.0 · **Status** US/Massive scope approved 28 November 2026; implemented and locally verified; live/hosted/review evidence outstanding  
**Baseline** jpdevpersonal/trading-platform at `9259f496f82d12fab7e3d725edac2a070e310cd6` · 26 November 2026

## 1. Objective

Choose Apple or Microsoft in the local browser, select a completed date range, click **Import daily prices**, and see saved daily open, high, low, close and volume (OHLCV). Repeat the import and see zero new rows for unchanged history. Reopen the page later to see the stored prices, their source and the last successful refresh.

## 2. Dependencies and decisions

Milestone 3 was merged into main at 21:14:28 UTC on 26 November. Its report records 43 unit, 45 integration, six frontend unit and eight Chromium passes, plus six launcher regressions. The report predates the hosted result. The hosted verify job for PR head `ddf6671f446aec786b80dfffe1361190c149a893` subsequently passed. GitHub showed no submitted PR reviews. The merge is confirmed independently of formal review evidence. [PR 3](https://github.com/jpdevpersonal/trading-platform/pull/3) · [Hosted verify](https://github.com/jpdevpersonal/trading-platform/actions/runs/45895458265/job/140168115238) · [M03 report](03-backend-api-and-database-verification.md)

Decision for the owner: use Massive Stocks Basic for US-listed equities, on an individual plan for personal use, or require another market (especially London) in this milestone. This specification assumes the US option. Invoking the supplied prompt with the recommended choice approves that scope. A London requirement changes instrument mapping, coverage and currency units. Resolve it before implementing a provider adapter. [Provider comparison](04-market-data-providers.md)

Retain the accepted [architecture](../architecture.md), [ADR 0001](../decisions/0001-local-first-modular-monolith.md), [requirements](../requirements.md) and [engineering standards](../engineering-standards.md). No new application service or SDK is needed. The API key is needed only for a real import. Development, tests, startup and stored reads can proceed without it.

## 3. In scope

One manual, synchronous import for one selected instrument and closed historical period. One provider and one raw daily series per instrument. Explicit opt-in real-listing setup, one additive EF migration, PostgreSQL-backed reads and one accessible table page reachable from Dashboard. Document the local workflow and prove it end to end.

## 4. Out of scope

Broker accounts, broker credentials or writes, orders, CSV uploads, streaming, intraday data, scheduling, bulk backfill, charts, corporate-action adjustment, screening, AI, watchlist changes, public deployment and paid subscriptions. Keep existing previews clearly labelled as sample data.

## 5. Technical design

### Listing and selection

M03 creates an empty catalogue. Its optional sample fixture contains fictional MICs, and a ticker can repeat across exchanges. Add a separate transactional, repeatable opt-in SQL setup for Apple (XNAS/AAPL/USD) and Microsoft (XNAS/MSFT/USD) as non-sample instruments. Use the existing identity convention. Reuse any compatible existing logical listing and its UUID; reject conflicting reserved UUIDs, sample flags or incompatible currencies atomically. Never reclassify a sample or auto-seed during startup/migration. Document the exact operator command. Keep the provider ticker mapping small and explicit in backend code keyed by MIC, symbol and currency. Test both mappings and verify the identities against provider reference information during live setup. No symbol search, listing editor or new reference-data service.

Use the existing paged M03 catalogue and UUID to select a listing. Label it with symbol and MIC; show sample status and guide the user if none are eligible. History GET exposes import eligibility so the browser needs no copy of the mapping. Re-check eligibility on POST before any outbound request.

### Provider and data rules

Application owns the import/read use cases and ports. Infrastructure supplies a typed HTTP adapter and EF persistence. Domain owns listing-independent daily-bar invariants and has no transport or ORM references. The API composes these dependencies. The typed frontend calls the local API only.

Massive Custom Bars GET, fixed HTTPS provider host, mapped ticker, multiplier 1, timespan day, inclusive from/to, adjusted=false, ascending order and an explicit limit sufficient for the bounded range. Use server-side bearer auth. Reject an unexpected continuation as incomplete for this bounded daily query. Never silently accept truncation, follow arbitrary URLs or substitute a demo response. Accept a documented successful zero-result response. Malformed envelopes and provider error envelopes, including those sent with HTTP 200, fail safely. One process-wide budget of five outbound requests per rolling minute for this local backend. Count all real attempts, with bounded timeout (e.g. 10 seconds), no automatic retries and prompt cancellation. Respect upstream throttling and Retry-After when supplied.

Accept exact calendar dates. Import yesterday or earlier in America/New_York; maximum 366 calendar days per request inclusive, start within the rolling two-calendar-year plan window. Default UI to the last 30 calendar days ending yesterday there. Convert provider Unix milliseconds to the New York trading date using a timezone-aware conversion. Persist that date as PostgreSQL date. Store raw daily OHLC and volume using explicit PostgreSQL numeric precision (proposed numeric(28,10)), mapped to C# decimal. Reject values outside supported precision/scale rather than silently rounding. Prices must be positive, low ≤ open/close ≤ high and volume nonnegative. Allow fractional volume. Require one bar per returned session, matching the requested listing/basis and within range. Reject duplicate dates within a single provider response. Do not manufacture holiday or missing-session rows. A returned bar is the provider’s daily aggregate, not an assertion of the official closing auction price.

### Database and refresh semantics

One daily-bar table with mandatory instrument FK, trading date, OHLCV, source and raw price basis. Enforce a database unique key on (InstrumentId, TradingDate). One refresh record per instrument with source, UTC time (timestamptz) and the inclusive range of its most recent successful request. Currency comes from the verified instrument. Persist provenance independently of runtime config. Do not mix sources or price bases for this series.

Fetch and validate the entire bounded response before opening the write transaction. Within the transaction serialize writers for that instrument (e.g. lock its existing catalogue row), apply the insert/change comparison against current rows, and advance refresh metadata in the same commit. Date absent → insert; identical → unchanged; different values for the same key → update. Leave dates absent from the response intact, including dates outside an overlap. The returned counts refer only to this request. Compute the UTC completion timestamp after acquiring the lock and keep it monotonic per stock. A valid empty interval is a successful check and advances last refresh, with all counts zero. Failed or cancelled work before commit preserves prior state. A retry after an ambiguous client disconnect is safe. A recorded refresh describes the last request for that stock, not freshness of every saved date.

### HTTP contracts

Both operations take the same canonical nonempty hyphenated UUID rules as M03. Use the existing safe Problem Details conventions and document schemas/statuses in Development OpenAPI. Retain M03 catalogue payloads.

| Operation under /api/v1/instruments/{id} | Inputs | Successful response (200) |
| --- | --- | --- |
| POST /daily-prices/import | application/json with required from and to as YYYY-MM-DD | instrumentId, source, from, to, inserted, updated, unchanged, lastRefreshedAtUtc. Counts sum to the number of valid returned bars. |
| GET /daily-prices | Required from and to as date query parameters, inclusive, maximum 366 days. Older stored data may be read while licensed. | instrumentId, currency, canImport, items (newest first), source, priceBasis, lastRefreshedAtUtc, lastRefreshFrom, lastRefreshTo. Each item has tradingDate, open, high, low, close, volume. |

Dates are ISO dates, timestamps UTC with Z, and decimal fields are invariant decimal strings to preserve precision through JavaScript. GET returns 200 with an empty array for no stored bars in range. Source/basis and refresh fields are null before first success; show the configured import provider separately. GET reads committed rows and matching metadata from a consistent database snapshot, sends no external traffic and works without a key. Use a fresh read after a successful import. Disable stale HTTP/Next caching for these reads. A failed import displays its error alongside previously stored results.

| Condition | Contract |
| --- | --- |
| Bad UUID/date, missing/repeated query values or range violation | 400 with field guidance |
| Well-formed unknown stock | 404 |
| Sample or unmapped stock on POST | 422, no outbound call |
| Unsupported request media type | 415, no import |
| Explicit foreign browser origin on POST | 403 before mutation or provider call |
| Local or upstream quota exhausted | 429, safe retry guidance and Retry-After when known |
| Missing provider key or upstream entitlement denied | 503 with a safe setup/entitlement message |
| Other provider failures / timeout | 502 / 504 respectively, safe retry message |
| Unexpected database failure | Generic 500 following M03 |

M03 permits only GET and Accept in CORS. Permit the required JSON POST, Content-Type and OPTIONS preflight for the existing explicit loopback UI origins. Enforce JSON and validate a supplied browser Origin on the import route before work. Continue accepting local non-browser clients without an Origin. Keep listeners loopback-only. Store the key in ignored backend configuration, never NEXT_PUBLIC_*, headers captured in logs, query strings, fixtures or report output.

### Browser

Add /price-history and a Dashboard link. Use labelled listing/date controls, a keyboard-operable submit button, visible focus, an accessible status region and a semantic table. Save the chosen stock and range in the page URL so reload reads the same view. Paginate the existing catalogue as needed. Display stock identity and quote currency, **Massive · Unadjusted daily prices**, an explicitly UTC **Last successful refresh for this stock** with its range, and **Latest trading date in this view**. An empty range can coexist with a previous refresh for a different range. Handle empty catalogue, no imports yet, no rows within range, provider not configured, throttling and retry. Avoid overlapping submissions in the UI. Ignore or cancel stale responses when selection changes. Visiting, reloading or changing filters must never import.

## 6. Deliverables

A working price-history page and navigation, two versioned HTTP operations, provider adapter and ports, explicit migration and real-listing setup, local runbook and verification evidence. Implementation updates the existing milestone index with the active spec and report. Preserve earlier evidence and all unrelated scope.

## 7. Numbered acceptance criteria

All criteria are required for milestone closure. The checklist separates local automated evidence, live-provider evidence and subsequent PR/owner evidence.

| ID | Requirement | Pass condition and evidence |
| --- | --- | --- |
| M04-REP-001 | Establish the working baseline | Record origin, branch, HEAD, merge ancestry and relevant later changes. Preserve unrelated work and record the owner’s provider/market choice. |
| M04-CAT-001 | Make a real stock selectable | On a migrated empty catalogue, run the opt-in listing setup twice. AAPL and MSFT on XNAS appear once each as non-sample USD listings without price rows. A conflicting identity rolls back the setup. |
| M04-CAT-002 | Bind each request to the selected listing | Exercise the M03 paged catalogue, same symbol on another MIC, sample, unmapped and absent UUID. Only an explicitly mapped non-sample listing reaches the provider. |
| M04-MD-001 | Fetch the requested daily series | Adapter tests inspect the outbound method, ticker, range, frequency, raw adjustment and auth placement. Validate ticker, adjustment, response status, result count and completeness using synthetic payloads. |
| M04-MD-002 | Preserve daily values and trading dates | Prove exact decimal round trips including fractional volume, valid OHLC bounds, session-date conversion across US daylight-saving changes and no fabricated dates. |
| M04-MD-003 | Bound calls and handle provider failure | Tests cover missing key, bad entitlement, 429, timeout, malformed and incomplete results. Enforce the shared outbound budget and cancellation. Record safe diagnostics and zero partial writes. |
| M04-DB-001 | Upgrade PostgreSQL explicitly | Review generated SQL and snapshot. Test fresh migration and upgrade from populated M03 on real PostgreSQL. Existing instruments survive, new tables start empty and a second migration application is a no-op. |
| M04-DB-002 | Keep one bar per stock and session | Inspect the database primary/unique key and foreign key. Run identical and overlapping imports and a provider correction. Assert exact dates, row counts, corrected fields and unaffected rows. |
| M04-DB-003 | Keep concurrent and interrupted imports consistent | Use independent requests/connections with overlapping imports. Assert duplicate-free rows, coherent counts and refresh metadata. Force a write failure before commit and verify full rollback. |
| M04-DB-004 | Record the last successful refresh | Assert a UTC timestamp and requested bounds per instrument. Verify first import, identical repeat, successful empty interval, failed attempt and isolation between two stocks using a controlled clock. |
| M04-API-001 | Expose the import command | Assert JSON-only POST and its typed summary, zero/positive counts, date limits, invalid ID and missing/ineligible listing. Inspect Development OpenAPI for request, responses and safe errors. |
| M04-API-002 | Read stored history independently | Assert bounded GET, newest-first ordering, exact string-valued decimals, dates, source, currency and nullable refresh metadata. Read after API restart with no configured provider key or provider traffic. |
| M04-API-003 | Preserve localhost and credential boundaries | Verify allowlisted JSON POST preflight, untrusted origin rejection before import, unsupported media types, redacted logs/browser traffic and unchanged health and readiness contracts. |
| M04-UI-001 | Choose a stock and its date range | Navigate from Dashboard to /price-history by keyboard. Page the catalogue, distinguish listings and see how to set up an empty catalogue. Selection and date changes only read stored data. |
| M04-UI-002 | View the result and provenance | Import via the page and inspect dated OHLCV rows, source, USD, raw price basis, latest stored session in view and instrument-level refresh time/range. Reload and recover the same selection, range and stored result. |
| M04-UI-003 | Make failures and progress understandable | Exercise loading, empty, unchanged, unavailable and throttled states with accessible status and focus. Preserve saved data on import errors. Switching stocks never shows another stock’s rows or metadata. |
| M04-TST-001 | Pass the existing local gates | Record locked restores, EF drift check, .NET build/format/unit/integration outcomes, launcher regressions, frontend lint/typecheck/unit/build and existing Chromium smoke suite. Report counts and skips. |
| M04-TST-002 | Prove the complete workflow without a key | Run Chromium through real HTTP and disposable PostgreSQL with only the upstream provider simulated. Import, repeat, reload, simulate upstream failure and view persisted results. Run in the PR workflow without live secrets. |
| M04-LIVE-001 | Confirm the chosen provider actually supplies bars | Owner or authorized local operator runs one recent completed range using the real provider, repeats it, and records safe counts, dates, UTC refresh and a duplicate count of zero. Keep licensed prices and keys out of evidence. |
| M04-OPS-001 | Hand over an operable, reviewed milestone | Update README and milestone index. Record per-ID evidence, actual commands, hosted verify job and revision, and owner review. Owner controls push, PR and merge. |

## 8. Testing

Use xUnit for rules/adapter and PostgreSQL/HTTP integration, Vitest/RTL for state transitions, and Playwright for the complete journey. Use synthetic upstream responses in automation and a real key only for the local live smoke. See the [verification checklist](04-daily-price-history-verification.md).

## 9. Definition of done

Every required criterion has evidence. Capture hosted verify job/revision and owner review when they occur. Without live credentials, finish independent implementation and tests and mark LIVE-001 Blocked. Passing simulation alone leaves the live check outstanding.

## 10. Completion report

Fill the companion checklist during implementation. At planning time all M04 checks are Pending. Record the actual changed files, commands, results, failures, recovery and remaining actions there.
