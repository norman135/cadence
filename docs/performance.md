# Performance

Cadence targets a **1–2 GB RAM server with about 10 concurrent users**, and must keep working at 5× that load. This page records how performance is measured and the results for each release. The targets themselves are defined in [ROADMAP §3](ROADMAP.md#3-performance-targets).

## Results

### v0.4.0 (M3: Projects & issues)

M3 adds the work-tracking domain: projects, issues, labels, links and comments, with the list and detail views. It is the first release measured with **authenticated traffic over real data**. The development seeder creates an organization with 3 projects, 9 labels, 6 people and 10,000 issues (39 s), and a new k6 scenario (`perf/k6/issues.js`) signs in and works through it. Each iteration opens a filtered or sorted list, sometimes the next page, then an issue from it.

**Environment:** the same as v0.2.0, with the seeded data.

| Metric | Target | Measured | Change from v0.3.0 | |
|---|---|---|---|---|
| Issue list p95, 10 users, 10,000 issues | ≤ 100 ms | **21.5 ms** | new | ✅ |
| Issue detail p95, 10 users | ≤ 100 ms | **19.6 ms** | new | ✅ |
| Issue list p95, 50 users | ≤ 300 ms | **38.9 ms** | new | ✅ |
| Issue detail p95, 50 users | ≤ 300 ms | **32.9 ms** | new | ✅ |
| Error rate, 10 and 50 users | 0% | **0%** | — | ✅ |
| DB queries per issue list request | ≤ 2 | **2** (asserted by an integration test) | new | ✅ |
| Whole stack at idle (seeded) | ≤ 450 MB | **120 MiB** (app 66, PostgreSQL 38, Caddy 16) | +17 MiB | ✅ |
| App container memory under 10-user load | ≤ 200 MB | **151 MiB** (stack 281 MiB) | +73 MiB | ✅ |
| App container memory after 50-user load | — | **128 MiB** (stack 265 MiB) | — | ✅ |
| Initial JavaScript (gzip) | ≤ 180 KB | **129.6 KB** | +2.7 KB | ✅ |
| Initial CSS (gzip) | ≤ 30 KB | **10.2 KB** | +1.2 KB | ✅ |
| Largest lazy route (gzip, all chunks it downloads) | ≤ 80 KB | **64.2 KB** (members page); issue page 59.3, issue list 56.8 | +0.8 KB | ✅ |
| EF Core model build (once per process) | — | **97 ms, 7.7 MB** allocated | new | — |

**Query plans.** Every list sort is served by an index that ends in `id`, so a keyset page reads only the rows it returns:
- the default sort (recently updated, whole organization) is an index-only scan of `ix_issues_organization_id_updated_at_id`, 0.38 ms
- My work (open issues assigned to me) uses the partial index `ix_issues_assignee_open`, 1.85 ms

Organization owners and admins see every project, so their queries carry no project filter. Members' queries filter on the project ids from the cached project roles.

**Where the memory went.** The app grew from 78 to 151 MiB under load. That is EF Core's query and model caches for the new query shapes, the project role cache, and pages of 50 issues serialized per request. After the 50-user run it settles at 128 MiB, so it doesn't grow with users. Both stay well under the container's 320 MiB limit.

**Bundles.** The issue view, marked and DOMPurify load only when an issue opens, so the list pages download 57 KB rather than 99 KB. The Markdown renderer is fetched on first use and shows the plain text until it arrives.

**Compiled model.** `dotnet ef dbcontext optimize` rejects named query filters, which tenant isolation relies on. Building the model costs 97 ms once per process, so the compiled model isn't adopted ([ADR-0017](adr/0017-projects-issues-and-access.md)).

### v0.3.0 (M2: Brand & design system)

M2 changes the frontend only: the design tokens, self-hosted fonts, a theme script, a rebuilt app shell and the restyled components. The server is unchanged, so its numbers move only within run-to-run noise.

**Environment:** the same as v0.2.0.

| Metric | Target | Measured | Change from v0.2.0 | |
|---|---|---|---|---|
| Whole stack at idle | ≤ 450 MB | **103 MiB** (app 62, PostgreSQL 25, Caddy 16) | −14 MiB (noise) | ✅ |
| App container memory under 10-user load | ≤ 200 MB | **78 MiB** | +3 MiB | ✅ |
| API p95, 10 users | ≤ 100 ms | **3.5 ms** | −0.6 ms | ✅ |
| Page (index.html) p95, 10 users | ≤ 100 ms | **4.6 ms** | −2.3 ms | ✅ |
| API p95, 50 users | ≤ 300 ms | **3.2 ms** | −1.5 ms | ✅ |
| Error rate, 10 and 50 users | 0% | **0%** | — | ✅ |
| Initial JavaScript (gzip) | ≤ 180 KB | **126.9 KB** | +2.7 KB | ✅ |
| Initial CSS (gzip) | ≤ 30 KB | **9.0 KB** | +2.6 KB | ✅ |
| Fonts (Latin, WOFF2) | ≤ 60 KB | **51.3 KB** (Geist 29.4 + Geist Mono 22.6) | new | ✅ |
| Largest lazy route (gzip, all chunks it downloads) | ≤ 80 KB | **63.4 KB** (members page) | +0.5 KB | ✅ |

**Where the bytes went.** The initial bundle gained the theme module, the new logo and wordmark, and the account layout (+2.7 KB). Two things were deliberately kept out of it:
- the tooltip provider, which would have pulled in Radix's positioning code (about 12 KB) and now lives in the app shell's chunk
- the sign-in brand panel, a lazy 3.3 KB chunk

The CSS grew with the token set and the new components, and is still under a third of its budget.

**Fonts.** Geist is preloaded, so text renders in the brand font on the first paint. Geist Mono loads when monospace text first appears. Metric-matched fallbacks (Arial at 104.76% size-adjust, Courier New) keep line breaks stable if a font arrives late.

### v0.2.0 (M1: Identity & tenancy)

M1 adds ASP.NET Core Identity, JWT validation, rate limiting, the HybridCache membership cache, a background email dispatcher, and the signed-in web app. The load test is still the anonymous baseline from M0, so latency figures are comparable between releases. Authenticated journeys join the load test in M3, once there are projects and issues to read.

**Environment:** the same as v0.1.0. The stack runs with `docker-compose.e2e.yml`, which adds Mailpit (left out of the memory totals) and lifts per-IP rate limits, because every k6 user shares one IP.

| Metric | Target | Measured | Change from v0.1.0 | |
|---|---|---|---|---|
| Whole stack at idle | ≤ 450 MB | **117 MiB** (app 51, PostgreSQL 49, Caddy 17) | +14 MiB | ✅ |
| App container memory under 10-user load | ≤ 200 MB | **75 MiB** | +14 MiB | ✅ |
| App container memory under 50-user load | — | **77 MiB** | +15 MiB | ✅ |
| API p95, 10 users | ≤ 100 ms | **4.1 ms** | +1.0 ms | ✅ |
| Page (index.html) p95, 10 users | ≤ 100 ms | **6.9 ms** | +2.6 ms | ✅ |
| API p95, 50 users | ≤ 300 ms | **4.7 ms** | −0.9 ms | ✅ |
| Error rate, 10 and 50 users | 0% | **0%** (1,200 and 6,000 requests) | — | ✅ |
| Cold start to ready | ≤ 5 s | **1.6–2.0 s** (three runs, container start to `/health/ready` through Caddy) | +0.5–0.9 s | ✅ |
| Initial JavaScript (gzip) | ≤ 180 KB | **124.2 KB** | +10.8 KB | ✅ |
| Initial CSS (gzip) | — (budget 30 KB) | **5.1 KB** | +1.2 KB | ✅ |
| Largest lazy route (gzip, all chunks it downloads) | ≤ 80 KB | **62.9 KB** (members page) | new method | ✅ |
| DB queries for a permission check | 0 | **0** (asserted by an integration test) | new | ✅ |

**Where the memory went.** The idle app grew from 45 to 51 MiB, and from 61 to 75 MiB under load: Identity, the JWT handler, the rate limiter partitions and HybridCache are all resident now. At 50 users the app uses only 2 MiB more than at 10, so memory does not grow with the number of users at this scale.

**Bundles.** The initial bundle gained the session and refresh logic (+10.8 KB). Everything else loads per page. The heaviest route, members, downloads 62.9 KB, including chunks it shares with other pages. Before switching forms to `zod/mini` it was 79.3 KB, just under its 80 KB budget.

**Found while measuring:**

- Npgsql tried GSS (Kerberos) encryption on every new connection, which failed in the chiseled image with an error on stderr. It is now disabled by default.
- The anonymous k6 baseline ran into the new per-IP API limit (30% `429`s at 50 users), so the test stack lifts that limit. Authenticated traffic in production is limited per user.

### v0.1.0 (M0: Foundation)

> M0 has no features yet, so these numbers are a **baseline for the platform itself**: the proxy, runtime, pipeline, database connectivity and SPA hosting. Later milestones add real workloads, and the numbers will be re-measured against the same targets.

**Environment:** Docker Desktop on Windows 11 (x86-64, 8 vCPU, 7.6 GB assigned to Docker), production Compose stack built from source, requests through Caddy with TLS. CI repeats the idle memory check and the 10-user load test on GitHub's amd64 and arm64 runners for every pull request.

| Metric | Target | Measured | |
|---|---|---|---|
| Whole stack at idle | ≤ 450 MB (M0 gate: ≤ 350 MB) | **103 MiB** (app 45, PostgreSQL 45, Caddy 12) | ✅ |
| App container memory under 10-user load | ≤ 200 MB | **61 MiB** | ✅ |
| App container memory under 50-user load | — | **62 MiB** | ✅ |
| API p95, 10 users | ≤ 100 ms | **3.1 ms** | ✅ |
| Page (index.html) p95, 10 users | ≤ 100 ms | **4.3 ms** | ✅ |
| API p95, 50 users | ≤ 300 ms | **5.6 ms** | ✅ |
| Error rate, 10 and 50 users | 0% | **0%** (1,200 and 6,000 requests) | ✅ |
| Cold start to ready | ≤ 5 s | **1.1 s** (three runs, including container start) | ✅ |
| Initial JavaScript (gzip) | ≤ 180 KB | **113.4 KB** | ✅ |
| Initial CSS (gzip) | — (budget 30 KB) | **3.9 KB** | ✅ |
| Largest lazy route chunk (gzip) | ≤ 80 KB | **4.6 KB** (home) | ✅ |
| DB queries for `GET /api/v1/system/info` | ≤ 3 | **0** (asserted by an integration test) | ✅ |
| Container image size | — | 255 MB (uncompressed, includes ReadyToRun code) | ℹ️ |

The initial JavaScript is dominated by React, React DOM, React Router and TanStack Query. It sets the floor that every later feature builds on, and is why feature pages are lazy-loaded.

#### Micro-benchmark: JSON serialization

`JsonSerializationBenchmarks` serializes `SystemInfoResponse` with reflection-based options and with the source-generated context (BenchmarkDotNet, ShortRun job):

| Method | Mean | Allocated |
|---|---|---|
| Reflection | 529 ns | 232 B |
| Source-generated | 494 ns | 232 B |

The steady-state difference for a small DTO is within noise. The source-generated context's benefit is avoiding reflection warm-up at startup and being trimming/AOT-ready ([ADR-0005](adr/0005-no-reflection-on-hot-paths.md)), not faster per-call serialization.

## How to measure

### Load test and memory

```bash
# 1. Start the production stack from source
cd deploy
export POSTGRES_PASSWORD=local-test JWT_SIGNING_KEY=local-test-signing-key-at-least-32-characters
# The e2e override adds Mailpit and lifts per-IP rate limits: all k6 users share one IP.
docker compose -f docker-compose.yml -f docker-compose.build.yml -f docker-compose.e2e.yml up -d --build

# 2. Smoke test and idle memory
../perf/smoke-test.sh https://localhost
../perf/measure-memory.sh 350

# 3. Load: 10 users (design load), then 50 users (headroom)
docker run --rm --network host -v "$PWD/../perf/k6:/scripts:ro" grafana/k6 run -e VUS=10 /scripts/baseline.js
docker run --rm --network host -v "$PWD/../perf/k6:/scripts:ro" grafana/k6 run -e VUS=50 /scripts/baseline.js
../perf/measure-memory.sh 450

# 4. Issues (M3 and later): seed 10,000 issues, then sign in and work through them
docker compose run --rm migrate seed --issues 10000 --password <password>
docker run --rm --network host -v "$PWD/../perf/k6:/scripts:ro" grafana/k6 run -e SEED_PASSWORD=<password> -e VUS=10 /scripts/issues.js
```

### Bundle budgets

```bash
cd web
npm run build && npm run budget
```

### Micro-benchmarks

```bash
dotnet run -c Release --project tests/Cadence.Benchmarks -- --filter "*"
```

### Query budgets

Integration tests wrap requests in `QueryCounter.AssertAtMostAsync(n, ...)`, which fails when an endpoint executes more database commands than its budget. This turns N+1 regressions into test failures.

## Tools

| Tool | Used for |
|---|---|
| k6 (`perf/k6`) | HTTP load with latency and error thresholds |
| `perf/measure-memory.sh` | Per-container and total memory against a budget |
| `perf/smoke-test.sh` | End-to-end checks of a running deployment |
| `web/scripts/check-bundle-budget.mjs` | Gzip size of initial and lazy chunks |
| BenchmarkDotNet (`tests/Cadence.Benchmarks`) | Micro-benchmarks of hot paths |
| `SlowQueryInterceptor` | Logs database commands slower than 50 ms |
| `pg_stat_statements` | Top queries by total time in production |
| `dotnet-counters`, `dotnet-gcdump` | Runtime and GC profiling (M10 deep dive) |
