# 0014. Commit the OpenAPI document and the generated client

- Status: Accepted
- Date: 2026-09-23

## Context

The React app talks to the API over HTTP. Hand-written fetch calls and TypeScript types drift from the server silently, and the mismatch only shows up at runtime. Generating the client during every build hides API changes from code review, and makes the frontend build depend on building the backend first.

## Decision

- The API generates its OpenAPI 3.1 document **at build time** (`Microsoft.Extensions.ApiDescription.Server`) into `openapi/cadence.json`, which is committed.
- The frontend generates its typed client and TanStack Query hooks from that file with **Orval** (`npm run api:generate`) into `web/src/shared/api/generated`, which is also committed.
- CI fails if either artifact differs from what the current code produces:
  - the backend job checks `openapi/`
  - the frontend job regenerates the client and checks `git diff`
- All generated calls go through one hand-written fetch wrapper (`http-client.ts`). It turns problem details responses into a typed `ApiError`.

## Consequences

- Every API contract change is visible in the pull request diff as a spec change plus a client change. Breaking changes are hard to miss.
- The frontend builds independently of the .NET SDK, as in the container image's Node stage.
- Contributors must run the generator after changing the API. CI enforces it with a clear error message.
