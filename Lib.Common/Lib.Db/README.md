# Lib.Db Modern Async Roadmap

This project replaces the legacy `bowoo.Lib` data layer with a .NET 8 asynchronous data-access platform. The modernization happens exclusively inside `Lib.Db`; the existing libraries remain untouched.

## Guiding Principles
- End-to-end `async/await` and `IAsyncDisposable`
- Polly v8 `ResiliencePipeline` for retry, timeout, circuit breaker, bulkhead, fallback, and rate limiter policies
- DI/Options/HostedService/Pipeline structure aligned with `Lib.Log`
- Strongly-typed parameters and results, including table-valued, JSON, and output parameters
- Built-in observability via OpenTelemetry and health checks

## Phase Overview
1. **Foundation**: folder layout, options/DI primitives, async interfaces
2. **Resilience**: configure Polly pipelines and resilience profiles
3. **Execution Core**: implement `DbCommandExecutor`, `DbDataSourceFactory`, modern ADO.NET async wrappers
4. **Binding & Mapping**: asynchronous parameter binding and result mapping, prepare source generators
5. **Pipeline**: compose `IAsyncQueryMiddleware` chains
6. **Observability**: add Activity/Meter instrumentation, health checks, hosted services
7. **Advanced**: batch/bulk/streaming operations, sharding/routing, testing doubles
8. **Adoption**: provide legacy-compatible facades and migrate consumers gradually

Each completed phase updates this roadmap and associated ADRs.

