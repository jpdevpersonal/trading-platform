# Milestone 4 — Verification checklist and completion report

**Local result, 28 November 2026: 18 Pass, 1 Blocked, 1 Pending.** Implementation and deterministic verification are complete. M04-LIVE-001 is Blocked on an authorized Massive key; M04-OPS-001 is Pending hosted verification and owner review. The milestone is not closed. No commit, push, PR, merge or deployment was performed.

### Independent rerun — 28 November 2026, 16:03 UTC

The [20 criterion results](#5-evidence-by-criterion) are unchanged after this rerun. No new implementation defect was reproduced. The development walkthrough and the successful synthetic imports below used **different databases**.

**Selector diagnosis (development).** `git status --short --branch`, `git rev-parse HEAD`, `git merge-base --is-ancestor 9259f496f82d12fab7e3d725edac2a070e310cd6 HEAD` and `git log main..HEAD --oneline` confirmed the existing M04 working tree on `milestone-4-daily-price-history`, exactly the merged baseline, with no later commits. The initial `curl` to port 5080 was refused: both app listeners were stopped, while Compose PostgreSQL was healthy. Started `./scripts/dev.sh`. `dotnet ef dbcontext info --no-build` with the README project arguments and the launcher's backend environment identified `trading_assistant` on `127.0.0.1:5432`. Read-only `psql` queries showed both migrations already applied and both XNAS/USD listings present. No development migration, reseed or reset was needed this time.

The earlier empty selector was an empty migrated catalogue **before** the opt-in listing SQL was applied, not a filter hiding real stocks. `curl -fsS http://localhost:5080/api/v1/instruments` returned HTTP 200 with AAPL and MSFT, both `isSample:false`. In the shared browser, Dashboard -> Price history made `GET http://localhost:5080/api/v1/instruments?offset=0&limit=50` (observed response 200). Its Listing control showed **AAPL · XNAS · USD** and **MSFT · XNAS · USD**. The first page contained both; no eligibility filter removed either.

**Live attempt (M04-LIVE-001: Blocked).** Selected AAPL, chose 2026-11-23 through 2026-11-24, and clicked **Import daily prices** in the development UI. The real API returned **503** with backend key setup guidance. No Massive key was configured; the adapter rejects this case before making a provider request. PostgreSQL had zero bars before the attempt and zero bars/refresh records afterward. There was no successful live import to repeat, so **live repeat counts and duplicate behavior are unverified**. Do not interpret the synthetic results below as live coverage.

**Commands and outcomes for this run.** All checks below exited 0, unless noted above. From repository root with the pinned SDK/Node, and from `frontend/` for npm commands:

| Checks run | Observation |
| --- | --- |
| `dotnet restore --locked-mode`; `dotnet tool restore`; `dotnet build --no-restore`; `dotnet format --verify-no-changes --no-restore`; `dotnet test --no-build` with isolated `TRADING_TEST_CONNECTION` | Build: 0 warnings/errors. **91 unit + 84 integration passed**, none skipped. Includes both writer-overlap cases, deferred-commit rollback, cancellation, correction, empty interval, upstream failure and throttling. |
| `dotnet ef dbcontext info`, `migrations list`, `migrations script --idempotent`, `migrations has-pending-model-changes`, `database update` twice, then `migrations list` (each with `--no-build --project backend/src/TradingAssistant.Infrastructure --startup-project backend/src/TradingAssistant.Api`) | Fresh `trading_m04_test` on `127.0.0.1:55434`, isolated container `m04-reverify-fcfb9946`. Reviewed SQL before applying. Two pending migrations became two applied, no drift. `psql -v ON_ERROR_STOP=1` ran `real-instruments.sql` twice there; two non-sample listings, no bars. Fresh and populated-M03 upgrade assertions also passed. |
| `bash -n scripts/dev.sh`; `node --test scripts/dev.test.mjs` | **6 passed**, none skipped. Real launcher separately served both the Dashboard and API. |
| `npm ci`; `npm run lint`; `npm run typecheck`; `npm run test`; `npm run build`; `npx playwright install chromium` | Locked install passed; existing ESLint package deprecation warning. **17 tests in four files passed**, none skipped; production build included `/price-history`. |
| `npm run test:e2e`; `npm run test:e2e:prices` with disposable `TRADING_TEST_CONNECTION` for the latter only | **8 original Chromium tests + 1 integrated Chromium test passed**, none skipped. Integrated path used real frontend, Kestrel and PostgreSQL; only the Massive HTTP handler was simulated. |

**Direct browser/SQL comparison and actual restart.** In addition to both suites, started the test-only host with `dotnet run --no-build --project backend/tests/TradingAssistant.IntegrationTests --no-launch-profile -- --browser-host` and real Next.js with `npm run dev -- --port 3000`. Followed Dashboard -> Price history, paged past 50 sample listings, selected AAPL, and imported the same two dates. First import: **2 inserted / 0 updated / 0 unchanged**. Repeat: **0 / 0 / 2**. The refreshed browser showed both trading dates newest first. Independent `psql` queries against that host's isolated schema matched **all five OHLCV decimal fields** for both dates, `Massive`, `raw`, the inclusive bounds and the UTC refresh `2026-11-28T15:00:00.000001Z`. Grouping `DailyPrices` by instrument/date with `HAVING count(*) > 1` returned **zero duplicate groups**.

For the restart check, ran the **compiled production API without a key** against those exact imported rows (no copies, SQL inserts or reseed), on port 5094. Stopped and restarted it on the same address/schema (PID **67029 -> 67551**). Before/after history response SHA-256 was identical (`59568d9e4c4cf57d68f38cc7810cdc15c2f462a61821471e6175c93180184064`). Pointed the frontend at this API and reloaded the same page URL. Confirmed both rendered rows matched the response field-for-field, with unchanged source/basis/refresh and `Cache-Control: no-store`. The restarted API's HTTPS proxy was an instrumented loopback listener that counted connections without forwarding or logging content: **0 outbound connections** during these reads. The browser test host's logs also showed actual OPTIONS 204 and POST 200 for both synthetic imports.

**Disposition.** The detailed per-ID evidence below applies to this rerun: REP, both CAT, all three MD, all four DB, all three API, all three UI, and both TST criteria **Pass locally**. LIVE remains **Blocked** as observed above; OPS remains **Pending** because there is no M04 hosted run or owner review to report. Test-only schema cleanup left zero `m03_*` schemas; `docker stop m04-reverify-fcfb9946` removed only the owned disposable instance. Development prices were not modified. No push or merge.

## 1. Establish the target

- Working directory: `/home/julian/Development/trading-platform`. Branch: `milestone-4-daily-price-history`. Origin: `git@github.com:jpdevpersonal/trading-platform.git`.
- Starting and tested HEAD: `9259f496f82d12fab7e3d725edac2a070e310cd6`, exactly local `main` and `origin/main`. The required merge is an ancestor (exit 0); `git log main..HEAD --oneline` was empty. There were no intervening commits to adapt.
- Initially only the M04 prompt, specification, provider comparison and this checklist were untracked. Preserved those files and earlier milestone evidence. The owner explicitly approved US-listed stocks and Massive Stocks Basic in this session.
- Tools: .NET SDK `10.0.404`, EF tool `10.0.15`, Node `v24.22.3`, npm `11.19.1`, Docker client/server `29.2.1`, Compose `v2.40.3-desktop.1`, PostgreSQL `17.12`. Selected the already installed pinned runtimes using `DOTNET_ROOT=$HOME/.dotnet` and prepending that directory plus `$HOME/.nvm/versions/node/v24.22.3/bin` to `PATH`.
- Owned disposable container: `trading-m04-fcfb9946-test`, bound to `127.0.0.1:55434`, database `trading_m04_test`, image `postgres:17.12`. It was created with `docker run -d --rm`, using only dummy test credentials, and polled with `pg_isready`. No development database or volume was changed.
- xUnit and the browser host reused M03's loopback/`_test` target guard and uniquely named schema harness. Browser -> Kestrel -> PostgreSQL used the same isolated schema. The test factory explicitly clears inherited Massive credentials and fixes market time; only the provider HTTP handler is simulated.
- No nonempty Massive key was present in the process environment or ignored backend environment file (presence-only checks, no values printed). No authenticated provider request was made. Public [Custom Bars](https://massive.com/docs/rest/stocks/aggregates/custom-bars) and [authentication](https://massive.com/docs/rest/quickstart) documentation were checked.

## 2. Automated checks to run

Actual commands below ran from the repository root unless labelled `frontend/`. `infra` means `backend/src/TradingAssistant.Infrastructure`; `api` means `backend/src/TradingAssistant.Api`. EF commands below used `--project "$infra" --startup-project "$api"`. `TRADING_TEST_CONNECTION` and, for manual EF/API checks, `ConnectionStrings__TradingAssistant` targeted only the owned disposable database; values are deliberately omitted.

| Command / action | Exit | Actual result |
| --- | --- | --- |
| `git status --short --branch`; `git rev-parse HEAD`; `git merge-base --is-ancestor 9259f496f82d12fab7e3d725edac2a070e310cd6 HEAD`; `git log main..HEAD --oneline`; credential-redacted origin inspection | 0 | Exact merged baseline and initial worktree recorded above. |
| `dotnet --version`; `node --version`; `npm --version`; `docker version --format '{{.Client.Version}} {{.Server.Version}}'`; `docker compose version` | 0 | Pinned tool versions recorded above. PostgreSQL `SELECT current_database(), version()` confirmed the intended disposable target. |
| `dotnet restore --locked-mode`; `dotnet tool restore`; `frontend/: npm ci` | 0 | Locked restores passed; no package, runtime or lockfile version changes. npm reported zero vulnerabilities and an existing ESLint deprecation warning. |
| `dotnet ef migrations add DailyPrices --project "$infra" --startup-project "$api" --output-dir Migrations` | 0 | Generated `20261128153241_DailyPrices`, designer and updated snapshot. |
| `dotnet ef migrations script --idempotent --no-build --project "$infra" --startup-project "$api" --output <scratch>/m04-migration.sql` | 0 | Reviewed generated SQL: additive tables, mandatory FKs, composite daily PK, exact numeric columns and UTC refresh; no listing or price seed. Scratch SQL removed after review. |
| `dotnet ef dbcontext info --no-build`; `dotnet ef migrations list --no-build` with project arguments above | 0 | `trading_m04_test`, `tcp://127.0.0.1:55434`, command timeout 5; both migrations initially pending. |
| `dotnet ef database update --no-build` with project arguments above, twice | 0 each | Both migrations applied explicitly; second invocation no-op. Separate automated fresh and populated-M03 upgrade tests passed. |
| `dotnet ef migrations has-pending-model-changes --no-build` with project arguments above | 0 | No pending model changes, including final rerun. |
| `dotnet format --no-restore`; later scoped formatting of the changed endpoint/unit-test files | 0 | Normalized formatting, including generated migration. |
| `dotnet build --no-restore`; `dotnet format --verify-no-changes --no-restore`; `dotnet test --no-build` | 0 finally | **91 unit + 84 integration passed, zero failures/skips.** Final build had zero warnings/errors. Tests include a measured ten-second provider deadline (asserted 9-15 seconds), real PostgreSQL, concurrent HTTP requests and rollback. |
| `dotnet test backend/tests/TradingAssistant.UnitTests --no-restore --filter FullyQualifiedName~DailyPriceTests` | 0 | Initial focused run: 46 passes; subsequently added timeout and entitlement-envelope cases, both in the final full run. |
| `dotnet test backend/tests/TradingAssistant.IntegrationTests --no-restore --filter 'FullyQualifiedName~DailyPrice'` | 1 initially, 0 after fix | Initial 37 pass / 1 fail exposed framework-generated 415 without Problem Details; fixed at the route, then 38 passed. Added consistent-read concurrency case later. |
| `bash -n scripts/dev.sh`; `node --test scripts/dev.test.mjs` | 0 | **6 launcher regressions passed**, zero skips. Keyless real API startup independently verified below; existing launcher implementation/configuration unchanged. |
| `frontend/: npm run lint`; `npm run typecheck`; `npm run test`; `npm run build` | 0 | **17 tests in four files passed**, zero skips. Strict types, lint and production build passed, including dynamic `/price-history`. |
| `frontend/: npx playwright install chromium`; `npm run test:e2e` | 0 | **8 existing Chromium smoke tests passed** with no PostgreSQL configuration supplied to their production API. Previews remain independent. |
| `frontend/: npm run test:e2e:prices` with isolated `TRADING_TEST_CONNECTION` | 0 finally | **1 full-workflow Chromium test passed**. Real Next.js, real ASP.NET/Kestrel, real PostgreSQL; only upstream HTTP simulated. Import/repeat/reload/failure/retry, paging, keyboard focus, provenance and 360px layout checked. |
| `git diff --check`; scoped `git diff`; production `ProjectReference`/`PackageReference` inspection | 0 | Approved layer references and package pins unchanged. CORS, auth placement, error handling, SQL, snapshot, new files and test-only simulation wiring reviewed. |

### Failures and recovery

- Adding POST to default CORS initially broke two existing health-route assertions (expected GET, received GET/POST). Replaced it with a named daily-price-only policy; existing tests were not weakened. The full backend rerun passed.
- Removed import `Accepts` routing metadata in favor of equivalent explicit OpenAPI metadata: JSON and foreign-origin checks now run in the handler, so even a foreign `text/plain` POST receives safe 403 before work, and local unsupported media receives Problem Details 415.
- The executable test host initially selected the generated test entry point; disabled test SDK entry-point generation. Its first real launch then exposed a content-root fallback unsuitable outside the test runner; made the test factory's API content root explicit. No production workaround was added.
- The first Chromium workflow assertion matched Next.js's route announcer as well as the application alert. Scoped it to `main`; the workflow then passed.
- Initial Playwright termination left two owned schemas. Added graceful SIGTERM shutdown to the test-host configuration and dropped only those two exact schemas. The final browser run left **zero** isolated test schemas.
- The first frontend run excluded the new `.test.ts` client tests because the existing pattern selected only `.tsx`. Extended the pattern; all four files / 17 tests then ran and passed.

## 3. Reproducible import scenarios

Executed with synthetic dates A = 2026-11-23, B = 2026-11-24, C = 2026-11-25 and controlled UTC market time 2026-11-28T15:00:00Z. Values were invented, not licensed prices. The scenarios below passed in `DailyPriceApiTests` and `DailyPricePersistenceTests`.

| Action | Observed result |
| --- | --- |
| Run fresh and populated-M03 upgrade paths; apply the listing setup twice | M03 records survive byte-for-byte. The two supported real listings exist once each; the fictional fixtures remain sample. Fresh history is empty with null refresh. |
| Import [A,B] returning A and B | 2 inserted, 0 updated, 0 unchanged; two rows, source Massive, raw basis and first UTC refresh. |
| Repeat exactly [A,B] | 0 inserted, 0 updated, 2 unchanged; same two rows and values. Only refresh metadata needs to advance. |
| Import [B,C] with corrected B and new C | 1 inserted, 1 updated, 0 unchanged; three distinct dates; A unchanged. Re-read [A,C] newest first. |
| Request a valid weekend with zero results | Three saved rows survive. All counts zero. Refresh shows that weekend range, with no invented trading dates. |
| Import another stock or same symbol on an unsupported MIC | Supported stock has its own series and timestamp. Unsupported listing is rejected before the provider is called. |
| Concurrent identical requests on a fresh series | Both complete within the test budget, two distinct dates total, summed inserted count two. Readers never see half an import. Also exercise partially overlapping ranges. |
| Force bad payload, quota, cancellation before commit and mid-transaction failure | No new/changed bars or refresh metadata for failures. For quota, verify retry guidance. Reconnect/retry successfully. Client disconnect after commit may already have saved a complete result. |
| Restart API, remove key, disable provider access, reload same page URL | Same rows and UTC metadata visible. Zero outbound calls on GET, reload or selection. A new import with missing key shows actionable setup feedback. |
| Inspect database uniqueness | Independently attempt a duplicate key in a rolled-back test transaction and observe a uniqueness violation. Group rows by instrument UUID and trading date with HAVING count(*) > 1; expect zero groups. |

Additional automated cases cover the DST transition window, invalid OHLC, out-of-range sessions, precision overflow, unexpected ticker, adjusted data, duplicate response dates and unexpected continuation. Empty and multipage catalogues, stock switching with outstanding requests, safe no-data states and accessible focus/status are covered. Real HTTP preflight checks verify the configured loopback allowlist.

`DailyPriceTests` verifies spring/fall New York date conversion, exact fractional volumes, invalid precision/scale, malformed/status-error envelopes, tickers, raw adjustment, result/query counts, continuation rejection, successful zero-result envelopes, entitlement, 429, rolling quota, timeout and cancellation. RTL exercises empty/unsupported/multipage catalogues, stale reads/imports, 503/429 feedback, explicit retry and preserved saved results. Kestrel preflight was also exercised manually.

### Explicit SQL and actual production-process restart

Ran twice, from root:

```bash
docker exec -i trading-m04-fcfb9946-test psql -U trading_test -d trading_m04_test -v ON_ERROR_STOP=1 \
  < infrastructure/seed/real-instruments.sql
```

Both exited 0. Independent SQL showed exactly AAPL/MSFT on XNAS, non-sample USD, zero daily rows and zero refresh rows before synthetic restart setup. The migration history contained exactly the M03 and M04 IDs. Key/FK violations, conflict rollback and compatible-UUID retention were checked against PostgreSQL by tests.

`node <scratch>/m04-restart.mjs` (exit 0) spawned the **compiled production API**, not the test factory, with the disposable connection, localhost listener and empty `Massive__ApiKey`. It checked actual HTTP statuses: health/ready 200, missing key 503, foreign `text/plain` Origin 403, JSON preflight 204 with the correct allowlist, and Development OpenAPI with required `from`/`to`. It inserted one explicitly synthetic SQL row solely for restart verification, then restarted the process **39299 -> 39379**. Parsed history and UTC metadata were identical after restart; GET had `Cache-Control: no-store`. Captured diagnostics contained no authorization headers, credential values or synthetic key. The script stopped both owned processes and was removed after verification. The full browser test, not this direct-SQL check, provides import-path evidence.

Independent duplicate-group SQL returned **0**. Final test-schema query returned **0** after the graceful browser run.

Cleanup: `docker stop trading-m04-fcfb9946-test` exited 0; `--rm` removed the owned disposable container. `ss -ltnp '( sport = :3000 or sport = :5080 or sport = :55434 )'` showed all test/application ports released. Only named scratch SQL/restart scripts were removed; existing ignored configuration and development volumes were preserved.

## 4. Owner’s live walkthrough

**Blocked:** the development browser import was attempted on the rerun and returned 503 before outbound HTTP; no authorized Massive key was configured. Deterministic tests and public documentation do not prove live entitlement, listing reference identities or actual bars. The following remaining steps must be run by the owner/authorized operator; keep credentials and licensed prices out of the shared evidence.

1. Apply the reviewed migration and opt-in listing script to an intended local database. Configure the Massive key only in the ignored backend environment and start the existing launcher. Check local health/readiness and open Dashboard → Price history.
2. Select AAPL and a short, recent completed date range containing known US trading sessions. Import, record source, range, counts, checked-at UTC, latest session date and success status. Compare dates and values privately with the provider for one session. Keep only the comparison outcome in shared evidence.
3. Repeat the same range. Expect zero inserts. Unchanged prices count as unchanged; if the provider corrected a bar, record the update. Check the database for duplicate stock/date groups.
4. Refresh the browser, restart the API and confirm durable history. If live access is unavailable, record the specific missing credential, entitlement or connectivity. Complete automated gates and leave the live row Blocked until observed.

## 5. Evidence by criterion

| ID | Capture | Result | Actual evidence / blocker |
| --- | --- | --- | --- |
| M04-REP-001 | Establish the working baseline | Pass | Section 1 records exact merged SHA, ancestry, origin, local branch, initial untracked files and owner approval. No later commits. |
| M04-CAT-001 | Make a real stock selectable | Pass | Manual double SQL setup on empty migrated catalogue. `RealListingSetupIsRepeatableAndReusesCompatibleUuidWithoutPrices` plus five `ListingConflictRollsBackEarlierListingAtomically` cases verify reuse, flags/currency and all-or-nothing collisions. |
| M04-CAT-002 | Bind each request to the selected listing | Pass | `CatalogueSelectionRejectsSampleUnmappedOtherMicAndAbsentIdsWithoutHttp`, two provider mappings and ineligible adapter cases. Browser pages past 50 samples to AAPL; UUID-based selection, no client mapping. |
| M04-MD-001 | Fetch the requested daily series | Pass | `OutboundRequestUsesFixedHostMappedTickerRawDailyRangeAndBearerOnly`; synthetic envelope cases validate status/ticker/basis/count/completeness and reject continuation. |
| M04-MD-002 | Preserve daily values and trading dates | Pass | Exact decimal/fractional volume PostgreSQL round trips, newest-first ISO dates, five DST conversion cases, domain OHLCV/precision rejection and zero-result tests. No holiday rows fabricated. |
| M04-MD-003 | Bound calls and handle provider failure | Pass | Missing key, 401/403/HTTP-200 entitlement denial, 429 and Retry-After, shared five-attempt rolling budget, malformed/incomplete responses, measured ten-second timeout and prompt cancellation. HTTP failure tests retain saved rows/metadata. |
| M04-DB-001 | Upgrade PostgreSQL explicitly | Pass | Generated/reviewed SQL and snapshot; manual double update; `FreshAndPopulatedM03UpgradeAreExplicitAdditiveAndRepeatable` for both paths, original M03 records byte-for-byte unchanged, empty new tables, EF drift check. |
| M04-DB-002 | Keep one bar per stock and session | Pass | Composite PK and FK violations tested in PostgreSQL. Workflow asserts first 2/0/0, repeat 0/0/2, correction/overlap 1/1/0, exact corrected fields, retained A, three distinct dates; duplicate query zero. |
| M04-DB-003 | Keep concurrent and interrupted imports consistent | Pass | Independent concurrent HTTP clients with identical and overlapping ranges; coherent counts and monotonic last-request metadata. Deferred commit trigger forces rollback of updates/inserts/refresh, successful retry, and cancellation while waiting on the parent lock. Consistent snapshot test interleaves a writer commit between the two read queries. |
| M04-DB-004 | Record the last successful refresh | Pass | Controlled-clock first/repeat/correction/empty/weekend/second-stock tests; timestamp kind and exact UTC values, monotonic microsecond advance, requested bounds and failed-attempt preservation. |
| M04-API-001 | Expose the import command | Pass | Typed JSON summary, positive/zero counts, canonical UUIDs, missing/invalid/repeated dates, 366-day and two-year/NY yesterday boundaries, missing/ineligible listings, safe statuses. Development OpenAPI request required fields verified over actual HTTP. |
| M04-API-002 | Read stored history independently | Pass | Range validation, empty arrays, exact string OHLCV, currency/provenance/nullable metadata, descending dates and no-store headers. Separate keyless test hosts plus actual production PID restart preserve identical stored results with no provider request. |
| M04-API-003 | Preserve localhost and credential boundaries | Pass | Both allowed origins' POST preflights, foreign/null/trailing-slash origins and all three request media cases; no outbound call before rejection. Browser request inspection has no provider traffic or authorization. Existing health/readiness/CORS tests retained; manual production statuses/log checks passed. |
| M04-UI-001 | Choose a stock and its date range | Pass | Chromium keyboard Dashboard link/focus, 50-row catalogue pagination and distinct labels. RTL empty setup guidance and filter-only reads; URL carries stock/from/to. |
| M04-UI-002 | View the result and provenance | Pass | Chromium real import shows dated OHLCV, source/raw basis, USD, latest session and UTC stock refresh/range; repeat and reload retain exact selection/range/table without importing on reload. |
| M04-UI-003 | Make failures and progress understandable | Pass | RTL loading, empty view vs previous refresh, unchanged import, 503/429/retry, no overlapping submit, cancellation and late-stock-response rejection. Chromium upstream failure preserves saved table and recovers; semantic table/status/alert and visible keyboard focus checked. |
| M04-TST-001 | Pass the existing local gates | Pass | Section 2: locked restores, build/format/EF drift, 91 unit + 84 integration, 6 launcher, lint/types, 17 frontend unit, production build and 8 original Chromium passes. Zero skips. |
| M04-TST-002 | Prove the complete workflow without a key | Pass | `npm run test:e2e:prices`: 1 Chromium pass through real Next/API/PostgreSQL with only Massive HTTP simulated, including import/repeat/reload/failure/retry. Dedicated PR step and local README command; no live secrets or production fake mode. Hosted execution itself remains under OPS-001. |
| M04-LIVE-001 | Confirm the chosen provider actually supplies bars | Blocked | No authorized configured Massive key. Remaining: validate AAPL/MSFT reference identity/entitlement, import a recent completed range, privately compare a session, repeat, capture safe counts/dates/UTC refresh, confirm zero duplicate groups and persistence. No live prices or key in evidence. |
| M04-OPS-001 | Hand over an operable, reviewed milestone | Pending | README, index and all 20 criteria updated; local evidence complete. No M04 hosted job/revision or owner review observed. Owner must push/open PR, record the green hosted job and tested revision, complete the live check and review before milestone approval/merge. |

## 6. Handoff

### Delivered paths

- Backend: [daily-bar domain rules](../../backend/src/TradingAssistant.Domain/DailyBar.cs), [Application use cases and ports](../../backend/src/TradingAssistant.Application/DailyPrices.cs), [Massive adapter/budget](../../backend/src/TradingAssistant.Infrastructure/MassiveDailyPriceProvider.cs), [transactional store](../../backend/src/TradingAssistant.Infrastructure/DailyPriceStore.cs), context/mappings/DI, [daily HTTP endpoints](../../backend/src/TradingAssistant.Api/DailyPriceEndpoints.cs), generated migration/designer/snapshot and backend environment example.
- Data setup: [opt-in real listings](../../infrastructure/seed/real-instruments.sql). Existing sample SQL and catalogue payloads preserved.
- Browser: [price-history page](../../frontend/src/app/price-history/page.tsx), [accessible feature](../../frontend/src/features/prices/price-history.tsx), [typed client](../../frontend/src/lib/daily-prices.ts), Dashboard link, component/client tests and Vitest discovery.
- Verification: new daily unit/API/persistence tests; test-only [Kestrel host](../../backend/tests/TradingAssistant.IntegrationTests/BrowserHost.cs) and HTTP fixtures; [Chromium journey](../../frontend/tests/prices/daily-prices.spec.ts), dedicated Playwright configuration/npm script and [PR workflow](../../.github/workflows/foundation.yml). Existing migration-count assertion now expects both migrations, with all prior behavior tests retained.
- Handoff: [README](../../README.md), [milestone index](../milestones.md), approved specification/provider status and this evidence. No production dependency or runtime pins changed.

The owner can apply the migration and opt-in listing script, open Dashboard -> Price history, read persisted history without a key, and configure a backend-only key when ready for the live walkthrough. No background refresh, chart, broker integration, order, paid service or unrelated milestone was added.

**Hosted M04 job URL/revision: not observed. Owner review: not observed.** Local tested revision is the uncommitted working tree on `9259f496f82d12fab7e3d725edac2a070e310cd6`, not a claim that merged main already contains this feature. The owner retains control of staging, commits, pushes, PRs and merge.
