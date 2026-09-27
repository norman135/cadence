# 0005. Minimal APIs and source generators instead of reflection

- Status: Accepted
- Date: 2026-09-23

## Context

Popular .NET building blocks rely on runtime reflection:
- MVC controllers
- MediatR's handler resolution
- AutoMapper's mapping plans
- reflection-based JSON serialization
- `ILogger` extension methods with boxing

Reflection costs startup time, memory for cached metadata, and allocations per call. It also blocks trimming and ahead-of-time compilation. In 2025, MediatR and AutoMapper also moved to commercial licensing.

## Decision

Prefer compile-time code generation on every hot path:

| Concern | Choice | Instead of |
|---|---|---|
| HTTP endpoints | Minimal APIs with `TypedResults` | MVC controllers |
| Request dispatch | [Mediator](https://github.com/martinothamar/Mediator) (source-generated) | MediatR |
| Object mapping | [Mapperly](https://mapperly.riok.app/) (source-generated, from M1) | AutoMapper |
| JSON | `System.Text.Json` source-generated context | Reflection-based serialization |
| Logging | `[LoggerMessage]` source-generated methods | `logger.LogInformation(...)` |

A BenchmarkDotNet project (`tests/Cadence.Benchmarks`) keeps these choices measurable.

## Consequences

- No handler scanning at startup, and fewer allocations per request.
- Handlers and pipeline behaviors are known at compile time. A missing registration is a build error, not a runtime failure.
- Each new API contract type must be added to `ApiJsonSerializerContext`. The reflection resolver remains as a fallback for framework types.
- The first benchmark shows steady-state serialization of a small DTO is roughly equal for both approaches (see `docs/performance.md`). The gains are in startup, warm-up and trimming-readiness, not per-call speed on small payloads. This is recorded honestly rather than overstated.
