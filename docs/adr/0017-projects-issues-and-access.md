# 0017. Projects, issues and access

- Status: Accepted
- Date: 2026-10-03

## Context

M3 adds the core of the product: projects, issues, comments, labels and links. Several choices in this domain are hard to change once data exists:

- **Who can see what.** Organization roles (ADR-0007) are too coarse. A contractor may need one project and nothing else, but organization owners and admins must never be locked out of their own data.
- **Readable keys** like `ATL-142` are what people type, link and say aloud. Numbers must never repeat or skip under concurrent creates, and the key appears in URLs and in other systems.
- **Lists are the hot path.** On a 1–2 GB host, the list endpoint must stay at two queries and use indexes for every sort, even with 10,000 issues.
- **The compiled EF Core model** was planned for M3, once the model had content (moved from M0).

## Decision

**Project access is organization role plus project membership.**

- Organization owners and admins are implicit admins of every project. Everyone else needs a `project_members` row with the role Viewer, Member or Admin.
- `ProjectAccess.EffectiveRole` combines the two in the domain; `CanRead`, `CanWrite` and `CanManage` follow from it. A project always keeps at least one explicit admin.
- `ProjectAccessCache` (HybridCache) holds each user's project roles per organization. It shares the `org:{id}` tag with the membership cache, and adds an `org-user:{org}:{user}` tag, so a role change or a removed member takes effect on the next request.
- `ProjectGate` answers "what is my role in this project" and "which projects can I see". For implicit admins it returns "all", so their list queries carry no project filter at all.
- Inaccessible projects and issues return `404`, exactly like missing ones, as with organizations.

**Issue keys come from a counter on the project row.**

- A project key is 2–10 uppercase letters or digits, unique per organization, and **immutable**, because keys are copied into commits, chats and other tools. Renaming a project keeps its key.
- Creating an issue runs `UPDATE projects SET last_issue_number = last_issue_number + 1 … RETURNING last_issue_number` in the same transaction as the insert. The row lock serializes creates within one project only, and a unique index on `(project_id, number)` backs it up. A test creates 20 issues concurrently and expects keys 1–20.
- The API looks issues up by key at `/issues/by-key/{key}` and uses ids everywhere else, so a key route can't collide with an id route.

**Counts are denormalized and recomputed, not incremented.**

- Projects store their open issue count; issues store sub-issue, closed sub-issue and comment counts. Lists show them without joins.
- After a change, a single `ExecuteUpdate` recomputes the affected counters from correlated subqueries. Recomputing is idempotent, so a retried or concurrent change can't leave a counter drifting the way `+1`/`-1` updates can.

**Lists use keyset pagination for every sort.**

- Each sort (updated, created, priority, due date) has an index ending in `id`, and the cursor is an opaque, URL-safe encoding of the sort value and the id.
- A partial index on open assigned issues (`status < done`) serves My work.
- Filters combine freely. Text search matches titles, or an exact key such as `ATL-142`. Full-text search arrives in M7.

**Statuses are fixed categories until M4.** Backlog, Todo, In progress, In review, Done and Canceled are stored as an ordered enum. M4's custom workflows will map their states onto these categories, so filters and counters keep working.

**Labels belong to the organization**, not to a project, so the same label can be used across projects. Names are unique per organization, ignoring case.

**The compiled EF Core model is not adopted.** `dotnet ef dbcontext optimize` refuses models with named query filters, which tenant isolation depends on (ADR-0007). A BenchmarkDotNet run puts building the model at about 97 ms and 7.7 MB, once per process, while the pooled contexts reuse it afterwards. That cost doesn't justify giving up named filters. The benchmark (`tests/Cadence.Benchmarks`) stays, to revisit the decision as the model grows.

## Consequences

- Authorization stays at zero database queries on the hot path: one cache read for the organization and one for project roles.
- A project's key can't be fixed after a typo; the project has to be recreated. Its name can change freely.
- Issue creates in one project are serialized by a row lock. At the target load (10 users) that's negligible, and creates in different projects don't contend.
- Counters cost one extra statement per change, in exchange for exact values and no drift.
- Custom workflows (M4) must map every state to one of the six categories.
- Startup includes building the EF model (about 0.1 s), and the API's cold start remains well inside its budget.
