# AI Trading Assistant

A local-first web application for stock research and human-led swing-trading decisions. The product scope is defined in [requirements](docs/requirements.md).

This checkout contains the planning documents for [Milestone 1 — Project Foundation](docs/milestones/01-foundation.md). Application files, local startup configuration, tests, and CI are Milestone 1 implementation deliverables.

## Work from VS Code

Open this folder as the workspace. Read [AGENTS.md](AGENTS.md) and the [Milestone 1 specification](docs/milestones/01-foundation.md). When ready to begin implementation, run [the Milestone 1 prompt](.github/prompts/milestone-01-foundation.prompt.md) in a local VS Code Copilot agent session. You can paste the Markdown body into Agent chat if the prompt picker is unavailable.

## Planned local commands

After implementation, the README must document verified prerequisites, example configuration, start and stop procedures, and tests. These are the expected commands from the indicated working directories.

| Directory | Purpose | Command |
| --- | --- | --- |
| Root | Start PostgreSQL | `docker compose up -d` |
| Root | Start API | `dotnet run --project backend/src/TradingAssistant.Api` |
| Root | Build and test API solution | `dotnet build` and `dotnet test` |
| `frontend/` | Install and start the web app | `npm ci` and `npm run dev` |
| `frontend/` | Check and build the web app | `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build` |
| `frontend/` | Run browser smoke test | `npm run test:e2e` |
| Root | Stop PostgreSQL and retain data | `docker compose down` |

See the [architecture](docs/architecture.md), [engineering standards](docs/engineering-standards.md) and [milestone index](docs/milestones.md).
