---
name: milestone-03-backend-api-and-database
description: Implement and verify the first PostgreSQL migration and read-only instrument catalogue
agent: agent
---

# Implement Milestone 3 — Backend API and database

Implement this milestone locally in the full `jpdevpersonal/trading-platform` checkout. This prompt authorizes the scope in the [Milestone 3 specification](../../docs/milestones/03-backend-api-and-database.md). Use its 22 acceptance IDs and [verification checklist](../../docs/milestones/03-backend-api-and-database-verification.md). Read linked files from disk if the editor does not resolve links.

## 1. Inspect the baseline

- Read [AGENTS.md](../../AGENTS.md), [Copilot instructions](../copilot-instructions.md), [requirements](../../docs/requirements.md), [architecture](../../docs/architecture.md), [ADR 0001](../../docs/decisions/0001-local-first-modular-monolith.md), [engineering standards](../../docs/engineering-standards.md), [milestone index](../../docs/milestones.md), the M03 spec and checklist, and [README](../../README.md). The initial index may stop at M02. The accompanying M03 spec defines this request's scope.
- Record `git status --short --branch`, origin identity and `git rev-parse HEAD`. Run `git merge-base --is-ancestor b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47 HEAD` and review relevant later changes if present. Work on the existing M03 branch or create `milestone/03-backend-api-database` from a verified compatible baseline if needed. Preserve unrelated edits and the three M03 guidance files. If baseline or working-tree conflicts make an edit unsafe, identify the specific conflict before editing.
- Inspect the actual backend project references, EF context and registration, API route and error patterns, test harness, locked packages, EF tool manifest, Compose file, launcher, browser smoke configuration and PR workflow. Identify which existing pieces will be reused. Plan the schema, API, integration verification and runbook phases, then execute the authorized work. Raise a material architecture conflict before changing affected code.

## 2. Implement persistence and its verification

- Add only the Instrument business model in Domain. Implement the field and identity rules from the spec. Keep the model independent of EF Core, HTTP and provider SDKs. Add focused xUnit tests for the stated invariants.
- Extend the existing Infrastructure context and registration. Keep migration and mapping in Infrastructure. Generate the migration and snapshot using the repository-pinned dotnet-ef, then inspect the SQL. Check required columns, lengths, UUID primary key and unique MIC/symbol pair. Leave the schema empty after migration and apply it explicitly to a disposable database for verification. Preserve readiness as a connectivity check. Keep startup and readiness free from schema or seed writes.
- Add `infrastructure/seed/sample-instruments.sql` as a separate, opt-in transactional demo import. Use the spec's three fictional rows, repeatable UUIDs, a repeated symbol across distinct exchanges and visible API provenance through `isSample`. Handle repeat import and conflicting UUIDs exactly as specified. Test against PostgreSQL, including preserving an existing logical row and rollback on UUID collision.

## 3. Implement and verify the read API

- Put catalogue use cases and the read port in Application. Implement async no-tracking reads in Infrastructure. Sort and page in PostgreSQL, pass cancellation through every layer and bound connection and command timeouts. Keep the API endpoints on the existing `/api/v1` group, with DTO mapping and validation at the HTTP boundary.
- Implement the list and UUID detail contracts exactly as specified. Validate explicit empty and repeated query values, integer bounds and UUID inputs. Return the defined Problem Details outcomes and reuse the safe exception handler. Register required services through existing composition. Document request and response metadata in the Development OpenAPI endpoint.
- Cover fresh and populated databases, invalid input, paging, detail and safe failure with real HTTP tests using the existing xUnit and PostgreSQL setup. Reuse isolated fixture setup and avoid colliding fixtures or schema changes across tests. Keep the existing health, readiness, CORS, configuration and browser checks. Do not introduce providers, market feeds, additional business entities or frontend feature changes.

## 4. Verify, document and report

- Use the [verification checklist](../../docs/milestones/03-backend-api-and-database-verification.md). Run locked restore, EF tool restore, build, formatting check, xUnit against disposable PostgreSQL, launcher checks, npm lockfile install, frontend lint, typecheck, Vitest, build and Chromium. Preserve the current CI job and test service if they already run the new checks. If a required dependency is missing, record the blocker and complete independent checks.
- Exercise the manual migration, idempotent import, API response, restart and PostgreSQL outage/recovery scenarios. Work with explicitly identified local databases. Keep credentials out of output. Preserve explicit migration and fixture-import commands in the README and document what readiness checks, the effective pagination defaults and the effect of leaving the catalogue empty.
- Update the milestone index and fill the checklist with actual commands, outcomes and all 22 IDs. Review the final diff for architecture, scope and data safety. Record the local result separately from the hosted job and owner review, which stay Pending until observed.
- Leave the working tree for the owner to review. Do not stage, commit, push, open a PR, merge, deploy or touch outside systems. Finish with what works locally, the test results, and any remaining blockers.
