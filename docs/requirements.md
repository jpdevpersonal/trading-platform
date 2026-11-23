# Product requirements

## Product goal

Give an individual investor a workspace for stock screening, watchlists, evidence-backed research, and portfolio review. The user makes investment decisions and places orders independently of the application.

## Product boundaries

- Start with a local-first development experience. Public exposure of private data requires a later authentication and security milestone.
- The app may eventually consume supported market-data providers and read-only broker or CSV data. Provider selection, credentials, and any live integration belong to later milestones.
- Future screeners and financial calculations must be deterministic and testable. If AI research is added, show sources and distinguish evidence from interpretation. AI output cannot initiate trades or govern deterministic rules.
- Order submission, cancellation, modification, automatic execution, and broker write permissions are outside product scope.

## Milestone 1

Deliver the local foundation, a versioned API health route, a browser shell that shows API connectivity, PostgreSQL infrastructure, and repeatable verification. The [specification](milestones/01-foundation.md) defines the deliverables and acceptance criteria.
