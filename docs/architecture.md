# Architecture

## Approved baseline

| Area | Choice |
| --- | --- |
| Repository | One GitHub monorepo, developed locally with VS Code and GitHub Copilot |
| Frontend | Next.js App Router, React, strict TypeScript, Tailwind CSS, shadcn/ui when needed, and Lightweight Charts when a charting milestone calls for it |
| Backend | ASP.NET Core and C#, modular monolith with lightweight Clean Architecture |
| Persistence | PostgreSQL via Docker Compose, EF Core and Npgsql |
| Integrations | Market data and OpenAI behind backend interfaces in their own milestones, and read-only broker or CSV ingestion when specified |
| Verification | Vitest and React Testing Library, xUnit, Playwright, GitHub Actions |
| Deployment | Local development first. Cloud deployment is outside Milestone 1. |

The baseline uses one backend process and one relational database. Python, separate microservices, Kubernetes, and Redis are outside the baseline.

## Dependency direction

| Project | Responsibilities | Allowed production project references |
| --- | --- | --- |
| `TradingAssistant.Domain` | Domain concepts and deterministic rules | None |
| `TradingAssistant.Application` | Use cases and interfaces for external capabilities | Domain |
| `TradingAssistant.Infrastructure` | EF Core, PostgreSQL and future provider adapters | Application, Domain as needed |
| `TradingAssistant.Api` | HTTP contracts, configuration and composition root | Application and Infrastructure |
| UnitTests and IntegrationTests | Verify appropriate layers and HTTP/persistence boundaries | Projects under test |

The frontend consumes HTTP contracts and has no database connection. The API composes implementation details through dependency injection. Keep EF Core types out of Domain and Application, and keep HTTP-specific models at the API boundary.

## Repository layout

| Path | Contents |
| --- | --- |
| `frontend/src/` | `app`, `components`, `features`, `hooks`, `lib` and `types` as needed |
| `backend/src/` | API, Application, Domain and Infrastructure |
| `backend/tests/` | UnitTests and IntegrationTests |
| `infrastructure/docker-compose.yml` | PostgreSQL service and local volume |
| `.github/` | Copilot guidance, path-specific instructions when needed, prompts and CI workflows |
| `docs/` | Requirements, engineering standards, decisions and milestone specs |

Prefer a thin root Compose entry point for the documented root workflow. The infrastructure file owns the service definition. Add a directory when its first real file needs it.

## Milestone 1 topology

The browser calls the ASP.NET Core service on an explicit localhost origin. The process health route is independent of PostgreSQL. A separate readiness route checks PostgreSQL through Infrastructure. The API and frontend run on the host, while Compose runs only PostgreSQL in this milestone.

See [the local-first architecture decision](decisions/0001-local-first-modular-monolith.md) and [the foundation specification](milestones/01-foundation.md).
