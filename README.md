# AI Trading Assistant

A local workspace for human-led stock research. Milestone 1 delivers a browser dashboard, four navigation placeholders, a versioned API and PostgreSQL. It does not fetch market data, connect to brokers or submit orders.

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

Open **http://localhost:3000** (or **http://127.0.0.1:3000**). Both local frontend origins are explicitly allowed, including development hot reload. Dashboard should say **Connected to API**. Screener, Watchlists, Portfolio and Research are intentionally empty destinations. Tab to skip navigation or activate a link with Enter. Stop the API and select **Check again** to see **API unavailable**; restart it and retry to recover.

| Local endpoint | What it verifies |
| --- | --- |
| `GET http://localhost:5080/api/v1/health` | Process only: `200 {"status":"healthy"}`, even with PostgreSQL stopped |
| `GET http://localhost:5080/api/v1/ready` | A bounded SQL round trip: `200 {"status":"ready"}` or `503 {"status":"unavailable"}` |
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

Alternatively, supply an explicit isolated loopback PostgreSQL database with a name ending in `_test`. Missing configuration, a wrong target or an unreachable test database fails the relevant test; it is not skipped. `timeout` is GNU coreutils (use `gtimeout` on macOS).

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

## Configuration and troubleshooting

- **Wrong runtime:** `dotnet --list-sdks` must include the exact pinned SDK. If installed in a private directory, export `DOTNET_ROOT` to that directory and prepend it to `PATH`. Use `nvm use` from the root to select the pinned Node runtime.
- **Port conflicts / CORS:** stop conflicting processes or change `POSTGRES_PORT`, the API connection string, `ASPNETCORE_URLS`, `FrontendOrigin`, or `NEXT_PUBLIC_API_BASE_URL` as appropriate. For example, start the web app with `npm run dev -- --port 3001` and set `FrontendOrigin=http://localhost:3001` for the API. To also use the numeric hostname on that port, set `AdditionalFrontendOrigins__0=http://127.0.0.1:3001` in the API environment. The default additional origin is `http://127.0.0.1:3000`; it remains allowed unless overridden. Every configured origin is validated as a loopback HTTP(S) origin. Origins must match exactly, with no trailing slash; `localhost` and `127.0.0.1` are different origins. Reload environment settings and restart the affected processes after edits. Do not expose these unauthenticated services publicly.
- **Unavailable database:** run `docker compose ps`, verify the listener and check the API shell settings. PostgreSQL initializes credentials only on a new volume; changing `.env` does not update an existing database user. Avoid printing resolved Compose configuration or connection strings. `docker compose stop postgres` and `docker compose up -d --wait` let you exercise an outage without deleting data.
- **Unavailable API:** check `/api/v1/health` directly, then the browser's configured origin and CORS settings. The dashboard checks on mount and on **Check again**, not continuously.

## Persistence and migrations

[Infrastructure](backend/src/TradingAssistant.Infrastructure) owns the EF Core context and Npgsql provider. M01 has no business tables, seed rows or migrations. Startup never modifies the schema.

**When a later milestone authorizes a schema change**, load the intended API environment, restore the pinned CLI and confirm the target before generating, reviewing and explicitly applying the migration:

```bash
set -a; . backend/.env; set +a
dotnet tool restore
infra=backend/src/TradingAssistant.Infrastructure
api=backend/src/TradingAssistant.Api
dotnet ef dbcontext info --project "$infra" --startup-project "$api"
# Future schema work only:
dotnet ef migrations add FirstRealSchema --project "$infra" --startup-project "$api" --output-dir Migrations
dotnet ef migrations script --project "$infra" --startup-project "$api"
dotnet ef database update --project "$infra" --startup-project "$api"
```

See the [architecture](docs/architecture.md), [engineering standards](docs/engineering-standards.md), [milestone spec](docs/milestones/01-foundation.md) and [verification report](docs/milestones/01-foundation-verification.md).
