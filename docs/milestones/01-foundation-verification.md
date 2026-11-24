# M01 local verification

2026-11-23 · local branch `milestone/01-foundation` · **30 Pass, 0 Fail, 1 Blocked**. Nothing staged, committed, pushed or deployed. The hosted PR run and owner review are outstanding; the milestone is not closed.

## Changes

- Root: `TradingAssistant.slnx`, `Directory.Build.props`, `global.json`, `.config/dotnet-tools.json`, `.nvmrc`, `.gitignore`, `.editorconfig`, `compose.yaml`, `.env.example`, and the [runbook](../../README.md).
- [Backend source](../../backend/src/): four projects with NuGet locks, an Application readiness port, Infrastructure EF context/probe, and API composition, origin validation and HTTP contracts. [Backend tests](../../backend/tests/): xUnit unit and HTTP/PostgreSQL integration projects and their locks. Separate API environment example; no migration or seed data.
- [Frontend](../../frontend/): pinned manifest/lock, public environment example, Next/TypeScript/Tailwind/ESLint configuration, App Router shell, navigation, typed transport, connection component, Vitest and Playwright suites.
- [Infrastructure](../../infrastructure/docker-compose.yml): canonical PostgreSQL service. [Workflow](../../.github/workflows/foundation.yml): read-only PR verification. Milestone index, spec status and this report record the local result.

## Acceptance record

| ID | Result | Evidence |
| --- | --- | --- |
| M01-REP-001 | Pass | Inspected intended paths: `frontend/src`, `backend/src`, `backend/tests`, `infrastructure` and `docs` exist. |
| M01-REP-002 | Pass | `dotnet sln list` returned the four production and two test projects; root build compiled all six. |
| M01-REP-003 | Pass | `dotnet reference list` for each production project: Domain → none, Application → Domain, Infrastructure → Application, API → Application + Infrastructure. |
| M01-REP-004 | Pass | Clean `npm ci` used the lock; strict TypeScript, Tailwind 4 PostCSS and Next production build passed. |
| M01-REP-005 | Pass | `git check-ignore` covered root/API/web local settings, generated .NET/Next/npm output, local data and editor settings. All three dummy examples remain eligible for Git. |
| M01-API-001 | Pass | Live curl returned HTTP 200 JSON at `/api/v1/health`; integration test checked status, content type and body. |
| M01-API-002 | Pass | Exact body `{"status":"healthy"}`; unchanged while PostgreSQL was stopped. |
| M01-API-003 | Pass | Live Development `/openapi/v1.json` was OpenAPI 3.1.1 with `/api/v1/health` and `/api/v1/ready`; HTTP test asserted the health operation. |
| M01-API-004 | Pass | Observed JSON log state with `Method=GET`, `Route=/api/v1/ready`, `StatusCode=503`, and numeric `ElapsedMs`. Test-only DI fault returned 500 `application/problem+json`, no internal exception type or sentinel; no public fault route. |
| M01-API-005 | Pass | HTTP tests verified exact allowed origin for GET and OPTIONS, GET/Accept preflight permissions, and no allow-origin for a different local port. Live override on API port 5085 returned `Access-Control-Allow-Origin: http://localhost:3001`. |
| M01-WEB-001 | Pass | Browser opened `http://localhost:3000`; live development server returned HTTP 200. |
| M01-WEB-002 | Pass | Desktop banner, navigation and main inspected; Chromium verified the keyboard skip link and focused main. At 360px the document was 360px wide; navigation wrapped without horizontal scrolling. |
| M01-WEB-003 | Pass | Keyboard-activated Dashboard, Screener, Watchlists, Portfolio and Research; each displayed its own heading and current-page link. |
| M01-WEB-004 | Pass | Playwright observed the real cross-origin health response and the dashboard's “Connected to API” status. No interception for the happy path. |
| M01-WEB-005 | Pass | Observed loading, then connected. Stopped API, selected **Check again**, saw “API unavailable”; restarted API and retried successfully. Component tests also reject network, HTTP and contract failures. |
| M01-DB-001 | Pass | Root Compose config resolved one `postgres:17.12` service, loopback port and named volume; `up -d --wait` and `ps` reported healthy. |
| M01-DB-002 | Pass | Compose requires credentials from the local environment; API shell settings are separate. All real configuration paths ignored; examples contain only dummy values. |
| M01-DB-003 | Pass | `dotnet ef dbcontext info` resolved `TradingContext` and `Npgsql.EntityFrameworkCore.PostgreSQL`. README specifies Infrastructure migration location, startup project, script review and explicit apply. No schema change executed. |
| M01-DB-004 | Pass | Real isolated PostgreSQL test returned 200 ready. With development PostgreSQL actually stopped, `/ready` returned 503 unavailable and `/health` stayed 200. Restart restored 200 ready. |
| M01-DB-005 | Pass | Inserted `survived-restart` into a disposable schema; after `docker compose restart postgres` and health polling, read the same marker and dropped table and schema. |
| M01-TST-001 | Pass | Root `dotnet build`: 0 warnings, 0 errors. Formatting verification also passed. |
| M01-TST-002 | Pass | `dotnet test`: 9 unit + 5 integration passed, 0 skipped. PostgreSQL test used port 55432, database `trading_assistant_test`. Removing its test configuration deliberately failed that test with the setup instruction. |
| M01-TST-003 | Pass | Lint, typecheck and production build passed in the clean candidate checkout, with no API running during build. |
| M01-TST-004 | Pass | Vitest/RTL: 4 passed. Deferred fetch exercised loading, validated response and configured URL; retry recovered from three failure modes. |
| M01-TST-005 | Pass | `npm run test:e2e`: 2 Chromium tests passed with real servers. Repeated with `CI=true` in the clean candidate; both listeners stopped afterward. |
| M01-TST-006 | **Blocked** | PR workflow exists and passed actionlint; its build/test commands passed locally. No authorized push or PR, hence no successful **hosted** run to record. Owner must run it on a PR. |
| M01-OPS-001 | Pass | Followed README setup, startup, verification and shutdown in a fresh local clone with the intended uncommitted files overlaid (no ignored config or generated output copied). |
| M01-OPS-002 | Pass | Clean candidate used only example settings, locked restores and a new Compose volume. Real `/ready` and browser connection succeeded. Runtime downloads verified by checksums. |
| M01-OPS-003 | Pass | Started/stopped API, Next and Compose with environment config only; default ports and alternate API port/CORS origin verified. Normal `compose down` retains the development volume. |
| M01-OPS-004 | Pass | Reviewed dependencies, route map, environment examples and intended file set. Only health, readiness and development OpenAPI; no provider keys, broker/AI integrations, order routes or business tables. |
| M01-OPS-005 | Pass | Reviewed tracked + non-ignored candidate paths and empty index diff. No local configuration/build artifacts in candidate; offline redacted Gitleaks scan found no leaks. |

## Reproduction and command evidence

Exact application versions: .NET SDK 10.0.404 / runtime 10.0.15; EF Core and CLI 10.0.15; Npgsql EF provider 10.0.4; Node 24.22.3 / npm 11.19.1; Next 16.4.6, React/DOM 19.3.1, TypeScript 6.0.3, Tailwind 4.3.5; Vitest 5.1.1, Playwright 1.65.1 (Chromium revision 1255); PostgreSQL 17.12. Docker Engine 29.2.1; Compose v2.40.3-desktop.1.

The [README](../../README.md) gives the full runnable setup, including the test-only `docker run`, environment setup and teardown. Commands exercised:

```bash
# Root: workspace, toolchain, build, tests and EF
git status --short --branch
git switch -c milestone/01-foundation
dotnet --list-sdks
node --version
npm --version
docker version
docker compose version
dotnet sln list
for p in backend/src/*/*.csproj; do dotnet reference list --project "$p"; done
dotnet restore --locked-mode
dotnet build
dotnet format --verify-no-changes --no-restore
dotnet test
dotnet tool restore
dotnet ef dbcontext info --project backend/src/TradingAssistant.Infrastructure --startup-project backend/src/TradingAssistant.Api --no-build

# Root: live stack, using the README's separate shell environment
docker compose config --quiet
docker compose up -d --wait --wait-timeout 60
docker compose ps
dotnet run --project backend/src/TradingAssistant.Api
curl -fsS -w ' HTTP %{http_code}\n' http://localhost:5080/api/v1/health
curl -fsS -w ' HTTP %{http_code}\n' http://localhost:5080/api/v1/ready
curl -fsS http://localhost:5080/openapi/v1.json
docker compose stop postgres
curl -sS -w ' HTTP %{http_code}\n' http://localhost:5080/api/v1/ready
docker compose up -d --wait --wait-timeout 60
docker compose down

# frontend/
npm ci
npm run dev
npm run lint
npm run typecheck
npm run test
npm run build
npx playwright install chromium
npm run test:e2e
CI=true npm run test:e2e
```

Persistence probe (with the new dummy-only development volume):

```bash
docker compose exec -T postgres psql -U trading_local -d trading_assistant -v ON_ERROR_STOP=1 -c "CREATE SCHEMA m01_volume_probe; CREATE TABLE m01_volume_probe.probe (marker text); INSERT INTO m01_volume_probe.probe VALUES ('survived-restart');"
docker compose restart postgres
docker compose up -d --wait --wait-timeout 60
docker compose exec -T postgres psql -At -U trading_local -d trading_assistant -c 'SELECT marker FROM m01_volume_probe.probe'
docker compose exec -T postgres psql -U trading_local -d trading_assistant -v ON_ERROR_STOP=1 -c 'DROP TABLE m01_volume_probe.probe; DROP SCHEMA m01_volume_probe;'
```

Final review used `git ls-files --cached --others --exclude-standard`, `git check-ignore`, `git diff --check`, actionlint 1.7.12 on the workflow and Gitleaks 8.30.1 (`stdin --redact --no-banner`) on candidate file contents. No hosted credentials were required. ESLint 9 emits an install-time deprecation notice; it is within the React plugin's supported peer range. Lint itself is clean.
