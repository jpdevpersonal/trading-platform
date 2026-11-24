---
name: milestone-02-application-shell
description: Implement and verify the approved AI Trading Assistant Application Shell locally
agent: agent
---

# Implement Milestone 2 — Application Shell

This prompt is for VS Code Copilot Agent mode in the full local `jpdevpersonal/trading-platform` checkout. Begin implementation only when the owner explicitly approves the Application Shell scope in the current request or in the spec. If approval is absent, summarize the scope decision in section 2 of the specification and wait for it.

## 1. Inspect the baseline and plan the work

- Read [AGENTS.md](../../AGENTS.md), [Copilot instructions](../copilot-instructions.md), [requirements](../../docs/requirements.md), [architecture](../../docs/architecture.md), [ADR 0001](../../docs/decisions/0001-local-first-modular-monolith.md), [engineering standards](../../docs/engineering-standards.md), [milestone index](../../docs/milestones.md), [README](../../README.md), [Milestone 2 spec](../../docs/milestones/02-application-shell.md) and [verification checklist](../../docs/milestones/02-application-shell-verification.md). Read them from disk if links do not open. All 22 acceptance IDs and the checklist are part of this implementation request. The merged index may list only M01 on first use. The approved M02 spec named here supplies this request's scope.
- Verify the actual worktree, Git status, starting revision and ancestry of the M01 merge `4cada776562271d570bbf465a4c0ae52dfd8d6fe`. Read relevant changes if the approved starting revision is newer. Preserve all unrelated work. Start from a local branch such as `milestone/02-frontend-shell` when the worktree is safe to use. If there is a conflicting worktree, a docs-only directory or an absent/divergent baseline, identify the issue before editing.
- Inspect the App Router layout, Dashboard, `[section]` page, navigation, section metadata, `ConnectionStatus`, typed health client, tests and package scripts. Use repository-pinned tools. Outline the frontend shell, previews, tests and verification phases. Raise material scope or architecture conflicts before making an affected change.

## 2. Refine the application shell and Dashboard

- Work in the existing App Router and Tailwind layout. Keep Dashboard `/` and `/screener`, `/watchlists`, `/portfolio` and `/research` with direct loads, browser history and 404 behavior. Reuse the metadata and shared `Navigation` component. Provide a desktop rail and a single wrapped navigation above main content on small screens. Keep a single active link with `aria-current="page"`, skip-to-main focus behavior, one main landmark, one route `h1`, and visible keyboard focus. Check the rendered fit at 360 and 1280 CSS pixels.
- On Dashboard, add four labelled preview links with one-line descriptions. Preserve the existing live `ConnectionStatus` component, connection states and retry. The panel checks the API process via `/api/v1/health`. Preserve health-client validation and cancellation. Preview pages must load with the API unavailable.

## 3. Build the four previews

- Add small deterministic TypeScript fixtures under `frontend/src/features/`, sharing instrument identities where useful. The examples are fictional. Display **Sample data** and an explicit fictional-data explanation on each preview page. Keep fixtures in source and render them locally. Use server components where they fit, semantic links, lists or tables, and existing responsive styles. Give each table an accessible name and proper column headings.
- Screener shows three sample instruments with name, symbol and grouping. Watchlists shows one named example list with the same three instruments. Portfolio shows three fictional positions with instrument name and illustrative quantity. Research shows two sample research prompts or notes, each with an instrument, title and excerpt. Keep these previews read-only and omit edit, screening, refresh-data and order actions.
- Limit changes to frontend, tests and local docs. Dependencies, package locks, backend projects, schema, existing local startup behavior and CI configuration are baseline constraints. Scope excludes market data, charts, screening rules, backtesting, working watchlists, account data, AI integration, authentication and cloud deployment. Trade execution, order submission and broker write permissions are outside project scope. Keep all local services on loopback and all credentials out of the frontend, fixtures and logs.

## 4. Test, document and report

- Extend Playwright using its existing real-server setup. Preserve both localhost-origin API and HMR checks and existing narrow-shell coverage. Cover direct routes, an unknown-path 404, browser history, Dashboard links, active nav, keyboard entry, viewport fit and visible preview content and labels. Add targeted Vitest and React Testing Library coverage for preview semantics or labelling. Keep the existing connection-state and retry assertions.
- Follow both command groups and the manual scenarios in the verification checklist. Run backend locked restore, build, format verification, xUnit with disposable real PostgreSQL and the launcher regressions. Run frontend lockfile install, lint, type checking, Vitest, build without the API or PostgreSQL, and Playwright Chromium. Exercise manual API outage and recovery with a separate backend process so the frontend stays up. Use local processes and databases you own. Record missing prerequisites as blockers and continue independent in-scope work.
- Update the local README and milestone index after approval. Describe the preview routes, fictional fixture labels, retained live API status and local verification. Report the actual M02 state. Review the diff and fill in the checklist with every acceptance ID, commands run, exit status, test or observation evidence, changed paths and pending owner checks.
- Leave the implementation and evidence in the local working tree. The owner handles staging, commits, pushes, PR creation, deployment and merge. Record hosted PR verification as Pending until an actual run exists. Finish by summarizing local results, any blocked criteria and the outstanding hosted run and owner review.
