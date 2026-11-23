# GitHub Copilot instructions

Use [AGENTS.md](../AGENTS.md) for repository-wide rules and [the milestone index](../docs/milestones.md) for the active scope. Read the linked specification before working.

- Implement only the requested milestone. Preserve existing work and keep changes reviewable.
- Use Next.js App Router, React and strict TypeScript on the frontend. Keep HTTP behind a typed client. Prefer semantic elements and accessible feedback.
- Use ASP.NET Core with the approved project references. Keep business rules out of transport and persistence. Put PostgreSQL and EF Core in Infrastructure.
- Use explicit local configuration and keep credentials out of commits, browser bundles, logs, and error responses. The application supports research and human decisions. Order placement and broker write permissions are out of scope.
- Run the acceptance checks and report pass, fail or blocked for each applicable ID with evidence. A skipped test is not a pass.
