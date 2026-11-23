# ADR 0001 — Local-first modular monolith

**Status** Accepted as the project baseline.

## Context

The project is developed in VS Code and GitHub Copilot one milestone at a time. The first milestone must prove a reproducible local workflow across browser, API and database.

## Decision

Use one GitHub monorepo with a Next.js frontend, an ASP.NET Core modular monolith, and PostgreSQL with EF Core. Run the host applications locally and manage PostgreSQL with Docker Compose. Use the dependency direction in [the architecture](../architecture.md). Keep future provider adapters behind backend interfaces. Broker and portfolio integrations are read-only when introduced and execution is outside scope.

## Consequences

The project has one backend process and a local database for development and verification. Supporting real PostgreSQL integration tests requires a local Docker engine or an isolated PostgreSQL instance supplied to the test harness. A later public deployment requires its own configuration and authentication design.
