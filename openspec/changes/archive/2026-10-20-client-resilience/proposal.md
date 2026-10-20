## Why

The OpenMeteo clients share a `FetchCoreAsync` static method on `WeatherClient` that AirQuality and Flood also call — cross-domain coupling. It silently swallows all errors (returns null), has no retry/circuit-breaker, and discards error details. With `Microsoft.Extensions.Http.Resilience`, transient failures get automatic retry with exponential backoff, circuit breaking, and timeouts — all configured at the HttpClient registration level, not in client code.

## What Changes

- **Delete** `FetchCoreAsync` static method from `WeatherClient`.
- **Delete** `LogBadRequestAsync` (doesn't actually log).
- Each client becomes ~15 lines: `http.GetFromJsonAsync<T>(url, typeInfo, ct)`. Only catch `HttpRequestException` with `StatusCode == 400` (invalid input, not transient).
- **Add** `Microsoft.Extensions.Http.Resilience` NuGet package.
- **Configure** `.AddStandardResilienceHandler()` on each HttpClient registration in `OpenMeteoSetupContainer`. This provides: retry (3 attempts, exponential backoff), circuit breaker, request timeout (30s), total timeout (2min).
- Update Gateway to handle exceptions from failed fetches (non-transient after retry exhaustion).
- Update FakeClients — they stay simple, no resilience needed.

## Non-goals

- Custom resilience policies (standard defaults are appropriate).
- Per-domain resilience tuning.
- API budget impact: zero (retry on transient errors only).

## Capabilities

### New Capabilities

- `http-resilience`: Automatic retry, circuit breaker, and timeout for all Open-Meteo API calls.

### Modified Capabilities

_(none — internal implementation change)_

## Impact

- **Njord.OpenMeteo**: All 4 clients simplified, FetchCoreAsync deleted, setup container adds resilience.
- **NuGet**: New dependency `Microsoft.Extensions.Http.Resilience`.
