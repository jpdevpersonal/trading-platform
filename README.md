# AI Trading Assistant

A local workspace for human-led stock research. It includes a browser dashboard, four read-only sample previews, a PostgreSQL-backed instrument catalogue and manual daily price history for US-listed Apple and Microsoft through Massive Stocks Basic. It never connects to brokers or submits orders.

## First run

Use Git, Bash, the **.NET 10.0.404 SDK**, **Node 24.22.3** (bundled npm 11.19.1), and a running Docker Engine with Compose v2.20+ (`include` support). The one-command launcher supports Linux/WSL and uses `curl` and `setsid` (util-linux). Open the checkout in VS Code with Copilot for the repository workflow. The [SDK pin](global.json), [Node pin](.nvmrc), NuGet locks and [npm lockfile](frontend/package-lock.json) are also used in CI. On Linux, install Playwright's Chromium system libraries if prompted.

From the repository root, with the pinned .NET SDK on `PATH`:

```bash
# If .NET is installed in ~/.dotnet rather than on PATH:
# export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$DOTNET_ROOT:$PATH"
dotnet --version
nvm install                    # if using nvm; repeat `nvm use` in new shells
node --version
npm --version
docker version
docker compose version

# First run only. Do not overwrite existing local settings.
cp .env.example .env
cp backend/.env.example backend/.env
cp frontend/.env.example frontend/.env.local
dotnet restore --locked-mode
(cd frontend && npm ci)
```

The examples use **dummy, local-only credentials**. Keep actual values in the ignored copies. The root `.env` configures Compose; **`dotnet run` does not load it**. Match the database, user, password and port in `backend/.env` to the Compose settings. Only the public API origin belongs in `frontend/.env.local`; `NEXT_PUBLIC_*` values are visible to the browser and fixed at build time.

## Everyday development: one command

From the repository root:

```bash
./scripts/dev.sh
```

The [launcher](scripts/dev.sh) selects the pinned runtimes from `PATH`, falling back to Node in `${NVM_DIR:-$HOME/.nvm}` and .NET in `${DOTNET_ROOT:-$HOME/.dotnet}`. It checks local settings, restored dependencies, Docker and free application ports before starting anything. It never installs dependencies, overwrites configuration, or stops existing applications.

It starts PostgreSQL with Compose, loads `backend/.env` **only into the API process**, and starts the API and frontend together with logs in the same terminal. It reports **Ready** after API health, database readiness and the frontend respond. Open **http://localhost:3000**.

Readiness checks connectivity, not the application schema. The launcher never migrates or imports data. Follow [Persistence and migrations](#persistence-and-migrations) once to enable the instrument routes; the optional demo import is a separate step. The browser previews remain independent of this catalogue.

Press **Ctrl+C** once to stop the frontend and API, including their child processes. If either service exits or startup fails, the other is stopped too. PostgreSQL stays running and its volume is never deleted. Stop it separately when finished:

```bash
docker compose stop postgres
```

The launcher uses frontend port **3000**. An alternate API port is supported if `ASPNETCORE_URLS` in `backend/.env` and `NEXT_PUBLIC_API_BASE_URL` in `frontend/.env.local` match exactly (one loopback HTTP origin). Keep `FrontendOrigin=http://localhost:3000`. For other frontend ports or platforms without `setsid`, use the manual commands below.

### Manual startup (alternative)

**Terminal 1, root** — wait for PostgreSQL to accept TCP connections, then run the API in the foreground:

```bash
docker compose config --quiet
docker compose up -d --wait --wait-timeout 60
set -a; . backend/.env; set +a
dotnet run --project backend/src/TradingAssistant.Api
```

**Terminal 2, root** — with Node 24.22.3 active:

```bash
cd frontend
npm run dev
```

Open **http://localhost:3000** (or **http://127.0.0.1:3000**). The frontend binds explicitly to IPv4 loopback (`127.0.0.1`) so its listener does not depend on whether the operating system resolves `localhost` to IPv4 or IPv6. Both local frontend origins are explicitly allowed, including development hot reload.

Dashboard (`/`) links to four previews. **Sample data** means the instruments, quantities and notes are fictional, not market or account data. The previews are read-only and available without the API.

Dashboard also links to **Price history** (`/price-history`), a separate real-listing workflow. See [Daily price history](#daily-price-history-milestone-4) for explicit setup. Visiting or reloading this page never imports data.

| Route | Preview |
| --- | --- |
| `/screener` | Three sample instruments grouped by sector |
| `/watchlists` | One example list built from the same three instruments |
| `/portfolio` | Three fictional positions with illustrative quantities |
| `/research` | Two research notes linked to fictional instruments |

Use the desktop navigation rail or the wrapped navigation above the content on narrow screens. Press Tab to reach **Skip to main content** and Enter to activate it. Dashboard should say **Connected to API** when the local API process responds. Stop the API and select **Check again** to see **API unavailable**; restart it and retry to recover. The panel checks process health, not database readiness.

| Local endpoint | What it verifies |
| --- | --- |
| `GET http://localhost:5080/api/v1/health` | Process only: `200 {"status":"healthy"}`, even with PostgreSQL stopped |
| `GET http://localhost:5080/api/v1/ready` | A bounded SQL round trip, not a schema check: `200 {"status":"ready"}` or `503 {"status":"unavailable"}` |
| `GET http://localhost:5080/api/v1/instruments` | Migrated catalogue, ordered and paginated; defaults to offset 0, limit 50 |
| `GET http://localhost:5080/api/v1/instruments/{id}` | One instrument by nonempty hyphenated UUID, or `404` |
| `GET http://localhost:5080/api/v1/instruments/{id}/daily-prices?from=YYYY-MM-DD&to=YYYY-MM-DD` | Saved history and stock-level refresh metadata; no provider calls |
| `POST http://localhost:5080/api/v1/instruments/{id}/daily-prices/import` | Explicit synchronous import with JSON `from` and `to` |
| `GET http://localhost:5080/openapi/v1.json` | API document, Development only |
| `127.0.0.1:5432` | PostgreSQL 17.12, loopback only, Compose-managed named volume |

**Manual shutdown:** Ctrl+C each foreground process, then `docker compose down` from root. This retains the database volume. Do **not** add `-v` to normal shutdown.

## Tests

Launcher regression tests use Node's built-in test runner (no extra dependencies or real Docker services). With the pinned Node active and application ports 3000/5080 free, run from root:

```bash
bash -n scripts/dev.sh
node --test scripts/dev.test.mjs
```

Run backend tests against a **separate, disposable real PostgreSQL**, not the development database. From root:

```bash
docker run -d --rm --name trading-assistant-m01-test \
  -p 127.0.0.1:55432:5432 \
  -e POSTGRES_DB=trading_assistant_test -e POSTGRES_USER=trading_test \
  -e POSTGRES_PASSWORD=dummy_test_only postgres:17.12
timeout 45 sh -c 'until docker exec trading-assistant-m01-test pg_isready -h 127.0.0.1 -U trading_test -d trading_assistant_test; do sleep 1; done'
export TRADING_TEST_CONNECTION='Host=127.0.0.1;Port=55432;Database=trading_assistant_test;Username=trading_test;Password=dummy_test_only'
dotnet build
dotnet format --verify-no-changes --no-restore
dotnet test
docker stop trading-assistant-m01-test
unset TRADING_TEST_CONNECTION
```

Alternatively, supply an explicit isolated loopback PostgreSQL database with a name ending in `_test`. Missing configuration, a wrong target or an unreachable test database fails the relevant test; it is not skipped. Each catalogue test creates and drops only its own uniquely named schema in that database. Tests apply the actual migration and repository SQL fixture, including identity constraints, repeat imports, collision rollback, paging and safe HTTP errors. No development schema or volume is used. `timeout` is GNU coreutils (use `gtimeout` on macOS).

From `frontend/`, with the manually started API and Next.js **stopped**:

```bash
npm run lint
npm run typecheck
npm run test
npm run build
npx playwright install chromium
npm run test:e2e
```

On a fresh Linux machine use `npx playwright install --with-deps chromium` if system libraries are missing. Playwright starts, polls and stops its own Next.js and ASP.NET Core processes on ports 3000 and 5080; the happy path calls the real health route. No PostgreSQL is needed for this **liveness** test or the frontend build. The [PR workflow](.github/workflows/foundation.yml) also provisions PostgreSQL for xUnit.

The additional M04 browser journey requires the same disposable `TRADING_TEST_CONNECTION` as xUnit. With ports 3000/5080 free, from `frontend/`:

```bash
npm run test:e2e:prices
```

This starts real Next.js and ASP.NET Core/Kestrel with a uniquely named PostgreSQL schema. Only the upstream Massive HTTP handler is replaced in the **test assembly**; the production application has no simulated-provider switch. All prices are invented. The test imports, repeats, reloads, encounters an upstream error and retries, using real browser HTTP and database commits. Its host drops its schema on graceful shutdown. If a test host is forcibly killed, discard the owned disposable database after testing. The existing eight preview/liveness smoke tests remain independent of PostgreSQL. Both suites run in the PR workflow without live secrets.

## Configuration and troubleshooting

- **Wrong runtime:** `dotnet --list-sdks` must include the exact pinned SDK. If installed in a private directory, export `DOTNET_ROOT` to that directory and prepend it to `PATH`. Use `nvm use` from the root to select the pinned Node runtime.
- **Port conflicts / CORS:** stop conflicting processes or change `POSTGRES_PORT`, the API connection string, `ASPNETCORE_URLS`, `FrontendOrigin`, or `NEXT_PUBLIC_API_BASE_URL` as appropriate. For example, start the web app with `npm run dev -- --port 3001` and set `FrontendOrigin=http://localhost:3001` for the API. To also use the numeric hostname on that port, set `AdditionalFrontendOrigins__0=http://127.0.0.1:3001` in the API environment. The default additional origin is `http://127.0.0.1:3000`; it remains allowed unless overridden. Every configured origin is validated as a loopback HTTP(S) origin. Origins must match exactly, with no trailing slash; `localhost` and `127.0.0.1` are different origins. Reload environment settings and restart the affected processes after edits. Do not expose these unauthenticated services publicly.
- **Unavailable database:** run `docker compose ps`, verify the listener and check the API shell settings. PostgreSQL initializes credentials only on a new volume; changing `.env` does not update an existing database user. Avoid printing resolved Compose configuration or connection strings. `docker compose stop postgres` and `docker compose up -d --wait` let you exercise an outage without deleting data.
- **Instrument routes return `500` while readiness is `200`:** confirm the intended database and run `dotnet ef migrations list` with the project arguments below. A reachable database without the application migration is ready but cannot serve catalogue queries. Errors deliberately omit SQL and connection details. Apply the reviewed migration explicitly, not from startup.
- **Empty catalogue:** `200 {"items":[],"offset":0,"limit":50}` is normal after migration or beyond the last page. Import the optional fixtures only if desired. An empty backend catalogue does not remove or change the M02 browser previews.
- **Unavailable API:** check `/api/v1/health` directly, then the browser's configured origin and CORS settings. The dashboard checks on mount and on **Check again**, not continuously.

## Persistence and migrations

[Infrastructure](backend/src/TradingAssistant.Infrastructure) owns the EF Core context, mappings, Npgsql reader and [initial migration](backend/src/TradingAssistant.Infrastructure/Migrations/20261126204038_InitialInstruments.cs). Domain validates instrument creation; Application owns the read port and catalogue use cases. Startup, health and readiness never modify the schema or insert data.

### Apply the schema explicitly

From root, start the intended local PostgreSQL and load its matching backend environment. Confirm the database name and loopback data source shown by `dbcontext info` before updating. For verification, use a separate disposable database ending in `_test`, never your development data.

```bash
docker compose up -d --wait --wait-timeout 60 postgres
set -a; . backend/.env; set +a
dotnet tool restore
infra=backend/src/TradingAssistant.Infrastructure
api=backend/src/TradingAssistant.Api
dotnet ef dbcontext info --project "$infra" --startup-project "$api"
dotnet ef migrations list --project "$infra" --startup-project "$api"
migration_review_file=$(mktemp)
dotnet ef migrations script --idempotent --project "$infra" --startup-project "$api" --output "$migration_review_file"
cat "$migration_review_file"     # Review before applying.
dotnet ef database update --project "$infra" --startup-project "$api"
rm "$migration_review_file"
```

Migration `20261126204038_InitialInstruments` creates an **empty** `Instruments` table. Running `database update` again is a no-op. PostgreSQL enforces all six non-null columns, a UUID primary key, maximum lengths (symbol 32, MIC 4, name 200, currency 3), required boolean provenance and unique `(ExchangeMic, Symbol)`. Domain creation additionally rejects an empty UUID, invalid ASCII character patterns, wrong code lengths and blank names; it trims display names. Registry validation and symbol history are deferred. Direct SQL writers must respect these domain rules.

### Optional fictional demo import

The [transactional fixture](infrastructure/seed/sample-instruments.sql) adds these sample listings, not market data:

| UUID | Symbol | MIC | Name | Currency |
| --- | --- | --- | --- | --- |
| `10000000-0000-0000-0000-000000000001` | S-AO | XAAA | Alder Orbit (sample) | USD |
| `10000000-0000-0000-0000-000000000002` | S-BG | XAAA | Bracken Grid (sample) | USD |
| `10000000-0000-0000-0000-000000000003` | S-AO | XBBB | Alder Orbit alternate listing (sample) | EUR |

Every imported row has `isSample: true`. XAAA and XBBB are illustrative MIC values; S-AO deliberately repeats across exchanges. These backend fixtures do not replace the independent frontend sample data.

For the **intended local Compose service**, confirm its database matches the migration target, then import from root:

```bash
docker compose exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -c "SELECT current_database();"'
docker compose exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' \
  < infrastructure/seed/sample-instruments.sql
```

For a separately configured disposable instance, run `psql -v ON_ERROR_STOP=1 -f infrastructure/seed/sample-instruments.sql` with explicit local `PGHOST`, `PGPORT`, `PGDATABASE` and `PGUSER` and an ignored password file or local authentication. Do not put real credentials on the command line or in SQL.

The import can be run twice: an existing MIC/symbol pair retains **all** its fields, UUID and provenance. A fixture UUID attached to another identity fails and rolls back the entire transaction, even when the matching logical row already exists. The short import locks out concurrent writers to make that check reliable. Resolve a conflict deliberately; do not delete existing rows blindly.

### Query and failure examples

Start the API with the same database configuration. Catalogue routes remain GET-only:

```bash
base=http://localhost:5080
sample_id=10000000-0000-0000-0000-000000000001
curl -sS -i --max-time 15 "$base/api/v1/instruments"
curl -sS -i --max-time 15 "$base/api/v1/instruments?offset=1&limit=1"
curl -sS -i --max-time 15 "$base/api/v1/instruments?offset=3"
curl -sS -i --max-time 15 "$base/api/v1/instruments/$sample_id"
curl -sS -i --max-time 15 "$base/api/v1/instruments/20000000-0000-0000-0000-000000000001"
curl -sS -i --max-time 15 "$base/api/v1/instruments/not-a-uuid"
curl -sS -i --max-time 15 "$base/api/v1/instruments?limit=101"
```

A demo detail response is:

```json
{"id":"10000000-0000-0000-0000-000000000001","symbol":"S-AO","exchangeMic":"XAAA","name":"Alder Orbit (sample)","currency":"USD","isSample":true}
```

The list wraps records in `{ "items": [...], "offset": 0, "limit": 50 }` without a total count. PostgreSQL orders by MIC then symbol **before** offset/limit. Omitted values default to 0/50; offset accepts 0 through 2147483647 and limit accepts 1 through 100. Empty, repeated, overflowing or invalid pagination values return `400` Problem Details. Malformed, non-hyphenated and zero UUIDs return `400`; a valid missing UUID returns `404`. Unexpected database failures return generic `500` Problem Details. Development OpenAPI documents both routes and their payloads.

HTTP cancellation flows through Application into EF Core. Connection timeout is capped at five seconds (zero/unlimited becomes five); commands are bounded to five seconds. Readiness retains its three-second deadline.

For an outage check, stop only your owned local PostgreSQL with `docker compose stop postgres`: health remains `200`, readiness becomes `503`, and catalogue reads return safe `500`. Run `docker compose up -d --wait --wait-timeout 60 postgres` to recover without restarting the API. Restart the API separately and query the same UUID to confirm durability. Do not remove the development volume.

See the [architecture](docs/architecture.md), [engineering standards](docs/engineering-standards.md) and [milestone index](docs/milestones.md) for specifications and verification reports.

## Daily price history (Milestone 4)

### Explicit schema and real-listing setup

Migration `20261128153241_DailyPrices` adds `DailyPrices` and `DailyPriceRefreshes`; it does not alter or seed the catalogue. Follow [Apply the schema explicitly](#apply-the-schema-explicitly) to review SQL, identify the intended local database, apply both migrations, then run the update again to confirm no pending changes. Neither startup nor the launcher migrates or imports.

After migrating, positively identify the intended Compose database and opt in to [real listings](infrastructure/seed/real-instruments.sql):

```bash
docker compose exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -c "SELECT current_database();"'
docker compose exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' \
  < infrastructure/seed/real-instruments.sql
```

For a separately configured disposable instance use `psql -v ON_ERROR_STOP=1 -f infrastructure/seed/real-instruments.sql` with explicit loopback `PGHOST`, `PGPORT`, `PGDATABASE`, `PGUSER` and an ignored password file. Run the script twice to verify repeatability.

| Reserved UUID | Listing | Currency |
| --- | --- | --- |
| `20000000-0000-0000-0000-000000000001` | AAPL / XNAS (Apple) | USD |
| `20000000-0000-0000-0000-000000000002` | MSFT / XNAS (Microsoft) | USD |

The script creates non-sample listings only, with no prices. A compatible existing MIC/symbol retains its UUID and other fields. A reserved UUID attached to another identity, sample flag or incompatible currency aborts the entire transaction. Inspect and resolve the conflict deliberately; never reclassify a sample or delete existing data to force setup. The fictional M03 seed and browser previews are unchanged. Import mapping is explicit: only non-sample XNAS/AAPL/USD and XNAS/MSFT/USD.

### Provider configuration and browser workflow

The owner approved **US-listed stocks and Massive Stocks Basic** for personal local use. A key is optional for startup, tests and stored reads. To make live imports, add `Massive__ApiKey` to the ignored **backend/.env** with your authorized personal key; never put it in frontend settings, `NEXT_PUBLIC_*`, SQL or command-line arguments. The example leaves it empty. Restart the API after configuration changes. Check plan coverage, current storage rights and both listings against Massive reference information during live setup.

1. Start `./scripts/dev.sh` and open Dashboard -> **Price history**.
2. Choose a listing (symbol, MIC, currency and sample status are shown). Use **Next listings** for another catalogue page. Empty/unsupported catalogues show setup guidance.
3. Select inclusive calendar dates. The default is the last 30 calendar days ending yesterday in **America/New_York**, not the browser's timezone. Imports allow at most 366 days and must start within two calendar years. Today's unfinished session is not importable. GET may read older stored data while licensed.
4. Click **Import daily prices** explicitly. One request imports one stock/range. An unchanged repeat returns zero inserted/updated and counts unchanged rows. A correction updates only its stock/date; dates absent from the response are retained.
5. Inspect the newest-first OHLCV table, USD, **Massive · Unadjusted daily prices**, latest session in this view, and **Last successful refresh for this stock** with UTC time/range. Stock and date choices are in the URL for reload. No key is needed to reopen saved results.

No daily bars are manufactured for holidays or other empty intervals. A successful empty response advances the stock's last successful refresh but leaves saved bars intact. A refresh describes only the last checked request range, not freshness of every stored date. These are provider daily aggregates, not official closing auction prices. Raw/unadjusted prices can jump after splits; adjusted history and charts are out of scope.

### Contracts, limits and recovery

Both routes use canonical nonempty hyphenated UUIDs. Imports require `application/json`:

```json
{"from":"2026-11-23","to":"2026-11-24"}
```

Use a recent completed range within the current entitlement window, rather than assuming the example remains valid.

- POST returns `instrumentId`, `source`, `from`, `to`, `inserted`, `updated`, `unchanged`, `lastRefreshedAtUtc`.
- GET returns `instrumentId`, `currency`, `canImport`, `items`, `source`, `priceBasis`, `lastRefreshedAtUtc`, `lastRefreshFrom`, `lastRefreshTo`. Each item has `tradingDate`, `open`, `high`, `low`, `close`, `volume`. OHLCV are invariant decimal **strings**, not JavaScript numbers; storage is exact `numeric(28,10)`, including fractional volume. Source/basis and refresh fields are null before first success. An empty range is a successful 200.
- GET uses a consistent database snapshot and `Cache-Control: no-store`, without provider traffic. A successful import triggers a fresh read. Failed imports retain saved rows and last successful metadata.
- One process-wide rolling budget allows **five outbound attempts per minute**, including failures. The fixed HTTPS Massive request uses bearer authentication, raw daily bars, ascending dates and limit 50000. No redirects, continuation following, retries or demo fallback. Timeout: ten seconds; response buffer: 1 MiB. Malformed, duplicate, out-of-range, rounded or incomplete results never partially persist.
- Writers lock the selected instrument row before comparing rows, then commit bars and monotonic UTC refresh together. A disconnect after commit may have succeeded: retrying is safe.
- JSON POST CORS is scoped to daily-price routes on the existing explicit loopback UI origins. Foreign supplied browser origins are rejected. Local clients without an Origin are accepted. Other catalogue/health CORS remains GET/Accept only.

| HTTP | Recovery |
| --- | --- |
| 400 | Correct UUID, required exact dates, repeated query values or range bounds. |
| 404 / 422 | Choose an existing supported non-sample listing; run the opt-in setup if needed. |
| 403 / 415 | Use the configured localhost UI origin and JSON media type. |
| 429 | Wait for the displayed `Retry-After` delay; the limit is shared across stocks and browser tabs. |
| 503 | Configure the backend key or resolve Massive entitlement; stored reads still work. |
| 502 / 504 | Provider failure/incomplete response or timeout. Retry manually later. |
| 500 | Check local PostgreSQL and explicitly applied migrations; errors intentionally omit SQL and credentials. |

Keep live prices private under the account's market-data terms, including deletion requirements when entitlement ends. Do not commit live responses or prices as test fixtures. The [M04 checklist](docs/milestones/04-daily-price-history-verification.md) records synthetic verification separately from the still-required authorized live walkthrough and hosted/owner review.
