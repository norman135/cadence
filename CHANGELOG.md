# Changelog

All notable changes to Cadence are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project uses [Semantic Versioning](https://semver.org/). Each release corresponds to a milestone in the [roadmap](docs/ROADMAP.md).

## [0.5.0] - M4: Workflows & Kanban board

Each project gets its own workflow, and its issues move across a drag-and-drop board.

### Added

- **Workflows** ([ADR-0017](docs/adr/0017-projects-issues-and-access.md)):
  - statuses per project in board order, each counting as one of six categories, with optional WIP limits and a default status
  - optional transition rules: which moves are allowed, whether they need an assignee, and who may make them
  - a workflow editor in project settings; removed statuses send their issues where you choose
- **Kanban board** (`/projects/:key/board`):
  - drag and drop with the mouse or the keyboard, with optimistic moves that roll back if refused
  - swimlanes by assignee, priority or epic, and the list's quick filters
  - WIP limits in column headers, a "+" per column, and a warning when a drop goes over a limit
  - List and Board views on every project, also in the sidebar
- **Ordering**: fractional-index ranks, so a move rewrites one row, and a background job that keeps the keys short ([ADR-0010](docs/adr/0010-fractional-indexing-for-ordering.md))
- **Concurrency**: issues carry a row version; a move or edit that lost a race answers 409, and the board reloads with a note
- **Performance tooling**: a k6 board scenario, and a script that measures drag smoothness on a 500-card board

### Changed

- Status pickers list the project's own statuses, and disable moves a restricted workflow doesn't allow.
- The API accepts status ids; status categories still work and pick the first status in that category.

### Fixed

- Labels of switches and checkboxes show the pointer cursor.
- A token expiring more than 24 days out no longer causes a refresh loop.

### Performance

Over 10,000 issues, a card move persists with p95 16 ms at 10 users (target 50 ms), the board loads with p95 84 ms, and a 500-card board drags at 59 fps. See [docs/performance.md](docs/performance.md#v050-m4-workflows--kanban-board).

## [0.4.0] - M3: Projects & issues

Teams can now track work: projects hold issues with readable keys, and issues can be filtered, discussed, nested and linked.

### Added

- **Projects**:
  - create, rename, describe, archive and restore projects
  - a key per project (`ATL`), suggested from the name, unique in the organization and fixed once created
  - project members with Viewer, Member and Admin roles; organization owners and admins can always reach every project ([ADR-0017](docs/adr/0017-projects-issues-and-access.md))
- **Issues**:
  - readable keys (`ATL-142`) that stay consecutive under concurrent creates
  - type, title, Markdown description, status, priority, assignee, reporter, labels, estimate and due date
  - sub-issues up to three levels deep, and links: blocks, relates to and duplicates
  - comments in Markdown, with edit history
  - labels shared across the organization's projects
- **Web app**, built to design boards 06 and 08:
  - a fast issue list: virtualized, loads more as you scroll, with filters, search, sort and columns, all kept in the URL
  - the issue view, as a page or a drawer over the list, with inline editing and one-click property pickers
  - My work fills in with your open issues, most urgent first
  - projects in the sidebar with open counts, a New issue button (or press `c`), and project settings
  - the command palette finds issues by title or key, and opens projects
  - browser tabs are titled after the page ("ATL-142 · Acme")
- **Performance tooling**:
  - a development seeder (`Cadence.Migrator seed --issues 10000`) that builds a realistic organization
  - a k6 scenario that signs in and works through lists and issues
  - a benchmark of EF Core model building
- **Quality**:
  - backend: 185 tests, including keyset paging in every sort, 20 concurrent creates and a two-query budget on the list
  - frontend: 45 tests, visual baselines for the issue list and issue page in both themes, and an end-to-end journey from a new project to a comment

### Changed

- The API's OpenAPI document describes integers as plain `integer` and issue sorts as strings; the generated client's types are narrower to match.
- The light theme's warning and danger colors are darker, so text in them meets WCAG AA.

### Fixed

- Buttons, menu items, tabs, switches, checkboxes and command palette results now show the pointer cursor, like links. Disabled controls keep the arrow.

### Performance

Over 10,000 issues, the list's p95 is 21.5 ms and an issue's 19.6 ms at 10 users (target 100 ms), with no errors at 50 users. See [docs/performance.md](docs/performance.md#v040-m3-projects--issues).

## [0.3.0] - M2: Brand & design system

Cadence gets its own identity, and the whole app is rebuilt to its design system, in light and dark.

### Added

- **Brand and design system** in `design/`:
  - the Cadence identity: logo mark, lockups and voice
  - design tokens (`design/tokens.css`) for color (Tempo, Ember and Ink, in light and dark themes), type (Geist and Geist Mono), space, radius, elevation and motion
  - ten design boards, rendered to PNG: identity, color, typography, foundations, components, My work, board, issue, backlog and sprints, and flows (sign-in, command palette, mobile)
  - ADR-0016
- **The app, rebuilt to the designs:**
  - the new logo, favicon, installable app icons and a web manifest
  - Geist and Geist Mono, self-hosted and preloaded (51 KB), with fallbacks that keep the layout still while they load
  - a light, dark or system theme, chosen from the account menu, the profile page or the command palette, applied before the first paint and synced across tabs
  - a split sign-in layout with the brand panel, a new app shell (sidebar, floating page panel, top bar with search), and My work as the starting page
  - a phone layout: a navigation drawer and a bottom tab bar below 768 px
  - the component kit restyled, plus tabs, tooltips, checkboxes, switches, keyboard keys, themed toasts, and workflow status and priority glyphs
  - a style guide page in development builds, showing every component in both themes
- **Quality:**
  - visual regression tests: screenshots of the key pages in both themes, compared in CI with baselines rendered in a pinned Linux container
  - axe accessibility scans of the same pages, failing on any serious or critical WCAG 2.2 AA issue
- A new milestone, M2: Brand & design system, before projects and issues. Later milestones move up one number and one minor version.

### Changed

- The sign-in page's heading is now "Welcome back", and the organization home is now My work.
- Button variant `outline` is now `secondary`, and badges use neutral and soft signal variants.

### Fixed

- Nine color-contrast failures that axe found, some of them in the designs themselves: subtle gray is no longer used for readable text, the organization tile uses dark ink, and three avatar colors are deeper.

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
