## Context

All OpenMeteo clients call `WeatherClient.FetchCoreAsync` — a static method that does HTTP GET + status check + JSON parse + error swallowing. It couples all clients to WeatherClient and has zero resilience.

## Goals / Non-Goals

**Goals:** Self-contained clients with resilience from DI pipeline.
**Non-Goals:** Custom retry policies, per-domain tuning.

## Decisions

### D1: Resilience at HttpClient registration, not in client code
`AddStandardResilienceHandler()` adds retry, circuit breaker, and timeout to the `HttpMessageHandler` pipeline. Client code doesn't see it — it just calls `GetFromJsonAsync` and lets the pipeline handle transient failures.

### D2: Only catch 400 Bad Request
A 400 means invalid coordinates or parameters — that's a permanent error, not transient. Return null. All other errors (including 5xx after retry exhaustion) propagate as exceptions to the Gateway, which reports them as fetch failures.

### D3: No shared base class or helper
Each client is self-contained: builds URL, calls HTTP, returns typed result. No shared `FetchCoreAsync`, no inheritance, no static helpers.
