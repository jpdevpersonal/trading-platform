# Milestone 2 — Application Shell

**ID** M02  
**Specification** v0.1  
**Status** Scope approved 2026-11-24; see the [verification report](02-application-shell-verification.md) for implementation status  
**Baseline** `jpdevpersonal/trading-platform` `main` at `4cada776562271d570bbf465a4c0ae52dfd8d6fe`

## 1. Objective

Open the local workspace and see what each area is intended to support. From Dashboard, reach Screener, Watchlists, Portfolio and Research through a consistent desktop and narrow-screen shell. Each destination shows a small, visibly labelled fictional preview. The Dashboard still reports real local API connectivity and lets the user retry.

## 2. Dependencies and scope approval

- [Milestone 1](01-foundation.md) is merged. Its starter shell already provides the five routes, responsive header and navigation, and the live connection panel. The merged code has four one-paragraph placeholders, so M02 focuses on the layout and previews that remain. [Merged PR #1](https://github.com/jpdevpersonal/trading-platform/pull/1) · [Baseline commit](https://github.com/jpdevpersonal/trading-platform/commit/4cada776562271d570bbf465a4c0ae52dfd8d6fe)
- Use [the approved architecture](../architecture.md), [engineering standards](../engineering-standards.md), [requirements](../requirements.md) and [ADR 0001](../decisions/0001-local-first-modular-monolith.md). Keep the checkout's pinned tools, package locks and existing GitHub Actions workflow. The reviewed baseline uses .NET SDK 10.0.404, Node 24.22.3, npm 11.19.1 and PostgreSQL 17.12. [Baseline runbook](https://github.com/jpdevpersonal/trading-platform/blob/4cada776562271d570bbf465a4c0ae52dfd8d6fe/README.md)
- **Owner decision (2026-11-24).** Approved the frontend-only Application Shell scope, including responsive navigation, Dashboard links and four read-only fictional previews. This follows the later architecture roadmap, which placed backend and market data work in subsequent milestones. The earlier Screening and Backtesting proposal is not part of M02. No provider, credential or schema decision is needed.

## 3. In scope

- Refine the existing Next.js shell and Dashboard. Reuse the route set and API connection feature.
- Replace the four generic placeholder bodies with distinct read-only previews populated by small, typed fixture data.
- Keep the workflow local-first. Verify the shell with the existing frontend test tools and preserve the full Milestone 1 regression gates.

## 4. Out of scope

Live or historical market data, provider selection, eToro or other broker integrations, CSV imports, operational watchlists, portfolio calculations and holdings import, filters and rankings backed by screening rules, indicators, backtesting, charting, AI research and research generation, authentication, cloud deployment, and new database tables or APIs. Trade execution, order submission and broker write permissions are outside the project scope.

## 5. Technical design

### Shared shell

Reuse the App Router layout, `Navigation` and the route metadata in `frontend/src/lib/sections.ts`. Keep the routes `/`, `/screener`, `/watchlists`, `/portfolio` and `/research`, including direct loading, browser history and 404 behavior for unknown paths. Use a desktop navigation rail and a single wrapped nav above main content on small screens. Preserve one Workspace navigation landmark, one `main` landmark with `id="main"`, the skip-to-main focus behavior and exactly one page `h1`. Mark the current link with `aria-current="page"` and provide a visible focus indicator. At 360 CSS pixels the full page fits without horizontal scrolling. Check readable layout at 1280 CSS pixels as well.

### Dashboard and previews

Keep `ConnectionStatus` on Dashboard, backed by the existing typed health client. It shows loading, connected and unavailable states, and **Check again** retries. Its status represents the API process. Keep navigation and previews usable during an API outage. Give Dashboard four destination links with a short purpose for each area and identify them as previews.

| Route | Preview content |
| --- | --- |
| `/screener` | A sample universe with three fictional instrument rows. Show instrument name, sample symbol and a grouping such as sector. Treat the rows as an illustration of the workspace, without scores, trade signals or ranking. |
| `/watchlists` | One named example watchlist with three instruments from the sample universe. Show the list name and its members. |
| `/portfolio` | One set of three example positions showing fictional instrument names and illustrative quantities. |
| `/research` | Two sample research prompts or notes associated with the fictional instruments. Show an instrument, note title and a short excerpt for each. |

Display a visible **Sample data** label alongside the heading or content on each fixture-driven page, plus short copy identifying the examples as fictional. Use a shared typed instrument fixture where it prevents inconsistent names or symbols. Store the static fixtures under `frontend/src/features/` near the preview code. Keep the route and presentation components as Server Components where possible. Reuse existing styling or small shared components for repeated patterns. Preserve a logical reading order and semantic links, lists and tables. The previews have no edit, trade, refresh-data or filter controls. Only the existing API connection panel makes a feature network request.

### Application boundaries

Use the existing Next.js, TypeScript and Tailwind toolchain. Preserve `/api/v1/health`, `/api/v1/ready`, loopback bindings, CORS and the Compose startup workflow. No new dependencies, backend or schema changes are required. Keep local settings and credentials out of source, example fixtures and browser bundles.

## 6. Deliverables

- A responsive shell, Dashboard quick links and four read-only preview pages in the existing web app.
- Frontend component and real-server Chromium coverage using the repository's existing test setup.
- Updated README and milestone index in the local implementation diff. Add M02 to the index after scope approval. Record M02's actual verification status and leave hosted PR results pending until observed.
- A completed [verification checklist](02-application-shell-verification.md) with evidence for all required IDs.

## 7. Acceptance criteria

All 22 criteria are required. Record evidence in the verification checklist.

| ID | Requirement | Pass condition and evidence |
| --- | --- | --- |
| M02-REP-001 | Use merged M01 as the baseline. | Record the actual starting commit. Verify the pinned merge commit is that revision or an ancestor, and review any intervening changes relevant to M02. Record the worktree state before editing. |
| M02-REP-002 | Keep changes within the approved frontend shell scope. | Review the diff for frontend, tests and milestone or runbook docs. Confirm existing runtime pins, package locks, backend, database, local launch tooling and CI configuration are preserved. |
| M02-UX-001 | Retain all five workspace URLs. | Chromium verifies each direct URL shows its expected `h1`, an unknown path returns 404, and browser Back/Forward works across workspace navigation. |
| M02-UX-002 | Identify the current destination. | Chromium verifies one Workspace nav and one active link with `aria-current="page"` for each workspace route, including after navigation. |
| M02-UX-003 | Provide landmarks and keyboard entry. | Chromium and a manual Tab/Enter pass verify one banner, Workspace nav and `main`, one page `h1`, a visible skip link on focus, focus on `main` after activation, and keyboard-reachable navigation and retry controls. |
| M02-UX-004 | Support narrow and desktop layouts. | Check all five pages at 360 and 1280 CSS pixels. Verify each page fits the viewport and sample content, links and focus indicators remain visible and usable. Capture the tested widths and routes. |
| M02-UX-005 | Present previews with accessible structure. | Inspect the rendered headings and semantic lists or tables. Verify any table has an accessible name and associated column headings. Verify the reading order and visible focus on actionable elements. |
| M02-DASH-001 | Link to all four previews from Dashboard. | Chromium activates each link from within Dashboard `main` and verifies its destination heading. Preview purposes and labels are visible. |
| M02-DASH-002 | Show and retry real API connection status. | Vitest covers loading, response validation, unavailable and retry recovery. Chromium retains both localhost-origin happy paths against the real API. Manual API stop/restart verifies failure and recovery using **Check again**, with previews accessible during the outage. |
| M02-PRE-001 | Provide the Screener preview. | `/screener` shows three fictional rows with instrument names, sample symbols and groupings. |
| M02-PRE-002 | Provide the Watchlists preview. | `/watchlists` shows a named example list with three members from the shared sample universe. |
| M02-PRE-003 | Provide the Portfolio preview. | `/portfolio` shows three fictional positions with instrument names and sample quantities. |
| M02-PRE-004 | Provide the Research preview. | `/research` shows two instrument-linked example notes or prompts with titles and excerpts. |
| M02-PRE-005 | Label fictional data and keep the previews read-only. | Component or browser assertions cover the visible **Sample data** label and fictional explanation on each preview. Manual and diff checks confirm the content offers no mutable or feature-execution controls. |
| M02-DATA-001 | Keep fixtures deterministic and local. | Inspect typed, static fixtures and their instrument references. Direct page loads and refreshes produce the same sample content. With the API stopped, previews render from the running frontend. |
| M02-DATA-002 | Keep data and authority within project boundaries. | Review network calls, config and the diff for only the existing feature health call, with no provider or broker calls, account credentials, persisted fixture state or order surface. |
| M02-TST-001 | Pass the frontend build and static checks. | From `frontend/`, record successful `npm ci`, `npm run lint`, `npm run typecheck` and `npm run build`. Frontend build succeeds with the API and PostgreSQL stopped in the isolated verification environment. |
| M02-TST-002 | Exercise shell and preview behavior automatically. | Record green `npm run test` and `npm run test:e2e`. New assertions cover representative preview content and labelling, direct navigation, Dashboard links, active navigation, browser history, the unknown-path case and viewport fit. Retain all existing coverage. |
| M02-TST-003 | Pass the Milestone 1 backend and launcher regression checks. | Record locked restore, backend build, format verification, xUnit against isolated real PostgreSQL, Bash syntax check and launcher regression tests. |
| M02-TST-004 | Run the hosted PR checks before closure. | Once the owner opens a PR, record the successful existing `verify` job link and the tested revision. Local evidence is recorded separately. |
| M02-OPS-001 | Document the resulting workspace. | README describes the preview routes, the meaning of Sample data, API connection behavior and the existing local start/test workflow. The milestone index links the approved spec and records current status. |
| M02-OPS-002 | Record verification and keep owner control. | Record every ID, the exact commands run, actual outcomes, relevant evidence, residual blockers and review status. Implementation stays in the local worktree and owner actions cover staging, commits, pushes, PR creation, merge and deployment. |

## 8. Testing

Run the command groups and manual checks in the [verification checklist](02-application-shell-verification.md). Use the pinned tooling. Keep the disposable PostgreSQL integration target separate from development data. The Chromium tests use the real frontend and API. PostgreSQL is not needed for browser preview or liveness checks.

## 9. Definition of done

The owner approves scope before implementation. All required criteria have evidence before closure, including green hosted PR checks and owner review. The owner controls merge. Required checks awaiting a prerequisite or hosted run stay Blocked or Pending until verified.

## 10. Completion report

Fill in the [verification checklist](02-application-shell-verification.md) during implementation. Record the baseline, changed paths, exact commands, per-ID results and the hosted PR and owner review state.
