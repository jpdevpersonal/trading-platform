# Milestone 2 verification checklist

Use with the [Application Shell specification](02-application-shell.md). As of 2026-11-24, **21 criteria passed locally**. Hosted verification and owner review are pending.

## 1. Record the starting state

- [x] Record owner scope approval, local branch, starting commit, worktree status, and any baseline differences relevant to M02.
- [x] Confirm the repository is the full `jpdevpersonal/trading-platform` checkout. From its root record `git status --short --branch`, `git rev-parse HEAD` and the exit code of `git merge-base --is-ancestor 4cada776562271d570bbf465a4c0ae52dfd8d6fe HEAD`. Exit code `0` establishes ancestry. Resolve an absent or divergent baseline before editing.
- [x] Record `dotnet --version`, `node --version`, `npm --version`, `docker version` and `docker compose version`. Use the repository pins and the Bash or WSL workflow documented in the [README](../../README.md).

The owner approved the frontend-only scope on 2026-11-24. `git status --short --branch` showed branch `milestone-02-application-shell`, no tracked changes, and three untracked M02 documents: the [prompt](../../.github/prompts/milestone-02-application-shell.prompt.md), [spec](02-application-shell.md) and this checklist. `git rev-parse HEAD` returned `4cada776562271d570bbf465a4c0ae52dfd8d6fe`. The ancestry check and origin identity check both returned `0`; there were no intervening commits. The pre-existing prompt was left unchanged.

Verification ran under Linux Bash. `dotnet --version` initially found only system SDK 10.0.115 and could not resolve the pin. With `DOTNET_ROOT="$HOME/.dotnet"` and that directory prepended to `PATH`, it returned 10.0.404. With `$HOME/.nvm/versions/node/v24.22.3/bin` on `PATH`, `node --version` returned v24.22.3 and `npm --version` returned 11.19.1. `docker version` reported client/server 29.2.1; `docker compose version` reported v2.40.3-desktop.1. The resolved version commands exited `0`.

## 2. Run the automated gates

Use an isolated verification environment. Set up disposable real PostgreSQL and `TRADING_TEST_CONNECTION` as documented in the README. Start and stop only services you own for this verification. Keep the integration target separate from the development database and volume.

From repository root, with the isolated PostgreSQL test instance ready and ports 3000 and 5080 free for the launcher regression checks:

```bash
dotnet restore --locked-mode
dotnet build --no-restore
dotnet format --verify-no-changes --no-restore
dotnet test --no-build
bash -n scripts/dev.sh
node --test scripts/dev.test.mjs
```

Stop the disposable PostgreSQL instance and clear its test environment value after xUnit. Run from `frontend/`, with the actual local application servers and PostgreSQL stopped in the verification environment. Playwright owns its frontend and API servers on ports 3000 and 5080. Follow the README for installing Chromium system dependencies if needed.

```bash
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
npx playwright install chromium
npm run test:e2e
```

- [x] Record command exit status and relevant counts or test names. Resolve in-scope failures and rerun affected checks.
- [x] Verify browser coverage of direct URLs, unknown-path 404, Back/Forward, active links, keyboard entry, viewport fit, Dashboard links, and content and provenance on all four previews. Retain both live API connection tests.
- [x] Verify component coverage of the connection states and representative preview semantics or labelling. Record where each fixture page's content and labelling are asserted.

| Working directory | Actual command | Exit | Evidence |
| --- | --- | --- | --- |
| Repository root | `dotnet restore --locked-mode` | 0 | All six projects restored with existing locks. |
| Repository root | `dotnet build --no-restore` | 0 | No warnings or errors. |
| Repository root | `dotnet format --verify-no-changes --no-restore` | 0 | No formatting changes required. |
| Repository root | `dotnet test --no-build` | 0 | 9 unit and 8 integration tests passed; none skipped. |
| Repository root | `bash -n scripts/dev.sh` | 0 | No Bash syntax errors. |
| Repository root | `node --test scripts/dev.test.mjs` | 0 | 6 launcher regressions passed. |
| `frontend/` | `npm ci` | 0 | Existing lockfile installed; npm reported an ESLint deprecation warning and zero vulnerabilities. |
| `frontend/` | `npm run lint` | 0 | No lint errors or warnings. |
| `frontend/` | `npm run typecheck` | 0 | Route type generation and strict TypeScript passed. |
| `frontend/` | `npm run test` | 0 | 6 Vitest tests passed across 2 files. |
| `frontend/` | `npx playwright install chromium` | 0 | Pinned runner's Chromium available. |
| `frontend/` | `npm run build` | 0 | Prerendered Dashboard and all four previews with no API or PostgreSQL listener. |
| `frontend/` | `npm run test:e2e` | 0 | 8 real-server Chromium tests passed. |

The disposable database was started only for xUnit, after the backend build and formatting checks. The test target was scoped to the `dotnet test` process:

```bash
docker run -d --rm --name trading-assistant-m02-test -p 127.0.0.1:55432:5432 -e POSTGRES_DB=trading_assistant_test -e POSTGRES_USER=trading_test -e POSTGRES_PASSWORD=dummy_test_only postgres:17.12
timeout 45 sh -c 'until docker exec trading-assistant-m02-test pg_isready -h 127.0.0.1 -U trading_test -d trading_assistant_test; do sleep 1; done'
TRADING_TEST_CONNECTION='Host=127.0.0.1;Port=55432;Database=trading_assistant_test;Username=trading_test;Password=dummy_test_only' dotnet test --no-build
docker stop trading-assistant-m02-test
```

Database start, readiness and stop exited `0`. Before the frontend build and after manual teardown, `ss -ltn '( sport = :3000 or sport = :5080 or sport = :5432 or sport = :55432 )'` and `docker ps --format '{{.Names}} {{.Ports}}'` exited `0` and showed no listeners on those ports or running containers.

## 3. Run the manual and diff checks

- [x] Opened Dashboard and all four previews at 360 and 1280 CSS pixels in the integrated browser. Checked heading/list/table reading order and no horizontal page scroll. At both widths, Tab reached the visible skip link and Dashboard nav link, Enter on the skip link focused `main`, and subsequent Tabs reached the four Dashboard preview links then **Check again**, with a solid focus outline throughout.
- [x] Followed all four Dashboard links. Inspected sample labels and content, then compared the rendered text before and after reload for all four previews. The screener showed names, symbols and sectors without scores; the portfolio showed illustrative quantities.
- [x] Started separate frontend and API processes. Used **Check again** after stopping the API and saw **API unavailable**. The four preview URLs still returned HTTP 200 and rendered sample data. Restarted the API without restarting the frontend, then used **Check again** and saw **Connected to API** with an HTTP 200 health response.
- [x] Inspected the browser resource trace on all five direct routes. Dashboard's only cross-origin request URL was `http://localhost:5080/api/v1/health`; the previews made none. Reviewed the diff and untracked preview files: navigation only on previews, local source fixtures, no provider/broker calls, credentials, data writes, order surface or generated output in the changes.
- [ ] After the owner opens a PR, record the hosted `verify` result, run link and tested revision. Record the owner review separately.

Manual startup used explicit loopback configuration and did not need a database to check liveness:

```bash
# Repository root, API process (run twice)
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 FrontendOrigin=http://localhost:3000 dotnet run --project backend/src/TradingAssistant.Api --no-build --no-launch-profile
# frontend/, separate frontend process
NEXT_PUBLIC_API_BASE_URL=http://localhost:5080 npm run dev -- --port 3000
```

Each process was confirmed ready and stopped through its own terminal session. `curl --fail --silent --show-error --max-time 10 http://localhost:5080/api/v1/health` exited `0` before and after restart. During the outage the same probe with `--max-time 5` exited `7`; `curl --fail --silent --show-error --max-time 10 --output /dev/null --write-out 'Frontend HTTP %{http_code}\n' http://127.0.0.1:3000/` still exited `0` with `200`.

## 4. Record evidence by acceptance ID

Use **Pass**, **Fail**, **Blocked** or **Pending**. For a blocked check, record the missing dependency and what will establish a pass. For hosted verification use Pending until a PR run is available. A command that was planned or skipped has no passing evidence.

| ID | Primary evidence to record | Result | Actual evidence or blocker |
| --- | --- | --- | --- |
| M02-REP-001 | Git starting state and ancestry | Pass | Exact M01 merge at HEAD; ancestry `0`; starting state recorded above. |
| M02-REP-002 | Scope and changed-path review | Pass | Frontend, tests and docs only; backend, pins, locks, DB, startup and CI unchanged. |
| M02-UX-001 | Chromium direct routes, 404 and history | Pass | Five HTTP 200 direct routes with expected headings; unknown route HTTP 404; Back/Forward across all four Dashboard links. |
| M02-UX-002 | Chromium active link and unique nav | Pass | One Workspace nav and one matching current link on direct loads and navigation. |
| M02-UX-003 | Landmarks, skip focus and keyboard pass | Pass | Chromium verified unique landmarks and page heading; browser Tab/Enter check confirmed skip focus and retry reachability. |
| M02-UX-004 | Chromium and manual 360/1280 layout results | Pass | All five routes at both widths; no horizontal overflow, wrapped nav above content at 360px, rail at 1280px. |
| M02-UX-005 | Heading, list/table, reading order and focus review | Pass | Browser heading/list/table inspection and keyboard outline checks; component tests asserted caption-based names and scoped column headings. |
| M02-DASH-001 | Four quick links and preview copy | Pass | Chromium activated all four links within the Workspace previews region and verified purpose text and destination headings. |
| M02-DASH-002 | Connection tests and manual outage/recovery | Pass | Loading, validation, failure and retry in Vitest; real API and HMR from both local origins in Chromium; API-only stop and restart verified. |
| M02-PRE-001 | Screener content assertions | Pass | Three fixture instruments with names, symbols and sectors in the named table. |
| M02-PRE-002 | Watchlists content assertions | Pass | Named list with all three of the same instruments. |
| M02-PRE-003 | Portfolio content assertions | Pass | Three named instruments with illustrative quantities 12, 8 and 20. |
| M02-PRE-004 | Research content assertions | Pass | Two notes with instrument, title and excerpt. |
| M02-PRE-005 | Sample labels and read-only surface review | Pass | All four show the visible label and fictional explanation; no feature controls in rendered pages or source. |
| M02-DATA-001 | Typed fixture review, refresh and API outage evidence | Pass | Typed shared instrument fixture; rendered text unchanged on reload; all four routes render during outage. |
| M02-DATA-002 | Network, configuration and scope review | Pass | Only Dashboard process health requested outside the frontend origin; no provider, broker, persistence or credential changes. |
| M02-TST-001 | npm install, lint, types and independent build | Pass | All four commands exited `0`; API and PostgreSQL stopped for build. |
| M02-TST-002 | Vitest and real-server Chromium results | Pass | 6 Vitest and 8 Chromium tests; existing connection and narrow-shell coverage retained. |
| M02-TST-003 | .NET gates, real PostgreSQL and launcher checks | Pass | Locked restore, build, format, 17 xUnit tests with disposable PostgreSQL, Bash syntax and 6 launcher regressions. |
| M02-TST-004 | Hosted `verify` link and revision | Pending | No implementation PR or hosted run yet. |
| M02-OPS-001 | README and milestone index review | Pass | Runbook documents the routes, fictional provenance and liveness; index links approved M02 and records status. |
| M02-OPS-002 | Command log, final diff and review handoff | Pass | Commands, evidence and pending owner checks recorded; work remains local and unstaged. |

## 5. Completion record

| Item | Actual result |
| --- | --- |
| Owner scope approval and date | Frontend-only Application Shell approved in chat on 2026-11-24. |
| Starting branch, commit and worktree state | See section 1; no changes since the pinned M01 merge except the three pre-existing untracked documents. |
| Changed paths and summary | [Layout](../../frontend/src/app/layout.tsx), [navigation](../../frontend/src/components/navigation.tsx) and [styles](../../frontend/src/app/globals.css): responsive shell. [Dashboard](../../frontend/src/app/page.tsx), [section metadata](../../frontend/src/lib/sections.ts), [preview route](../../frontend/src/app/%5Bsection%5D/page.tsx) and [preview feature](../../frontend/src/features/previews/): links, read-only fixtures and component coverage. [Chromium tests](../../frontend/tests/browser/workspace.spec.ts): route, shell and content coverage. [README](../../README.md), [milestone index](../milestones.md), [spec](02-application-shell.md) and this checklist: scope and evidence. |
| Exact commands and flags, working directories, versions and exit status | Sections 1–3. |
| Test names or counts and manual observations | Sections 2–4. |
| Final diff, owner review and hosted run link | `git --no-pager diff --check` exited `0`; `git --no-pager diff --name-status` and `git ls-files --others --exclude-standard` confirmed the tracked and untracked scope. Owner review and hosted run link pending. |
| Unresolved checks and their next verification steps | Owner stages and commits the work, opens the PR, records the hosted `verify` run link and tested revision, and reviews before closure. No push, merge or deployment performed. |
