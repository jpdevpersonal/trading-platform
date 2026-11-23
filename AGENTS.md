# AI Trading Assistant repository instructions

Work milestone by milestone. Read [requirements](docs/requirements.md), [architecture](docs/architecture.md), [engineering standards](docs/engineering-standards.md), and the active spec in the [milestone index](docs/milestones.md). Use the active spec for scope and acceptance IDs. Raise material conflicts between the specification and an approved architecture decision before changing either.

Inspect the workspace before editing. Preserve unrelated changes. Outline the intended phases and checks, implement the authorized work locally, verify each phase, and report results against the criterion IDs. Record commands actually run and call out blocked checks. Leave pushes, deployment, and merges to the owner unless explicitly authorized.

The application supports stock research and human decision-making. Trade execution, order submission, and broker write permissions are outside project scope. Keep provider credentials server-side. Use localhost for development, dummy values in examples, and disposable test databases.

Keep the Domain independent. Application depends on Domain and owns use cases and ports. Infrastructure implements persistence and external adapters. API owns HTTP and dependency registration. Keep the frontend strict, typed and accessible. Check the [engineering standards](docs/engineering-standards.md) before adding a dependency, integration, or schema change.
