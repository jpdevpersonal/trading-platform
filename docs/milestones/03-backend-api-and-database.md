# Milestone 3 — Backend API and database

**ID** M03  
**Specification** v1.0  
**Status** Ready for local implementation from the accompanying prompt  
**Baseline** `jpdevpersonal/trading-platform` `main` at `b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47`

## 1. Objective

Run the local API against PostgreSQL. Apply the first application migration explicitly, load optional fictional instrument rows and query them through a versioned API. A database starts with an empty catalogue and returns the same records after an API restart.

## 2. Dependencies

- [Milestone 2](02-application-shell.md) was merged in [PR #2](https://github.com/jpdevpersonal/trading-platform/pull/2). Use the [merge commit](https://github.com/jpdevpersonal/trading-platform/commit/b17b0d8ca6f10ab84f1bc29c9f63f891c97d5c47) or a later compatible descendant. Review any intervening changes that affect M03.
- Read [requirements](../requirements.md), [architecture](../architecture.md), [ADR 0001](../decisions/0001-local-first-modular-monolith.md), [engineering standards](../engineering-standards.md) and the [local runbook](../../README.md). Use the checkout's pinned .NET, EF CLI, Node and PostgreSQL versions. The merge commit pins .NET SDK 10.0.404, dotnet-ef 10.0.15, Node 24.22.3 and PostgreSQL 17.12.
- Use the current local PostgreSQL setup for development and a separate disposable PostgreSQL database for verification. Keep the existing service ports, loopback bindings and Compose workflow.

## 3. In scope

- One initial business entity, Instrument, with a stable identifier and a unique exchange-and-symbol pair.
- A reviewed EF Core migration, explicit operator-applied schema setup and an optional idempotent local SQL demo import.
- A list endpoint with bounded pagination and a detail endpoint. Both read PostgreSQL through Application and Infrastructure.
- PostgreSQL integration tests, regression checks and concise local runbook updates.

## 4. Out of scope

Market or broker data ingestion, provider identifiers and mappings, prices, technical indicators, screening rules, watchlist management, portfolio persistence, research or AI integration, authentication and deployment. The existing web shell continues to show its independent fictional previews. Trade execution, order submission and broker write permissions remain outside the project scope.

## 5. Technical design

### Instrument identity and persistence

Use `TradingAssistant.Domain` for the instrument concept. Keep EF Core mappings and migrations in `TradingAssistant.Infrastructure`. Use the existing PostgreSQL context and database registration. Apply nullability, length limits, UUID primary key and the unique composite key in the PostgreSQL schema. Validate the field rules on domain creation. Reuse existing naming and mapping conventions where present. Document which rules PostgreSQL enforces directly.

| API field | Required representation | Rule |
| --- | --- | --- |
| `id` | UUID | Stable, nonempty application identifier. It is independent of broker and provider identifiers. |
| `symbol` | String, up to 32 characters | Starts with an ASCII uppercase letter or digit. Remaining characters may be uppercase ASCII letters, digits, dots or hyphens. |
| `exchangeMic` | Four-character string | Uppercase ASCII letters or digits. Stores a market identifier code. A full MIC registry check is deferred. |
| `name` | String, up to 200 characters | Trimmed nonblank display name. |
| `currency` | Three-character string | Uppercase ASCII letters. Stores the quote currency code. A full currency registry check is deferred. |
| `isSample` | Boolean | Required provenance flag. Every row in the local demo import sets it to true. |

The UUID identifies a catalogue row. Symbols can repeat across exchanges. The exchange-and-symbol key supports the first catalogue and is not a provider mapping or a model of symbol changes over time.

Generate the first application migration and model snapshot with the pinned EF tool. Review the SQL for the intended schema and index. Keep the initial catalogue empty after migration. Applying migrations and importing fixtures are separate explicit actions. Startup, health and readiness do not modify the schema or insert rows.

Provide a repository-owned SQL fixture at `infrastructure/seed/sample-instruments.sql`. Use three fictional instruments with fixed UUIDs and make one symbol appear on two distinct MICs. Apply the fixture in a single transaction and make it safe to run twice. A matching exchange-and-symbol row keeps its existing contents. A conflicting UUID attached to a different identity must fail the import without a partial write. Document explicit import into a disposable or intended local database using `psql` with `ON_ERROR_STOP`. Avoid logging connection strings or introducing credentials into SQL.

### Application and API

Application owns the catalogue read use cases and persistence port. Infrastructure implements asynchronous, no-tracking database queries. Page in PostgreSQL using `ORDER BY` exchange MIC and symbol before applying offset and limit. The unique pair prevents ties. Pass cancellation through HTTP, Application and EF Core and use bounded database connection and command timeouts. Return only the fields required by the contract.

API owns transport DTOs, HTTP input validation and the existing composition root. Use the existing Problem Details handling for unexpected failures and keep responses free of raw SQL, provider exceptions and connection details. Use local fixture values in documentation.

| Method and route | Response |
| --- | --- |
| `GET /api/v1/instruments?offset=0&limit=50` | `200` with `{ "items": [], "offset": 0, "limit": 50 }` on an empty page, or an ordered array of instrument DTOs in `items`. Include effective offset and limit. |
| `GET /api/v1/instruments/{id}` | `200` with one instrument DTO for a matching UUID. |
| Either route, invalid input | `400` with a safe Problem Details response. |
| Detail route, valid UUID with no row | `404` with a safe Problem Details response. |
| Either route, unexpected database failure | Safe `500` through the existing exception handler. |

Default `offset` to 0 and `limit` to 50 when omitted. Accept a single nonnegative 32-bit integer offset and a single integer limit from 1 to 100. Explicit empty values, repeated values, overflow and out-of-range values for these parameters return `400`. An offset past the last row returns an empty page. List responses omit a total count. Accept a nonempty UUID in standard hyphenated form at the detail route. Map a malformed or all-zero UUID to `400`; use a route shape that permits explicit validation. Document both endpoints, parameters, success payloads and applicable error codes in Development OpenAPI.

Keep `/api/v1/health` independent of PostgreSQL and preserve `/api/v1/ready` as a connectivity check. An available database can be ready before the first application migration. The instrument routes require the migration. Preserve the existing loopback and CORS configuration and the Milestone 2 browser behavior.

## 6. Deliverables

- Instrument domain model, Application read use cases and port, Infrastructure mapping and queries, initial migration and model snapshot, and two API endpoints.
- Optional SQL demo import with deterministic data, and documented migration, import, query and failure examples in the README.
- Focused unit and real PostgreSQL integration tests, retained baseline checks, an updated milestone index and a filled-in [verification checklist](03-backend-api-and-database-verification.md).

## 7. Acceptance criteria

All 22 criteria are required for closure. Record implementation and hosted evidence in the [verification checklist](03-backend-api-and-database-verification.md).

| ID | Requirement | Pass condition and evidence |
| --- | --- | --- |
| M03-REP-001 | Start from merged M02. | Record origin, worktree state, starting SHA and a successful ancestry check for the pinned merge. Review any relevant intervening changes. |
| M03-REP-002 | Preserve architecture and agreed scope. | Review the diff and project references against ADR 0001. Verify no dependency or runtime pin changes are needed, unless a justified in-scope necessity is documented and resolved before use. |
| M03-DOM-001 | Model a valid instrument independently of EF Core and HTTP. | Unit tests cover a valid instrument, the field size and character rules, blank names and an empty UUID. Confirm Domain independence and Application ownership of the read port. |
| M03-DB-001 | Introduce a reviewable first migration. | Pinned EF commands produce an inspectable SQL script and a committed model snapshot. A fresh disposable PostgreSQL target accepts the migration and has the expected columns, primary key and unique index. |
| M03-DB-002 | Enforce instrument identity in PostgreSQL. | A real database test rejects a duplicate UUID and a duplicate MIC/symbol pair and permits the same symbol on two distinct MICs. |
| M03-DB-003 | Keep schema changes and demo import explicit. | On a fresh reachable database, API startup and health/readiness requests leave the application table absent. Explicit migration creates an empty catalogue. A repeat migration is a no-op. |
| M03-DB-004 | Import optional demo rows safely. | Import into a disposable migrated database twice. Exactly three known fictional rows are present, all flagged sample. A matching logical row retains its existing fields and a different-identity UUID collision rolls back the import. |
| M03-DB-005 | Read durable data. | Query previously inserted rows from independent contexts and across an API process restart against the same database. |
| M03-API-001 | Return an ordered, bounded instrument page. | Real HTTP and PostgreSQL verify defaults, requested offset and limit, deterministic page order, and empty results before data load and beyond the final page. |
| M03-API-002 | Return one instrument by stable UUID. | Verify `200` and all six DTO fields for a stored UUID and `404` Problem Details for a missing valid UUID. |
| M03-API-003 | Validate request inputs. | Verify `400` Problem Details for malformed and zero UUIDs, repeated or empty pagination values, negative offsets, out-of-range limits and overflowing numbers. Boundary limits 1 and 100 succeed. |
| M03-API-004 | Keep failures safe and I/O bounded. | Verify a database failure returns safe Problem Details through the existing handler. Review cancellation, timeouts, query order and database-side pagination. |
| M03-API-005 | Document the versioned contract. | Inspect Development OpenAPI for both paths, input metadata, response schemas and applicable error responses. |
| M03-API-006 | Retain liveness, readiness and local boundaries. | Existing tests and a database stop/recovery check show health stays green and readiness returns `503` then `200`. Verify loopback/CORS remain restricted and real credentials are absent from the diff and error output. |
| M03-TST-001 | Pass backend static and unit checks. | Record successful locked restore, build, formatting verification and xUnit unit results with the pinned SDK. |
| M03-TST-002 | Verify persistence and routes against real PostgreSQL. | Record green xUnit integration results with the isolated target. Existing readiness and missing-config coverage remain active. |
| M03-TST-003 | Retain the local launcher. | Record Bash syntax and launcher regression results. During the manual pass confirm the startup command still works with the local setup. |
| M03-TST-004 | Retain Milestone 2 browser behavior. | Record green npm lockfile install, lint, typecheck, Vitest, production build and Chromium smoke tests. Browser tests still run without PostgreSQL. |
| M03-TST-005 | Pass hosted PR verification before closure. | Record the hosted `verify` job link and tested revision after an actual PR run. |
| M03-OPS-001 | Document operations and milestone state. | README covers explicit migration, optional fixture import, endpoint examples and troubleshooting. The index links the M03 spec and reflects actual verification. |
| M03-OPS-002 | Record evidence and preserve owner control. | Verification log contains exact commands, outcomes and every ID. Owner review is recorded when observed. The owner handles commits, pushes, PRs, merges and deployment. |
| M03-OPS-003 | Preserve safe local data handling. | Disposable tests never use development data or volumes. Manual commands target an explicitly identified local database. No provider, broker, execution or cloud surface appears in the changes. |

## 8. Testing

Run the command groups and the manual sequence in the [verification checklist](03-backend-api-and-database-verification.md). Use the existing real PostgreSQL test harness and CI service. Use deterministic fixture setup and scoped cleanup. Keep existing tests active, including the frontend liveness-only path.

## 9. Definition of done

Local implementation is ready for owner review once all required local criteria pass. Milestone closure also requires green hosted verification and owner review. Record unavailable checks as Blocked or Pending with the remaining evidence. The owner controls the merge.

## 10. Completion report

Complete the [verification checklist](03-backend-api-and-database-verification.md) with starting and ending Git state, changed paths, actual commands, evidence by ID and current review status.
