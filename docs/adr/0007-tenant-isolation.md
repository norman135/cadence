# 0007. Tenant isolation with query filters and cached memberships

- Status: Accepted
- Date: 2026-09-27

## Context

Cadence is multi-tenant: every project, issue and member belongs to exactly one organization, and one database serves all of them. A single missing `WHERE organization_id = …` would leak one customer's data to another, the worst bug a multi-tenant product can have. Isolation therefore can't depend on every handler remembering a filter.

Nearly every request also needs to know the caller's role in the organization. On a small host that lookup must not cost a query per request.

## Decision

**One database, shared schema, `organization_id` on every tenant-owned row.** It is the cheapest model to run and fits the target scale. Separate schemas or databases would multiply connections and migrations for no benefit here.

**Tenant resolution per request.** Organization routes have the shape `/api/v1/organizations/{organizationId}/…`. `TenantResolutionMiddleware` looks up the caller's membership and binds the request to that organization (`ITenantContext`). Non-members get the same `404` as a missing organization, so outsiders can't even confirm an organization exists.

**Memberships are cached in-process** with HybridCache: 10-minute entries, tagged per organization, with stampede protection. Every change (role change, removal, joining, deletion) invalidates the entry immediately, so a removed member loses access on their next request. Permission checks (`.RequirePermission(...)`) read the resolved membership: **0 database queries on the hot path**, which an integration test enforces.

**Isolation is enforced by the persistence layer, not by handlers:**

- Every `ITenantScoped` entity gets a named EF Core query filter (`Tenant`) that restricts it to the resolved organization. Without a resolved organization the filter matches nothing, so a missing tenant **fails closed**.
- Queries that legitimately span tenants, such as "my organizations" or looking up an invitation by its token, must opt out explicitly with `IgnoreQueryFilters([QueryFilters.Tenant])`, which is easy to spot in review.
- `SaveChanges` refuses to add, modify or delete a tenant-scoped entity of another organization while a tenant is resolved. This is defense in depth against handler bugs.
- DbContexts come from a pool. The request-scoped instance is bound to the request's `ITenantContext`, and reads it lazily on each query, so it can be created before tenant resolution runs.

**Composite indexes lead with `organization_id`**, for example unique `(organization_id, user_id)` on memberships and a partial `(organization_id, email)` index on pending invitations. Every filtered query narrows to one organization first.

**Permissions, not roles, are checked.** One matrix in the domain (`Permissions.For(role)`) maps roles to permissions. Ownership rules (only owners manage owners; the last owner can't leave or step down) live on the `Membership` entity.

## Consequences

- A new tenant-scoped entity is isolated just by implementing `ITenantScoped`. There is nothing to remember in handlers.
- Integration tests prove that two organizations can't see or modify each other's data, that the save guard throws, and that cached permission checks make no queries.
- A role change takes effect on the next request, because invalidation is explicit. The 10-minute expiry is only a safety net.
- If Cadence ever runs several app instances, the cache needs a shared invalidation channel (Redis as the HybridCache L2 plus backplane, as described in ROADMAP §9.6).
- Row-level security in PostgreSQL would add a database-enforced layer on top. It is deliberately left out for now; the EF filters and save guard give strong isolation without extra connection-state management.
