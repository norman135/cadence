# Changelog

All notable changes to Cadence are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/). Each release corresponds to a milestone in the [roadmap](docs/ROADMAP.md).

## [Unreleased] - M2: Brand & design system

### Added

- **Brand and design system** in `design/`:
  - the Cadence identity: logo mark, lockups and voice
  - design tokens (`design/tokens.css`) for color (Tempo, Ember and Ink, in light and dark themes), type (Geist and Geist Mono), space, radius, elevation and motion
  - ten design boards, rendered to PNG: identity, color, typography, foundations, components, My work, board, issue, backlog and sprints, and flows (sign-in, command palette, mobile)
  - ADR-0016
- A new milestone, M2: Brand & design system, before projects and issues. Later milestones move up one number and one minor version.

## [0.2.0] - M1: Identity & tenancy

People can now sign up, work in organizations and invite their team, and each organization's data is isolated from every other's.

### Added

- **Accounts**:
  - registration with email confirmation, sign-in, sign-out, and password reset by email
  - a profile page: change your name, or your password (which signs out your other sessions)
  - 10-minute access tokens kept only in memory, and rotating refresh tokens in an `HttpOnly`, `SameSite=Strict` cookie. Replaying a used refresh token ends that session everywhere ([ADR-0006](docs/adr/0006-jwt-access-tokens-and-rotating-refresh-tokens.md)).
  - rate limits on sign-in, registration and password endpoints (per IP) and on the rest of the API (per user)
  - email sent in the background over SMTP (any provider), with plain-text and HTML templates
- **Organizations**:
  - create, rename and delete organizations, and switch between them
  - invitations by email that expire after 7 days, can be revoked, and work for new and existing accounts
  - member management with Owner, Admin, Member and Guest roles. Ownership rules protect the last owner.
  - tenant isolation enforced by the persistence layer, with permission checks served from an in-process cache ([ADR-0007](docs/adr/0007-tenant-isolation.md))
- **Web app**: sign-in and account pages, protected routes, an app shell with an organization switcher, sidebar, account menu and a `Ctrl K` command palette, organization, member and profile settings, and an invitation page
- **Quality**:
  - backend: 116 unit, integration and architecture tests, including tenant isolation, token rotation and reuse detection, and query budgets on every endpoint
  - frontend: 23 tests
  - Playwright end-to-end tests against the production stack in CI: sign up, confirm by email, sign in, create and switch organizations, sign out

### Changed

- The production stack needs `JWT_SIGNING_KEY`, and should have `CADENCE_DOMAIN` and the `SMTP_*` settings. See `deploy/.env.example`.
- Forms validate with `zod/mini`, which keeps each form page about 16 KB smaller.
- The bundle budget now counts every chunk a route downloads, including chunks shared with other routes.

### Fixed

- New database connections no longer try GSS (Kerberos) encryption first, which failed and logged an error in the container image every time.

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
