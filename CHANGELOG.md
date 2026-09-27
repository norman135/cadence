# Changelog

All notable changes to Cadence are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/). Each release corresponds to a milestone in the [roadmap](docs/ROADMAP.md).

## [0.1.0] - M0: Foundation

A production-shaped foundation with no product features yet. It builds, tests, packages into containers, deploys and measures itself.

### Added

- **Backend** (.NET 10), a modular monolith with Clean Architecture:
  - domain building blocks: entities, aggregates with domain events, `Result`/`Error`
  - a CQRS request pipeline on a source-generated mediator, with logging and validation behaviors
  - PostgreSQL 18 through EF Core 10 and Npgsql, with DbContext pooling, a capped connection pool, `snake_case` naming and slow-query logging
  - a versioned Minimal API (`/api/v1`) with RFC 9457 problem details, source-generated JSON, and `GET /api/v1/system/info`
  - health endpoints (`/health/live`, `/health/ready`), with opt-in OpenTelemetry
  - a one-shot database migrator
  - Aspire orchestration for local development (PostgreSQL, Mailpit, migrator, API, Vite)
- **Frontend** (React 19, TypeScript, Vite 8):
  - an app shell with lazy-loaded routes, error boundaries and a 404 page
  - design tokens with automatic light and dark themes, and shadcn/ui-style components
  - a typed API client and TanStack Query hooks generated from the committed OpenAPI document
  - the React Compiler, and ESLint-enforced feature boundaries
- **Quality**:
  - backend unit, integration (Testcontainers PostgreSQL) and architecture tests: 50 tests
  - frontend tests with Vitest, Testing Library and MSW: 7 tests
  - query-count budgets in integration tests
  - OpenAPI and client drift checks in CI
- **Performance**:
  - frontend bundle budgets
  - a k6 load test baseline
  - memory budget checks
  - a BenchmarkDotNet project
  - baseline results in `docs/performance.md`
- **Delivery**:
  - a multi-arch (amd64 and arm64) chiseled, non-root container image, cross-compiled without emulation
  - a production Docker Compose stack with Caddy (automatic HTTPS), a tuned PostgreSQL config and memory limits on every service
  - CI that builds and smoke-tests the production stack on amd64 and arm64
  - tag-based releases to GitHub Container Registry, with provenance and an SBOM
- **Documentation**:
  - README, contributing guide, roadmap
  - architecture overview and performance report
  - ADRs 0001–0005, 0011, 0014 and 0015
