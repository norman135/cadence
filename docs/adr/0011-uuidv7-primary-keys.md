# 0011. UUIDv7 primary keys

- Status: Accepted
- Date: 2026-09-23

## Context

Entities need identifiers that are:
- unique across tenants
- safe to expose in URLs
- generated without a database round-trip, so aggregates have identity before they are saved and domain events can reference them

Random UUIDv4 values meet those needs but insert at random positions in B-tree indexes. That causes page splits, index bloat and poor cache locality as tables grow. Sequential integers are index-friendly, but they leak record counts and need a round-trip to be generated.

## Decision

Primary keys are `Guid` values generated as **UUIDv7** (`Guid.CreateVersion7()`, .NET 9+) by the domain when an entity is created. UUIDv7 starts with a millisecond timestamp, so new keys are roughly ordered and append to the end of indexes, like sequential integers.

Human-friendly identifiers such as issue keys (`CAD-142`) are separate, per-project sequence numbers (M3), not primary keys.

## Consequences

- Inserts stay index-friendly, and identity exists before persistence.
- Keys reveal their approximate creation time. This is acceptable for Cadence's data.
- Keys are 16 bytes instead of 4 or 8. This is an accepted cost for tenant-safe, round-trip-free identity.
