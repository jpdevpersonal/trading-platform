# Milestone 1 — Project Foundation

**ID** M01  
**Specification** v1.0  
**Status** Implemented locally. See the [acceptance report](01-foundation-verification.md); hosted PR evidence and owner approval are pending.

## 1. Objective

From a fresh checkout, open the monorepo in VS Code, start PostgreSQL, launch the API and frontend, and run the tests. The browser shows whether the API is reachable using a typed HTTP integration.

## 2. Dependencies

- [Approved architecture](../architecture.md), [requirements](../requirements.md) and [engineering standards](../engineering-standards.md).
- Git, VS Code with GitHub Copilot, .NET 10 LTS SDK, Node.js 24 LTS with npm, Docker Engine with Compose v2, and a Chromium installation managed by Playwright.
- Implementation defaults selected for this handoff are .NET SDK 10.0.404, Node.js 24.22.3, stable Next.js 16 with its supported React peer, Tailwind CSS 4, EF Core and Npgsql 10, and PostgreSQL 17.12. Use at least the verified baseline patch where a newer security patch is required and record the exact versions used. Pin resolved JavaScript packages in the npm lockfile, use explicit .NET package versions, and pin the PostgreSQL image to an available patch tag or digest. Official references include [.NET downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), the [Node.js LTS release](https://nodejs.org/en/blog/release/v24.22.3), the [Next.js support policy](https://nextjs.org/support-policy), and the [PostgreSQL versioning policy](https://www.postgresql.org/support/versioning/).
- Docker was not discoverable in the documentation workspace. Check `docker version` and `docker compose version` on the implementation workstation. Record Docker-dependent checks as blocked if the engine cannot run and no isolated real PostgreSQL test instance is available.

## 3. In scope

- The monorepo layout, root developer commands and six .NET projects.
- Versioned API health and database readiness, safe errors, structured logging, explicit development CORS, and development OpenAPI.
- Responsive Next.js shell, five navigation placeholders and API connection status.
- Local PostgreSQL with a persistent volume, EF Core configuration and a documented migration workflow.
- Local verification, pull-request CI and reproducible developer documentation.

## 4. Out of scope

Market prices and data APIs, historical data ingestion, screeners, indicators and financial calculations, OpenAI and AI research, broker integrations including eToro and Trading 212, portfolio and trade journal functionality, user registration and login, trade execution and order submission, paid services and cloud deployment. Navigation placeholders are in scope. Future business tables and dummy data are outside scope.

## 5. Technical design

- **Local defaults.** Frontend `http://localhost:3000`, API `http://localhost:5080`, PostgreSQL host binding `127.0.0.1:5432`. Document overrides and keep URL hostnames consistent with the CORS origin. Use a thin root Compose entry point so the canonical service remains in `infrastructure/docker-compose.yml`.
- **Process health.** `GET /api/v1/health` returns HTTP 200 and JSON `{"status":"healthy"}` while the API process is running. It neither queries PostgreSQL nor requires live dependencies. Document it in OpenAPI, for example at `/openapi/v1.json` in Development. Use a consistent safe problem response for unexpected HTTP failures.
- **Database readiness.** Use `GET /api/v1/ready` with HTTP 200 and `{"status":"ready"}` when the configured PostgreSQL connection works, and HTTP 503 with `{"status":"unavailable"}` when it does not. Use a bounded async check. The API remains available to report liveness when PostgreSQL is down. Give each development or CI process the correct database settings through environment configuration.
- **Persistence.** Infrastructure owns EF Core configuration and migrations. Document how to create and apply the first real schema migration when needed, using the Infrastructure and startup project paths. Keep future domain tables out of M01. To verify volume persistence, use a disposable schema or database and remove the test data after verifying recovery.
- **Frontend.** Use an environment-configured typed health client with a bounded request. Validate the response and show loading, connected and API unavailable states. At a minimum, Dashboard displays connection status, while Screener, Watchlists, Portfolio and Research lead to usable placeholders. Show status in text and support keyboard navigation. The status represents API reachability.
- **Configuration.** Document root Compose environment and API shell environment setup separately. Compose's root `.env` is not automatically loaded by `dotnet run`. Use ignored local files or shell variables and provide dummy-only examples. Only the public API URL belongs in browser configuration.
- **Test isolation.** CI and local integration tests use isolated real PostgreSQL. Use disposable containers or an explicit test connection, never the development database. Fail clearly when a required dependency is unavailable.

## 6. Deliverables

A root solution with API, Application, Domain, Infrastructure, UnitTests and IntegrationTests; a Next.js frontend with an npm lockfile; local Compose infrastructure and example configuration; backend, frontend and browser tests; pull-request CI; and a README updated with commands actually verified. See the [repository layout](../architecture.md#repository-layout).

## 7. Acceptance criteria

All 31 checks below are required. The earlier discussion's closing summary said 30. The numbered checklist itself contains 31.

| ID | Pass condition | Verification |
| --- | --- | --- |
| M01-REP-001 | The repository contains the agreed frontend, backend, infrastructure and documentation directories. | Inspect repository layout. |
| M01-REP-002 | An ASP.NET Core solution contains API, Application, Domain, Infrastructure, UnitTests and IntegrationTests projects. | List solution projects and build. |
| M01-REP-003 | Project references follow the approved dependency direction. Domain and Application have no dependency on concrete Infrastructure. | Inspect project references. |
| M01-REP-004 | The frontend runs with strict TypeScript, Tailwind CSS and a package-manager lockfile. | Inspect config and run a clean `npm ci` and the frontend checks. |
| M01-REP-005 | Ignore rules cover secrets, build output, local data and developer-specific files. | Use `git check-ignore` and inspect tracked or staged paths where Git is available. |
| M01-API-001 | The backend starts locally and `GET /api/v1/health` returns HTTP 200 and valid JSON. | Call the running API and assert the contract in an HTTP integration test. |
| M01-API-002 | The health response explicitly reports service status and contains only operational data. | Assert the expected JSON and inspect the response. |
| M01-API-003 | Development OpenAPI is available and lists the health endpoint. | Start in Development and inspect the OpenAPI document. |
| M01-API-004 | Requests have structured logs and unexpected exceptions return consistent safe errors. | Inspect representative log output and verify the error path without adding a public fault route. |
| M01-API-005 | CORS permits only the explicitly configured local frontend origin. | Test allowed and disallowed origins, including relevant preflight headers. |
| M01-WEB-001 | Next.js loads on its configured localhost development port. | Start the frontend and load it in a browser. |
| M01-WEB-002 | A responsive shell contains a header, navigation and main content area. | Inspect desktop and narrow layouts and check keyboard navigation. |
| M01-WEB-003 | The shell provides Dashboard, Screener, Watchlists, Portfolio and Research placeholders. | Navigate to each destination. |
| M01-WEB-004 | A typed client requests and displays the backend health result. | Verify the browser against the running API. |
| M01-WEB-005 | Loading, successful connection and API-unavailable states render without a crash. | Test UI states and stop the API to check the failure experience manually. |
| M01-DB-001 | Compose starts a pinned supported PostgreSQL image with persistent local storage. | Inspect the resolved config and start the service. |
| M01-DB-002 | Database credentials come from local environment configuration and stay out of Git. | Inspect runtime configuration, examples and ignored paths. |
| M01-DB-003 | Infrastructure configures EF Core's PostgreSQL provider and documents migration strategy. | Inspect provider registration and the documented create/apply procedure. |
| M01-DB-004 | Database connectivity is validated by an integration test or a separate readiness check. | Exercise readiness against real PostgreSQL and verify the failure path. |
| M01-DB-005 | PostgreSQL container restart preserves its stored data. | Write disposable data, restart the container, read it back and clean up. |
| M01-TST-001 | `dotnet build` succeeds for the solution. | Run it from the root. |
| M01-TST-002 | `dotnet test` passes meaningful backend unit and integration tests. | Run both suites, including actual PostgreSQL integration. |
| M01-TST-003 | Frontend linting, TypeScript checking and production build pass. | Run `npm run lint`, `npm run typecheck` and `npm run build`. |
| M01-TST-004 | Vitest and React Testing Library verify at least one meaningful frontend behavior. | Run `npm run test` and inspect the assertions for connection states. |
| M01-TST-005 | Playwright verifies that the app loads and presents connection status. | Run `npm run test:e2e` with the real web app and API. |
| M01-TST-006 | GitHub Actions validates backend and frontend builds and tests on pull requests. | Review the workflow and record a successful PR run when authorized. |
| M01-OPS-001 | The README explains prerequisites, environment setup, startup, testing and shutdown. | Follow the instructions from a fresh checkout. |
| M01-OPS-002 | A new workstation can be configured using documented commands and example files. | Follow the setup with dummy values and no pre-existing local configuration. |
| M01-OPS-003 | Local startup and shutdown require no source changes. | Start and stop with the documented commands and configuration. |
| M01-OPS-004 | Changes introduce no real secrets, paid API keys, broker connections or trade execution. | Inspect new dependencies, route surfaces and configuration. |
| M01-OPS-005 | The Git-tracked set is free of embedded credentials and generated build artifacts. | Review tracked or staged paths and scan the intended diff without printing sensitive values. |

## 8. Testing

Run the backend build, formatting verification and test suites locally. Run lint, type checking, production build, component tests and the Chromium smoke test. Repeat the required automated gates in GitHub Actions on pull requests. For manual verification, navigate the shell, stop and restart the API, exercise independent liveness and database readiness, and confirm PostgreSQL data survives a container restart. Review project references, configurations, dependencies and intended tracked files.

## 9. Definition of done

All required checks pass with recorded evidence. Local and pull-request verification are green. The user reviews the scope and results before merging to main. A missing tool, pending PR run or missing manual result keeps the related criterion blocked or pending.

## 10. Completion report

After implementation, list the changed paths, exact tools and commands used, and an ID-by-ID Pass, Fail or Blocked result with evidence. Identify manual checks left to verify, CI status, and any follow-up work. Milestone closure requires owner review.
