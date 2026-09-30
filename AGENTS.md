# AGENTS.md

Guidance for AI coding agents working on Cadence. Human contributors will find it a useful quick reference too. It complements [CONTRIBUTING.md](CONTRIBUTING.md), which remains the source of truth for workflow rules.

## Start here

| Read | For |
|---|---|
| [docs/ROADMAP.md](docs/ROADMAP.md) | Scope, milestones and progress. The checkboxes show what is done and what comes next. |
| [docs/architecture.md](docs/architecture.md) | How the system is built today |
| [docs/adr/](docs/adr/README.md) | Why it is built that way. Do not contradict an accepted ADR without writing a new one. |
| [docs/performance.md](docs/performance.md) | Performance targets, the current baseline and how to measure |

Cadence is a monorepo: an ASP.NET Core (.NET 10) modular monolith in `src/`, a React 19 + TypeScript SPA in `web/`, tests in `tests/`, load tests and scripts in `perf/`, and the self-hosted Docker Compose stack in `deploy/`.

## Hard constraints

- **Small hardware.** The whole stack must run on a 1–2 GB RAM host (amd64 or arm64) with about 10 concurrent users. Before adding a dependency, service or background process, consider its memory and startup cost. New infrastructure such as Redis or a message broker needs an ADR.
- **PostgreSQL 18** is the only database. PostgreSQL-specific features (full-text search, `SKIP LOCKED`, `xmin`) are allowed and encouraged.
- **Self-hosted Docker.** Every image must build for linux/amd64 and linux/arm64.

## Commands

Prerequisites: .NET 10 SDK, Node.js 22+, Docker (integration tests and the local stack need it).

```bash
# Run everything locally (PostgreSQL, Mailpit, migrator, API, Vite) with the Aspire dashboard
dotnet run --project src/Cadence.AppHost

# Backend
dotnet build Cadence.slnx
dotnet test                                    # all test projects (Microsoft.Testing.Platform)
dotnet test --project tests/Cadence.Domain.UnitTests
dotnet format Cadence.slnx --verify-no-changes

# Database migrations (authoring does not need a running database)
dotnet ef migrations add <Name> --project src/Cadence.Infrastructure --output-dir Persistence/Migrations

# Frontend (run in web/)
npm ci
npm run dev | lint | format:check | typecheck | test | build | budget
npm run api:generate                           # after any API contract change

# Production stack from source (plus Mailpit), then end-to-end checks
cd deploy
export POSTGRES_PASSWORD=local JWT_SIGNING_KEY=local-only-signing-key-at-least-32-characters
docker compose -f docker-compose.yml -f docker-compose.build.yml -f docker-compose.e2e.yml up -d --build
../perf/smoke-test.sh https://localhost && ../perf/measure-memory.sh 350
cd ../web && npx playwright install chromium && npm run e2e
```

## Before opening a pull request

Run what CI runs (`.github/workflows/ci.yml`) and make sure all of it passes:

1. `dotnet format --verify-no-changes`, `dotnet build -c Release` (warnings are errors), `dotnet test`
2. Building regenerates `openapi/cadence.json`. Commit it if it changed.
3. In `web/`: `npm run api:generate`, then commit any change to `src/shared/api/generated`
4. `npm run lint`, `npm run format:check`, `npm run typecheck`, `npm test`, `npm run build`, `npm run budget`
5. For container, deployment or user-journey changes: build the stack, run `perf/smoke-test.sh` and `npm run e2e`

## Backend conventions

- **Layers.** `Api → Infrastructure → Application → Domain`, with dependencies pointing inwards only. `tests/Cadence.ArchitectureTests` enforces this. Domain has no dependencies. Application must not reference Infrastructure, ASP.NET Core or Npgsql.
- **Use cases** are commands and queries under `src/Cadence.Application/Features/<Feature>/`:
  - messages are immutable `record`s named `*Query` or `*Command`
  - handlers are `sealed`
  - validators are FluentValidation `AbstractValidator<T>` classes, picked up automatically
- **Expected failures** (not found, conflict, forbidden) return `Result`/`Error` values. Exceptions are for unexpected conditions only.
- **No reflection-heavy libraries** ([ADR-0005](docs/adr/0005-no-reflection-on-hot-paths.md)):
  - use the source-generated Mediator, not MediatR
  - use Mapperly, not AutoMapper
  - add every new request and response type to `ApiJsonSerializerContext`
  - log through `[LoggerMessage]` methods
- **Endpoints** are Minimal APIs in `src/Cadence.Api/Endpoints/`, grouped per feature under `/api/v{version}`. They use `TypedResults`, `WithName(...)` (the name becomes the OpenAPI operationId and the generated hook name) and `WithSummary(...)`.
- **Data access:**
  - reads use `AsNoTracking()` and project to DTOs with `Select`
  - bulk changes use `ExecuteUpdateAsync`/`ExecuteDeleteAsync`
  - pagination is keyset-based, never `OFFSET`
  - every endpoint's integration test asserts a query budget with `QueryCounter.AssertAtMostAsync`
- **Identifiers** are UUIDv7 (`Guid.CreateVersion7()`) created by the domain ([ADR-0011](docs/adr/0011-uuidv7-primary-keys.md)).
- **Migrations** are applied only by `Cadence.Migrator`, never at API startup ([ADR-0015](docs/adr/0015-one-shot-migrator.md)). Never edit a migration that has been merged; add a new one.
- **Package versions** live only in `Directory.Packages.props`. Do not put `Version` attributes in `.csproj` files.

## Frontend conventions

- **Structure.** `src/app` is the shell, `src/features/<name>` holds one folder per feature with a public `index.ts`, and `src/shared` holds reusable code. ESLint enforces the boundaries: features never import other features or the app shell, and `shared` never imports features.
- **API access** goes only through the generated hooks in `src/shared/api/generated`. Never edit generated files; regenerate them. Never call `fetch` directly; use `httpClient`, which turns problem details into `ApiError`.
- **Styling** uses Tailwind with the semantic tokens from `src/index.css` (`bg-card`, `text-muted-foreground`, …), never raw palette colors. Reuse the components in `src/shared/ui`.
- **Forms** use react-hook-form with `zod/mini` schemas (`z.string().check(z.minLength(1, '…'))`), not the classic `zod` API, which costs about 16 KB more per route. Apply server field errors with `applyServerErrors`.
- **Performance.** Route pages are lazy-loaded. Heavy libraries must load only on the routes that use them. `npm run budget` enforces 180 KB of initial JS and 80 KB per lazy chunk (gzip).
- **Tests** use Vitest, Testing Library and MSW. Mock HTTP with `server.use(...)`; unhandled requests fail the test. Query elements by role, label or text.

## Git and pull requests

- **Never commit to `develop`, `staging` or `main`.** Work on a branch from `develop` named `feature/…`, `fix/…`, `perf/…`, `docs/…` or `chore/…`, and open a pull request into `develop`.
- **Commits** follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat(board): …`). Keep them small and focused, each one building on its own, with a body that explains *why* when that isn't obvious.
- **Pull requests** use the template in `.github/pull_request_template.md` and a Conventional Commits title. Merges use merge commits only.
- A significant architectural decision needs an ADR in `docs/adr/`. Completing roadmap scope means ticking its checkboxes in `docs/ROADMAP.md`.

## Security

- Never commit secrets. Local secrets belong in `.env` files (git-ignored) or .NET user secrets. `deploy/.env.example` documents the production settings.
- Never log SQL parameter values, tokens, passwords or personal data.
- Keep containers non-root and keep PostgreSQL on the internal Compose network.

## Known quirks

- **TypeScript is pinned to 6.0.** typescript-eslint does not support TypeScript 7 yet.
- **Vitest uses the `threads` pool**, because forked workers time out on some Windows machines.
- **Integration tests start PostgreSQL through Testcontainers**, so Docker must be running.
- **`dotnet sln add` writes CRLF line endings.** `.gitattributes` normalizes them to LF on commit.
