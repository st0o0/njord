## Why

The per-domain learning cycle uses hash comparison to detect when API data actually changes between polls. The current implementation uses `DomainForecast<T>.GetHashCode()` (C# record auto-generated), which is broken in two ways:

1. **`ModelRun` = `now`**: Each client passes `DateTimeOffset.UtcNow` as the model run timestamp. This changes every poll, making the hash always different.
2. **`ImmutableArray<T>.GetHashCode()`** is reference-based (struct wrapper), so two arrays with identical content produce different hashes.

The result: every poll is classified as "Changed", the learning cycle learns the poll interval (20 min Discovery) instead of the real API update frequency, and the system polls more aggressively than needed.

## What Changes

- Add a `ContentHash()` extension method on `IDomainForecast` that computes a deterministic hash from `Domain`, `Resolution`, and all `Points` (excluding `ModelRun`). The Point records (`ForecastPoint`, `MarinePoint`, etc.) are value-type records with deterministic `GetHashCode()` -- only the container is broken.
- Change `OpenMeteoGateway` to call `ContentHash()` instead of `GetHashCode()` when building `FetchDomainCompleted`.
- Add unit tests proving the hash is stable across identical data and changes when data differs.
- Fix the `ModelRun` parameter in all domain clients: pass a meaningful timestamp (e.g. the first point's ValidFrom truncated to hour) instead of `now`, so the field has semantic value for diagnostics even though it's excluded from the content hash.

## Capabilities

### New Capabilities
- `content-hash-change-detection`: Specification for deterministic content-based change detection on domain forecasts.

### Modified Capabilities

## Non-goals

- Changing the learning cycle algorithm itself (tolerance, miss thresholds, backoff).
- Changing how often njord polls (the fix makes the cycle learn the real API frequency, which may result in longer intervals -- that's the correct behavior).
- Hashing the raw HTTP response bytes (too coupled to serialization details).

## Impact

- **Code**: `Njord.Core` (new extension), `Njord.OpenMeteo` (Gateway + 6 clients for ModelRun fix)
- **Tests**: `Njord.Core.Tests` (hash determinism), `Njord.OpenMeteo.Tests` (Gateway hash source)
- **Budget**: Positive -- once the cycle learns real API frequencies (1-3h for Forecast, ~1h for AQ/Marine, ~24h for Flood/Seasonal), njord will poll less often in Steady phase, reducing API calls significantly
