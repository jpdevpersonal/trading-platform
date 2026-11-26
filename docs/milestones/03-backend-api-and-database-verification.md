# Milestone 3 verification checklist

Use with the [Backend API and database specification](03-backend-api-and-database.md).

**Local result (2026-11-26): Pass.** All 21 local criteria are verified. **M03-TST-005 and owner review remain Pending**; the milestone is not closed. No changes were staged, committed, pushed, deployed or submitted as a PR.

## 1. Baseline and tools

- [x] Branch: `milestone/03-backend-api-database`. Origin: `git@github.com:jpdevpersonal/trading-platform.git`. Starting and ending HEAD: `b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47`. Initially only the M03 prompt, specification and verification checklist were untracked; they were preserved.
- [x] The pinned merge is HEAD; ancestry check exited 0. There are no intervening commits.
- [x] .NET SDK `10.0.404`, dotnet-ef `10.0.15`, Node `v24.22.3`, npm `11.19.1`, Docker client/server `29.2.1`, Compose `v2.40.3-desktop.1`, PostgreSQL image `17.12`. No runtime, dependency or lockfile pin changed.

The initial shell selected system .NET `10.0.115` (SDK resolution failed) and Node `v18.19.1`/npm `11.4.2`. The already installed pinned runtimes were selected before builds and tests:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$HOME/.nvm/versions/node/v24.22.3/bin:$PATH"
```

```bash
git status --short --branch
git rev-parse HEAD
git merge-base --is-ancestor b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47 HEAD
```

## 2. Automated gates

From the repository root in Bash or WSL, use the [README](../../README.md) to start disposable real PostgreSQL with an isolated loopback port and a database name ending in `_test`. Supply its connection via `TRADING_TEST_CONNECTION` to the test process. Use a fresh database or isolated test state for migration cases. Do not use the development database or volume. Keep ports 3000 and 5080 free for launcher and browser regressions.

```bash
dotnet restore --locked-mode
dotnet tool restore
dotnet build --no-restore
dotnet format --verify-no-changes --no-restore
dotnet test --no-build
bash -n scripts/dev.sh
node --test scripts/dev.test.mjs
```

Record which tests exercise explicit migration, identity constraints, fixture reruns and transactional rollback, paging, detail, validation, safe errors and health/readiness. Do not disable existing integration checks. Stop the disposable test instance when finished. From `frontend/` with the application services stopped, let Playwright manage its own servers:

```bash
npm ci
npm run lint
npm run typecheck
npm run test
npm run build
npx playwright install chromium
npm run test:e2e
```

## 3. Migration and manual API checks

Use an explicitly identified fresh, disposable local database and set the API's `ConnectionStrings__TradingAssistant` for that target. Keep real connection values out of evidence. Use the current README to start the API on loopback and a separate terminal for the database commands. The README changes that ship with the implementation should supply an exact fixture-import command for the local Compose service.

1. With PostgreSQL running and before applying the migration, start the API. Check the application table is absent in PostgreSQL. Confirm health and readiness are both `200`; check the table is still absent. Exercise the missing-schema error through the instrument route and check for generic Problem Details.
2. Use the pinned tools to generate and inspect the migration script. Apply the migration explicitly, rerun the update to verify a no-op and confirm list returns `200` with empty `items`. Record the actual migration ID and evidence for the key and index.
3. Apply the fixture twice using `psql` with `ON_ERROR_STOP`. Compare counts and values. Capture the actual sample UUID from the list; use it for the detail and restart checks. Use independent database or HTTP sessions to verify persistence.
4. Start the full workspace using the documented launcher against an intended local setup and open Dashboard and the M02 previews. Stop the owned PostgreSQL service while keeping the API alive. Verify the health, readiness and instrument response behavior, then restart PostgreSQL and confirm recovery. Stop services you started and retain any development volume.

```bash
infra=backend/src/TradingAssistant.Infrastructure
api=backend/src/TradingAssistant.Api
dotnet ef dbcontext info --project "$infra" --startup-project "$api"
dotnet ef migrations list --project "$infra" --startup-project "$api"
dotnet ef migrations script --idempotent --project "$infra" --startup-project "$api" --output "$migration_review_file"
dotnet ef database update --project "$infra" --startup-project "$api"
```

Set `migration_review_file` to a scratch path before running these commands. Inspect the SQL before the update. Run the update a second time as part of the no-op check. Keep the scratch SQL output out of the implementation diff.

```bash
base=http://localhost:5080
curl -sS -i --max-time 15 "$base/api/v1/health"
curl -sS -i --max-time 15 "$base/api/v1/ready"
curl -sS -i --max-time 15 "$base/api/v1/instruments"
curl -sS -i --max-time 15 "$base/api/v1/instruments?offset=1&limit=1"
curl -sS -i --max-time 15 "$base/api/v1/instruments?offset=3"
curl -sS -i --max-time 15 "$base/api/v1/instruments/$sample_id"
curl -sS -i --max-time 15 "$base/api/v1/instruments/00000000-0000-0000-0000-000000000000"
curl -sS -i --max-time 15 "$base/api/v1/instruments/not-a-uuid"
curl -sS -i --max-time 15 "$base/api/v1/instruments?limit=101"
curl -sS -i --max-time 15 "$base/openapi/v1.json"
```

Set `sample_id` from the returned demo data. Also request a valid UUID absent from the fixture and record HTTP `404`. Inspect HTTP status and response, including error content type. A successful `curl` transport exit does not establish the HTTP status. If the API uses a different configured loopback port, set `base` accordingly.

## 4. Evidence

Use **Pass**, **Fail**, **Blocked** or **Pending**. Record command, working directory, exit status, HTTP status or relevant test names as appropriate. Use Pending for hosted results and owner review until observed. For Blocked, name what is missing and the exact check it prevents.

Paths below abbreviate the commands' absolute paths: **root** is the repository root; **scratch** was the session's `files/` directory; **copy** was `scratch/m03-workspace`. `infra` and `api` are the project paths defined above. Scratch scripts, logs, SQL and the copy were temporary verification material, not implementation deliverables.

### Automated command results

| Working directory | Actual command | Exit | Outcome |
| --- | --- | --- | --- |
| root | `git status --short --branch`; `git rev-parse HEAD`; `git remote get-url origin` (credential-redacting filter) | 0 | Baseline recorded above. |
| root | `git merge-base --is-ancestor b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47 HEAD`; `git log --oneline b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47..HEAD` | 0 | Exact required baseline; no later commits. |
| root | `dotnet --version`; `node --version`; `npm --version`; `docker version --format '{{.Client.Version}} {{.Server.Version}}'`; `docker compose version` | 0 with corrected PATH | Versions recorded above. |
| root | `dotnet restore --locked-mode`; `dotnet tool restore` | 0 | Locked restore and pinned EF tool succeeded. Locked restore also passed after the test fixture-copy project change. |
| root | `dotnet ef migrations add InitialInstruments --project backend/src/TradingAssistant.Infrastructure --startup-project backend/src/TradingAssistant.Api --output-dir Migrations` | 0 | Generated migration `20261126204038_InitialInstruments`, designer and snapshot. |
| root | `dotnet test backend/tests/TradingAssistant.UnitTests --no-restore` | 0 | 43 unit tests passed. |
| root | `dotnet build --no-restore` | 0 | Final build: zero warnings, zero errors. |
| root | `dotnet format --verify-no-changes --no-restore` | 2 initially; 0 finally | Generated migration had a BOM/CHARSET formatting issue. Corrected with `dotnet format --no-restore --include backend/src/TradingAssistant.Infrastructure/Migrations/20261126204038_InitialInstruments.cs`; subsequent verification passed. |
| root | `dotnet test --no-build` with `TRADING_TEST_CONNECTION` targeting disposable PostgreSQL | 0 | Final: **43 unit + 45 integration passed, 0 failed, 0 skipped**. Initial run before additional order cases: 43 + 42 passed. |
| root | `dotnet ef migrations has-pending-model-changes --project backend/src/TradingAssistant.Infrastructure --startup-project backend/src/TradingAssistant.Api` | 0 | No changes since the generated migration. |
| root | `bash -n scripts/dev.sh`; `node --test scripts/dev.test.mjs` | 0 | Six launcher regressions passed, including process cleanup and occupied-port protection. |
| frontend | `npm ci` | 0 | Locked install succeeded, audit reported zero vulnerabilities. Existing ESLint version emitted a deprecation warning; pins unchanged. |
| frontend | `npm run lint`; `npm run typecheck` | 0 | ESLint and strict TypeScript passed. |
| frontend | `npm run test` | 0 | Two files, six Vitest tests passed. |
| frontend | `npm run build` | 0 | Production build and all seven static pages succeeded. |
| frontend | `npx playwright install chromium`; `npm run test:e2e` | 0 | **Eight Chromium tests passed**, with the owned PostgreSQL instance stopped and no database connection supplied to Playwright's API. |
| root | `git diff --check`; `git diff --stat`; scoped `git diff` and new-file review | 0 | Reviewed implementation, tests, SQL, runbook and architecture boundaries. No frontend, launcher, workflow, production project-reference, package or runtime pin changes. |

### Disposable targets and manual commands

The first xUnit target was `trading_assistant_test` at `127.0.0.1:55432`, container `trading-assistant-m03-444b12b4-test`, started with:

```bash
docker run -d --rm --name trading-assistant-m03-444b12b4-test \
  -p 127.0.0.1:55432:5432 \
  -e POSTGRES_DB=trading_assistant_test -e POSTGRES_USER=trading_test \
  -e POSTGRES_PASSWORD=dummy_test_only postgres:17.12
timeout 45 sh -c 'until docker exec trading-assistant-m03-444b12b4-test pg_isready -h 127.0.0.1 -U trading_test -d trading_assistant_test; do sleep 1; done'
docker stop trading-assistant-m03-444b12b4-test
```

All commands exited 0. The container was stopped before Chromium and removed by `--rm`.

The manual target was a **new** Compose project `m03-444b12b4`, database `m03_manual_test` at `127.0.0.1:55433`, container `m03-444b12b4-postgres-1`, using only dummy credentials. A disposable copy of the current source, launcher and Compose definition was used with fresh local configuration so the owner's existing ignored settings and development volumes were never modified. Final xUnit checks also used this disposable database, each catalogue test creating and dropping a unique `m03_<uuid>` schema.

| Working directory | Actual command/action | Exit | Observed outcome |
| --- | --- | --- | --- |
| copy | `./scripts/dev.sh` | Running, then intentionally stopped | First scratch-only attempt could not compile Next.js through an external `node_modules` symlink. Readiness probe timed out (124). Removed only that symlink and ran `npm ci` in the copy (0); second launch printed **Ready**, serving frontend and API. No repository workaround was introduced. |
| root | `node <scratch>/m03-http-check.mjs pre-migration` | 0 | Real HTTP: health/ready `200`, list/detail safe `500`, Development OpenAPI `200`. |
| root | `docker exec m03-444b12b4-postgres-1 psql -U trading_test -d m03_manual_test -v ON_ERROR_STOP=1 -c 'SELECT current_database(), to_regclass('"'"'"Instruments"'"'"') AS instruments, to_regclass('"'"'"__EFMigrationsHistory"'"'"') AS migration_history;'` | 0 | Database identified; both tables absent after API startup and requests. |
| root | `dotnet ef migrations script --idempotent --project "$infra" --startup-project "$api" --output <scratch>/m03-migration.sql` | 0 | Reviewed SQL: only the history/table/index DDL, no application seed INSERTs. |
| root | `dotnet ef dbcontext info --project "$infra" --startup-project "$api"` | 0 | `m03_manual_test`, `tcp://127.0.0.1:55433`, command timeout 5; no credentials output. |
| root | `dotnet ef migrations list --project "$infra" --startup-project "$api"` | 0 | Exactly `20261126204038_InitialInstruments`, initially Pending. |
| root | `dotnet ef database update --project "$infra" --startup-project "$api"` (twice) | 0 each | Explicit migration succeeded; second update made no further schema/data changes. One migration-history row. |
| root | `node <scratch>/m03-http-check.mjs empty` | 0 | Exact `200 {"items":[],"offset":0,"limit":50}`. |
| copy | `docker compose exec -T postgres sh -c 'exec psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1' < infrastructure/seed/sample-instruments.sql` (twice) | 0 each | Both imports committed; exactly three expected rows, all sample. |
| root | `docker exec m03-444b12b4-postgres-1 psql -U trading_test -d m03_manual_test -v ON_ERROR_STOP=1 -c '\d "Instruments"' -c 'SELECT "MigrationId" FROM "__EFMigrationsHistory";' -c 'SELECT "Id", "ExchangeMic", "Symbol", "IsSample" FROM "Instruments" ORDER BY "ExchangeMic", "Symbol";'` | 0 | Verified six required columns/lengths, UUID primary key, unique MIC/symbol index, history and row values. |
| copy/root | `node <scratch>/m03-http-check.mjs seeded` | 0 | Full HTTP results below; captured records for later equality comparisons. |
| integrated browser | Opened Dashboard, Screener, Watchlists, Portfolio and Research on `http://localhost:3000` | Successful | Dashboard showed **Connected to API**. All four independent previews retained sample warnings and expected content. |
| copy | `docker compose stop postgres`; `node ../m03-http-check.mjs outage` | 0 | Same API stayed alive; health `200`, ready `503`, both catalogue routes safe `500`, OpenAPI `200`. |
| copy | `docker compose up -d --wait --wait-timeout 60 postgres`; `node ../m03-http-check.mjs recovered` | 0 | Readiness and reads recovered; saved records unchanged. API PID remained `33437` before/after database outage. |
| root | After stopping the owned launcher: `dotnet run --project backend/src/TradingAssistant.Api --no-launch-profile --no-build` with the same disposable target | Running, then intentionally stopped | New real API process PID `38448`; no migration/import at startup. |
| root | `node <scratch>/m03-http-check.mjs restarted` | 0 | List/detail identical to saved records after the process restart. |
| root | `docker exec m03-444b12b4-postgres-1 psql -U trading_test -d m03_manual_test -v ON_ERROR_STOP=1 -c "SELECT count(*) AS remaining_test_schemas FROM information_schema.schemata WHERE schema_name LIKE 'm03_%';"` | 0 | Zero test schemas remained after xUnit. |
| copy | `docker compose down`; `docker volume inspect m03-444b12b4_postgres_data --format '{{.Name}} {{index .Labels "com.docker.compose.project"}}'`; `docker volume rm m03-444b12b4_postgres_data` | 0 | Only the owned disposable container/network/volume removed; volume ownership confirmed before removal. |
| root | `ss -ltnp '( sport = :3000 or sport = :5080 or sport = :55432 or sport = :55433 )'`; `docker ps --format '{{.Names}}\t{{.Ports}}'` | 0 | All four test/application ports released and no owned containers left running. |

The temporary HTTP assertion script used native Node `fetch` with a 15-second timeout and asserted actual status, JSON shape, error media type and absence of provider/SQL/credential details. It did not treat a transport success as an HTTP pass. In seeded, recovered and restarted phases it checked:

| GET path | HTTP | Verified payload |
| --- | --- | --- |
| `/api/v1/health` | 200 | `{"status":"healthy"}` |
| `/api/v1/ready` | 200 | `{"status":"ready"}` |
| `/api/v1/instruments` | 200 | Three six-field rows; offset 0, limit 50; order `(XAAA,S-AO)`, `(XAAA,S-BG)`, `(XBBB,S-AO)`; all sample. |
| `/api/v1/instruments?offset=1&limit=1` | 200 | Only the second row. |
| `/api/v1/instruments?offset=3` | 200 | Empty items. |
| `/api/v1/instruments/10000000-0000-0000-0000-000000000001` | 200 | Same six-field record as the list. |
| `/api/v1/instruments/20000000-0000-0000-0000-000000000001` | 404 | Safe `application/problem+json`. |
| `/api/v1/instruments/00000000-0000-0000-0000-000000000000` | 400 | Safe `application/problem+json`. |
| `/api/v1/instruments/not-a-uuid` | 400 | Safe `application/problem+json`. |
| `/api/v1/instruments?limit=101` | 400 | Safe `application/problem+json`. |
| `/openapi/v1.json` | 200 | Both catalogue operations present. Automated assertions also inspected parameters, response schemas and all applicable error codes. |

| ID | Evidence to capture | Result | Actual evidence or blocker |
| --- | --- | --- | --- |
| M03-REP-001 | Worktree, origin, SHA, ancestry and reviewed intervening changes | Pass | Exact M02 baseline; initial state and ancestry recorded in section 1. |
| M03-REP-002 | References, diff scope, pins and dependency review | Pass | No project-reference or pin changes. Domain independent; Application depends only on Domain. Infrastructure owns EF/SQL. API owns transport/composition. Only the integration project adds fixture copying. |
| M03-DOM-001 | Unit cases and project dependency direction | Pass | `InstrumentTests`: valid/max-size fields, ASCII rules, blank/null names, name trimming, invalid MIC/currency and empty UUID. `IInstrumentReader` is Application-owned. |
| M03-DB-001 | Migration ID, snapshot, reviewed SQL and verified schema | Pass | Generated `20261126204038_InitialInstruments` and snapshot with EF 10.0.15, reviewed idempotent SQL, applied to fresh PostgreSQL. `MigrationEnforcesRequiredColumnsLengthsAndIdentity` and manual `\d` verify schema. Files await owner commit. |
| M03-DB-002 | PostgreSQL duplicate constraints and cross-exchange identity | Pass | `MigrationEnforcesRequiredColumnsLengthsAndIdentity` asserts PostgreSQL unique violations and named PK/index, and permits S-AO on two MICs. |
| M03-DB-003 | Pre-migration schema check, explicit migration and repeated no-op | Pass | `StartupAndReadinessDoNotCreateSchemaAndExplicitMigrationIsEmptyAndRepeatable` plus manual absent-table, empty-page and double-update checks. |
| M03-DB-004 | Fixture rerun, sample flags, row preservation and collision rollback | Pass | Manual double psql import; `DemoImportIsIdempotentAndPreservesAnExistingLogicalRow`; both `DemoUuidCollisionRollsBackEvenWhenLogicalRowWouldBeSkipped` cases verify rollback of earlier rows and retained existing data. |
| M03-DB-005 | Independent reads and API restart evidence | Pass | Independent contexts and API hosts in tests; real process change `33437` to `38448` retained identical parsed list/detail values. |
| M03-API-001 | Paging, order, defaults and empty cases | Pass | `ListIsOrderedPagedAndDurableAcrossApiHosts`, three deliberately out-of-order `DatabaseOrdersBeforeApplyingPagination` cases, and fresh/past-end/max-offset pages. Exact three-field page and six-field item shapes asserted. |
| M03-API-002 | Detail payload and missing UUID | Pass | Stored sample returns all six fields; missing valid UUID returns safe `404`, tested and exercised manually. |
| M03-API-003 | Invalid inputs, safe validation response and boundary limits | Pass | 23 `InvalidInputReturnsProblemDetails` cases; limits 1/100 succeed; max Int32 offset returns an empty page. Includes repeated/empty values, overflow and malformed/zero/nonstandard UUIDs. |
| M03-API-004 | Failure response, cancellation, timeout and query review | Pass | Both outage routes return safe `500`; missing-schema path also tested. `ReadsAreIndependentUntrackedAndCancellableWithBoundedTimeouts` verifies untracked independent reads, cancelled tokens and five-second timeouts. Reviewed server-side OrderBy/ThenBy/Skip/Take and token flow. |
| M03-API-005 | Development OpenAPI operations and schemas | Pass | `DevelopmentOpenApiDescribesInputsPayloadsAndProblems` checks both paths, integer query defaults, UUID parameter, payload properties and 200/400/404/500 metadata. Live document also retrieved. |
| M03-API-006 | Health/readiness outage and recovery, loopback and CORS | Pass | Existing five-origin CORS tests retained; missing config/liveness checks pass. Real database stop/recovery gives ready 503/200 while health stays 200. Listeners remain loopback; reviewed logs/errors/diff contain no real credentials. |
| M03-TST-001 | Backend restore, build, format and unit results | Pass | Locked restore, build, formatting and all 43 unit tests passed on SDK 10.0.404; initial generated-file encoding issue fixed. |
| M03-TST-002 | Real PostgreSQL integration results | Pass | All 45 integration tests passed, zero skipped, with isolated schemas on PostgreSQL 17.12. Existing readiness coverage retained and explicit missing-config regression added. |
| M03-TST-003 | Launcher syntax, regressions and manual startup | Pass | Bash syntax and six regressions pass; unchanged launcher reached Ready in an isolated real workspace. Owned application processes were stopped; no development settings/volume changed. |
| M03-TST-004 | Frontend and browser regression results | Pass | npm ci, lint, typecheck, six Vitest cases, production build and eight Chromium cases passed. Chromium ran with PostgreSQL stopped. |
| M03-TST-005 | Hosted job link and tested revision | Pending | No PR or hosted run requested/created. Owner must commit/push and record the existing `verify` job URL and tested revision after it actually runs. |
| M03-OPS-001 | README and milestone index review | Pass | Runbook documents migration, explicit fixture import, constraints, defaults, errors, empty catalogue and outage/restart behavior. Index links M03 with local/hosted status separated. |
| M03-OPS-002 | Complete evidence, working tree and owner review | Pass | Local evidence and all IDs recorded here; final state below. No staging/commit/push/PR/merge/deployment performed. Owner review itself remains Pending, not implied by this local pass. |
| M03-OPS-003 | Test target isolation and project boundary review | Pass | Only the two named disposable databases and generated test schemas were modified. Owned containers/volume cleaned up. Existing development config/data untouched; no provider, broker, order or cloud surface added. |

### Handoff

**Ready for owner review, not milestone closure.** No unresolved local failures or blocked local checks remain. Hosted job URL/tested revision and owner review are **Pending**. The existing CI `verify` job already builds and runs all xUnit tests against PostgreSQL and the frontend/browser gates, so it was preserved.

Changed paths: instrument Domain model; Application reader port/use cases; Infrastructure mapping/reader/registration and generated migration/designer/snapshot; API endpoints/registration; focused unit and integration tests with shared isolated PostgreSQL/API helpers; fixture SQL; README/index and this evidence report. The incoming prompt and specification were not rewritten. All scratch processes and disposable databases were stopped; temporary verification files were removed.

Final `git status --short --branch` (HEAD unchanged, nothing staged):

```text
## milestone/03-backend-api-database
 M README.md
 M backend/src/TradingAssistant.Api/Program.cs
 M backend/src/TradingAssistant.Infrastructure/Persistence.cs
 M backend/src/TradingAssistant.Infrastructure/TradingContext.cs
 M backend/tests/TradingAssistant.IntegrationTests/ApiTests.cs
 M backend/tests/TradingAssistant.IntegrationTests/TradingAssistant.IntegrationTests.csproj
 M docs/milestones.md
?? .github/prompts/milestone-03-backend-api-and-database.prompt.md
?? backend/src/TradingAssistant.Api/InstrumentEndpoints.cs
?? backend/src/TradingAssistant.Application/IInstrumentReader.cs
?? backend/src/TradingAssistant.Application/InstrumentCatalogue.cs
?? backend/src/TradingAssistant.Domain/Instrument.cs
?? backend/src/TradingAssistant.Infrastructure/InstrumentReader.cs
?? backend/src/TradingAssistant.Infrastructure/Migrations/
?? backend/tests/TradingAssistant.IntegrationTests/InstrumentApiTests.cs
?? backend/tests/TradingAssistant.IntegrationTests/InstrumentPersistenceTests.cs
?? backend/tests/TradingAssistant.IntegrationTests/PostgresDatabase.cs
?? backend/tests/TradingAssistant.IntegrationTests/TestApi.cs
?? backend/tests/TradingAssistant.UnitTests/InstrumentTests.cs
?? docs/milestones/03-backend-api-and-database-verification.md
?? docs/milestones/03-backend-api-and-database.md
?? infrastructure/seed/
```
