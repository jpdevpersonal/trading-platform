# Milestones

| ID | Milestone | Status |
| --- | --- | --- |
| M01 | [Project Foundation](milestones/01-foundation.md) | [Verified locally](milestones/01-foundation-verification.md); hosted PR checks and owner review pending |
| M02 | [Application Shell](milestones/02-application-shell.md) | [Verified locally](milestones/02-application-shell-verification.md); hosted PR checks and owner review pending |
| M03 | [Backend API and database](milestones/03-backend-api-and-database.md) | [Verified locally](milestones/03-backend-api-and-database-verification.md); hosted PR checks and owner review pending |

Use the [milestone template](milestones/_template.md) to define the next milestone. Assign stable acceptance IDs and record verification in the completion report. Subsequent feature scope is agreed milestone by milestone.

## Workflow

1. Review the architecture, scope and acceptance criteria.
2. Work in a local branch when a Git repository is available. Inspect state and preserve unrelated changes.
3. Implement and verify one phase at a time. Update the implementation notes and retain command and test evidence.
4. Review the changes against the specification. Milestone approval follows local verification, green pull-request checks and owner review. The owner controls merging to main.
