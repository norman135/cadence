# 0003. PostgreSQL 18 instead of SQL Server

- Status: Accepted
- Date: 2026-09-23

## Context

The target host can be an ARM64 machine (for example an Ampere cloud VM or a Raspberry Pi-class server) or an x86-64 one, with 1–2 GB of RAM. The database must work well with EF Core, fit comfortably in a few hundred megabytes, and cover the features Cadence needs:
- full-text search (M7)
- a reliable job queue (M8)
- reporting queries (M9)

SQL Server was the initial choice, but Microsoft publishes no ARM64 Linux images for it, and its minimum memory requirement (2 GB) exceeds the whole host budget.

## Decision

Use **PostgreSQL 18** (official Alpine image) with the **Npgsql EF Core provider**.

- Npgsql is one of the most complete EF Core providers, with support for compiled models, `ExecuteUpdate`/`ExecuteDelete`, arrays, JSON, and full-text search types.
- Built-in features replace extra infrastructure:
  - `tsvector` and GIN indexes provide full-text search, so no search engine is needed.
  - `FOR UPDATE SKIP LOCKED` and `LISTEN/NOTIFY` provide a job queue, so no message broker is needed.
  - `xmin` supports optimistic concurrency, so no extra column is needed.
- It is tuned for the small host in `deploy/postgresql.conf` (96 MB shared buffers, 25 connections, JIT off).
- Identifiers use `snake_case` through EFCore.NamingConventions, the idiomatic PostgreSQL style.

## Consequences

- The stack runs unchanged on amd64 and arm64. CI verifies both.
- PostgreSQL idles at about 45 MB in the production configuration.
- Search, queueing and concurrency features become PostgreSQL-specific. This is an accepted trade-off: the database is a deliberate, long-term choice, not an interchangeable detail.
