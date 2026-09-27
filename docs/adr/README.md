# Architecture decision records

Each significant decision is recorded as a short, immutable ADR: the context, the decision, and its consequences. A decision that changes later gets a new ADR that supersedes the old one, so the reasoning history is preserved.

| ADR | Decision | Status |
|---|---|---|
| [0001](0001-record-architecture-decisions.md) | Record architecture decisions | Accepted |
| [0002](0002-modular-monolith-with-clean-architecture.md) | Modular monolith with Clean Architecture | Accepted |
| [0003](0003-postgresql.md) | PostgreSQL 18 instead of SQL Server | Accepted |
| [0004](0004-single-container-serves-the-spa.md) | A single container serves the API and the SPA | Accepted |
| [0005](0005-no-reflection-on-hot-paths.md) | Minimal APIs and source generators instead of reflection | Accepted |
| [0011](0011-uuidv7-primary-keys.md) | UUIDv7 primary keys | Accepted |
| [0014](0014-committed-openapi-contract-and-generated-client.md) | Commit the OpenAPI document and the generated client | Accepted |
| [0015](0015-one-shot-migrator.md) | Apply migrations in a one-shot migrator process | Accepted |

Numbers 0006–0013 are reserved for the decisions planned in [the roadmap](../ROADMAP.md#7-architecture-decisions). Each is written when its milestone implements it.

## Template

```markdown
# NNNN. Title

- Status: Proposed | Accepted | Superseded by NNNN
- Date: YYYY-MM-DD

## Context
What forces are at play? What problem needs a decision?

## Decision
What we will do.

## Consequences
What becomes easier or harder, including the trade-offs we accept.
```
