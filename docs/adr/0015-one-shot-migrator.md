# 0015. Apply migrations in a one-shot migrator process

- Status: Accepted
- Date: 2026-09-23

## Context

EF Core migrations must be applied before a new release serves traffic. Common options:

1. **Migrate at API startup.** Simple, but every replica races to migrate. Startup slows down, the app needs schema-altering privileges, and a failed migration leaves a half-started app.
2. **EF migration bundle.** A self-contained executable, but it must be built for each target runtime (linux-x64 and linux-arm64). That complicates the cross-compiled multi-arch image.
3. **A small console app** that references the same DbContext and calls `MigrateAsync`.

## Decision

`Cadence.Migrator` is a console app published into the application image at `/app/migrator`. The same image serves two roles:

- In Docker Compose, the `migrate` service overrides the entrypoint to run the migrator. `app` starts only after it exits successfully (`service_completed_successfully`).
- In development, Aspire runs it as a resource the API waits for (`WaitForCompletion`).

It waits for the database to accept connections, logs the migrations it applies, and exits with 0 (up to date) or 1 (failure).

## Consequences

- The API never alters the schema. It can start fast and later run with least-privilege credentials.
- A failed migration stops the deployment before any new code serves traffic.
- One image to build, sign and version. The migrator adds a few megabytes of duplicated dependencies.
- The migrator is also the natural home for the demo data seeder (M9).
