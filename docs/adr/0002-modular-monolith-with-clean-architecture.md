# 0002. Modular monolith with Clean Architecture

- Status: Accepted
- Date: 2026-09-23

## Context

Cadence must run on a single server with 1–2 GB of RAM, serving about 10 concurrent users. It also needs to demonstrate enterprise-grade structure: clear boundaries, testability, and room to grow over ten milestones.

Microservices would give strong boundaries, but each service adds a runtime (roughly 50–100 MB of memory each), network hops, distributed transactions, and operational overhead. None of that pays off at this scale.

## Decision

Build a **modular monolith**: one deployable process, structured with **Clean Architecture** so dependencies only point inwards.

```
Cadence.Api  →  Cadence.Infrastructure  →  Cadence.Application  →  Cadence.Domain
```

- **Domain**: entities, aggregates, value objects and domain events. No dependencies at all.
- **Application**: use cases as commands and queries (CQRS), dispatched through a mediator pipeline that adds logging and validation.
- **Infrastructure**: EF Core, PostgreSQL, and later email, file storage, the job queue and the outbox.
- **Api**: HTTP endpoints, SignalR hubs, authentication, and hosting of the SPA.

Architecture tests (`tests/Cadence.ArchitectureTests`) fail the build if a layer takes a forbidden dependency.

The Application layer will be allowed to use EF Core's querying abstractions (`DbSet`, LINQ) directly for read models. Projecting straight into DTOs is the most important performance technique on read paths, and wrapping it behind repositories would hide it. Npgsql and other provider specifics stay in Infrastructure.

## Consequences

- One process, one deployment, and in-process calls. This is the lowest memory and latency option.
- Transactions across features are local database transactions, not sagas.
- Boundaries rely on discipline and automated architecture tests rather than on network separation.
- If a part ever needs to scale independently, the layer boundaries and domain events make extracting it feasible. The scale-out path in ROADMAP §9.6 covers running several replicas of the monolith first.
