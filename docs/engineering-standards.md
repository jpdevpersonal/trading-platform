# Engineering standards

## Code and dependencies

- Keep nullable reference types enabled in C# and strict mode in TypeScript. Use clear names, small modules, and the repository's formatter and lint rules.
- Prefer built-in framework capabilities and existing project patterns. Add dependencies when the milestone needs them. Pin SDK and runtime versions and commit dependency lockfiles as appropriate. Use the same versions locally and in CI.
- Use semantic HTML, keyboard-accessible navigation, visible focus and readable status text. Keep feature UI and typed transport separate.
- Follow approved layer dependencies. Use dependency injection at the composition root. Use async I/O, propagate cancellation, and bound network timeouts.
- Version HTTP routes under `/api/v1`. Document contracts in OpenAPI. Validate untrusted inputs and return consistent safe errors. Use structured request logs without sensitive request details.

## Persistence, research and security

- Keep migrations in Infrastructure. Apply them explicitly to intended databases and review schema changes. Test database behavior against PostgreSQL.
- Store timestamps in UTC. Use decimal precision and explicit currencies for future monetary values. Keep provenance and audit history where required by the feature.
- Use deterministic rules for screening and risk features. Keep external AI output source-linked and subordinate to user decisions.
- Bind developer services to loopback, allow only explicit CORS origins, and keep credentials in ignored local configuration or environment variables. Commit example values only. Keep private credentials out of browser configuration and logs.
- Public deployment, auth design, paid services and integrations require their own milestone scope.

## Verification and review

| Layer | Verification |
| --- | --- |
| Frontend | ESLint, TypeScript, production build, Vitest and React Testing Library |
| Backend | .NET build, formatting verification and xUnit unit and integration tests |
| Persistence | Integration tests using a reachable, isolated PostgreSQL instance |
| Browser | Playwright smoke tests for critical workflows |
| Pull request | GitHub Actions runs builds and tests, and the reviewer checks milestone evidence |

A required criterion is complete only when its evidence is recorded. Preserve tests and checks when fixing failures. For a blocked check, record the dependency and remaining evidence needed.
