---
name: milestone-01-foundation
description: Implement and verify Milestone 1 of AI Trading Assistant locally
agent: agent
---

# Implement Milestone 1 — Project Foundation

Implement M01 in the current local workspace. Read [AGENTS.md](../../AGENTS.md), [Copilot instructions](../copilot-instructions.md), [requirements](../../docs/requirements.md), [architecture](../../docs/architecture.md), [engineering standards](../../docs/engineering-standards.md), and the [Milestone 1 specification](../../docs/milestones/01-foundation.md) before editing. Treat the specification and all 31 acceptance IDs as part of this prompt. Read them from disk if links are not followed automatically.

## 1. Inspect and plan

- Check the workspace root, existing files, repository state and tool versions. Preserve existing changes. If Git is initialized and safe to use, work on a local branch such as `milestone/01-foundation`. If this is a docs-only directory and has no Git metadata, work in place and report which Git and PR checks need the intended repository.
- Check `dotnet --list-sdks`, `node --version`, `npm --version`, `docker version` and `docker compose version`. Use the supported baselines in the spec. Print no credentials. If a prerequisite is missing, continue independent work, document the blocker and rerun checks when the prerequisite is available.
- Briefly state the plan, affected paths and order of verification. This request authorizes implementation locally. Proceed through the phases below, report verification at each phase, and ask only when a material architecture or scope choice cannot be resolved from the approved docs.

## 2. Create the repository and backend foundation

- Keep a root-level .NET solution discoverable by `dotnet build` and `dotnet test`. Place `TradingAssistant.Api`, `TradingAssistant.Application`, `TradingAssistant.Domain` and `TradingAssistant.Infrastructure` under `backend/src/` and `TradingAssistant.UnitTests` and `TradingAssistant.IntegrationTests` under `backend/tests/`.
- Apply approved project dependencies. Pin the SDK with `global.json`, use nullable C#, and add targeted ignore and formatting configuration. Keep unused feature modules and speculative dependencies out.
- Implement `GET /api/v1/health` with HTTP 200 and `{"status":"healthy"}` independently of database availability. Expose a development OpenAPI document containing the route, add structured request logs, and use standard safe problem responses for unexpected exceptions. Configure the explicit matching local frontend origin. Test successful HTTP contract, CORS allow and deny behavior, and safe error handling without a public test-only endpoint.
- Add meaningful xUnit unit and integration tests. Test real logic or configured behavior instead of trivial framework assertions or placeholders.

## 3. Create the web foundation

- Scaffold the Next.js App Router with React, strict TypeScript, Tailwind and npm. Pin supported tool versions and commit the npm lockfile. Use the latest supported stable patch in the approved Next.js 16 line that is compatible with the resolved React dependencies. Keep the shell small. Charting and provider clients belong to later milestones.
- Provide a responsive header, navigation and main region, with Dashboard, Screener, Watchlists, Portfolio and Research destinations. Render actual placeholders without fabricated balances, securities, recommendations or prices.
- Read a public API base URL from documented environment configuration. Use a typed, validated, timeout-bounded browser health client and show clear loading, connected and unavailable states. Let the app build without a running backend. Test these states with Vitest and React Testing Library.

## 4. Set up local PostgreSQL and readiness

- Compose runs PostgreSQL only in M01. Keep the service in `infrastructure/docker-compose.yml` and make `docker compose up -d` work from the root. Use a pinned PostgreSQL image, named volume, loopback binding and a database health check. Document how to stop it without removing the volume. Keep example config dummy-only and Git-ignore actual local environment files.
- Configure EF Core and Npgsql in Infrastructure with explicit environment settings, and describe the migration workflow using the correct Infrastructure and startup project paths. Do not create empty business tables or automatically apply schema changes at API startup.
- Add the separate `GET /api/v1/ready` contract in the spec. Verify readiness against actual isolated PostgreSQL and handle an unavailable database with a safe response while liveness stays up. Verify a disposable record survives a container restart, then remove the test data.
- Use isolated disposable PostgreSQL for xUnit and CI. Tests must fail clearly if the necessary test instance cannot start or connect. Do not use SQLite or an in-memory database as evidence for PostgreSQL criteria.

## 5. Wire quality gates and verify

- Add GitHub Actions pull-request checks for backend restore, build, formatting and unit/integration tests; frontend lockfile install, lint, type checking, component tests and production build; and a Playwright Chromium smoke test against the running web app and API. Run real PostgreSQL in CI for persistence checks, with test-only configuration. Use read-only workflow permissions where sufficient. Use readiness polling with a timeout to start test dependencies.
- Expose and run the root `dotnet build` and `dotnet test` commands, plus `npm ci`, `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build` and `npm run test:e2e` in `frontend/`. Run the selected `dotnet format` verification and record its command. Playwright must assert that the shell loads and that the connection-status UI is visible. Use actual API connectivity for the happy path.
- Follow the manual verification in the spec, including all placeholders, narrow layout, backend shutdown and recovery, separate database failure, and PostgreSQL volume persistence. Fix in-scope failures and rerun affected checks. Update README with exact prerequisites, startup commands, ports, migration, tests, troubleshooting and shutdown. Check the setup from clean configuration.
- Inspect the final diff for project reference direction, scope, secrets and generated output. Use Git inspection where available. Do not stage, commit, push, deploy, run destructive commands, alter external accounts or merge unless separately asked.

## Scope and completion

Implement the local foundation only. Leave market data, screening, indicators, financial calculations, OpenAI, broker and CSV imports, portfolio functionality, journaling, authentication, order execution, cloud services and paid services to a separately authorized milestone or outside project scope as specified.

Report changed files, actual commands and outcomes, and a result with evidence for each criterion from M01-REP-001 through M01-OPS-005 as listed in the specification. Use Pass, Fail or Blocked. Record the cause for any blocked check and distinguish a workflow file that exists from a successful hosted PR run. Preserve the user's control of PR creation and merge.
