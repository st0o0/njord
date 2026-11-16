## Context

The learning cycle compares hashes between consecutive polls to detect API data changes. The hash flows: Gateway builds `FetchDomainCompleted(hash)` -> PollWorker compares with `LastHash` -> records Changed or Unchanged -> state machine transitions.

Currently: `ok.Forecast.GetHashCode()` on a `DomainForecast<T>` record. Broken because `ModelRun` (set to `now`) and `ImmutableArray` (reference-based hash) make every hash unique.

## Goals / Non-Goals

**Goals:**
- Content hash that is stable for identical API data
- Learning cycle learns the real API update frequency (1-3h for Forecast, ~1h for AQ/Marine, ~24h for Flood/Seasonal)
- ModelRun has diagnostic meaning (data origin, not fetch time)

**Non-Goals:**
- Changing the learning algorithm
- Hashing raw HTTP bytes

## Decisions

### 1. ContentHash extension on IDomainForecast

A `ContentHash()` extension method in `Njord.Core/Forecast/DomainForecastExtensions.cs`:

```csharp
public static int ContentHash(this IDomainForecast forecast, ImmutableArray<T> points)
```

Since `IDomainForecast` doesn't expose `Points` (it's generic), the extension needs access to the typed points. Two options:

**Option A: Generic extension with explicit points parameter.** The Gateway already has access to the `FetchSuccess` which contains the typed `DomainForecast<T>`. Add a `ContentHash<T>()` method on `DomainForecast<T>` directly.

**Option B: Non-generic via reflection.** Too fragile.

**Decision: Option A** -- a `ContentHash()` instance method or extension on `DomainForecast<T>`:

```csharp
public int ContentHash()
{
    var hash = new HashCode();
    hash.Add(Domain);
    hash.Add(Resolution);
    foreach (var p in Points) hash.Add(p);
    return hash.ToHashCode();
}
```

This works because each Point record (`ForecastPoint`, `MarinePoint`, etc.) is a `sealed record` whose auto-generated `GetHashCode()` hashes all value-type fields deterministically.

**Placement:** Instance method on `DomainForecast<T>` (not an extension) since it accesses `Points` directly. Alternatively, an extension in a static class next to the record.

**Rationale over GetHashCode override:** Overriding `GetHashCode()` on a record changes equality semantics, which could break dictionary lookups or other code that relies on record equality. A separate method is explicit about its purpose.

### 2. Gateway change

In `OpenMeteoGateway`, line 40:
```csharp
// Before:
{ Result: FetchSuccess ok } => new FetchDomainCompleted(location, domain, ok.Forecast.GetHashCode()),
// After:
{ Result: FetchSuccess ok } => new FetchDomainCompleted(location, domain, ok.Forecast.ContentHash()),
```

But `ok.Forecast` is `IDomainForecast` (the interface), not `DomainForecast<T>`. We need to get the typed forecast. Looking at `FetchSuccess`:

```csharp
public sealed record FetchSuccess(IDomainForecast Forecast) : FetchResult
```

The `ContentHash()` needs to be callable on `IDomainForecast`. Solutions:
- Add `int ContentHash()` to the `IDomainForecast` interface and implement in `DomainForecast<T>`
- Or cast in the Gateway

**Decision:** Add `ContentHash()` to `IDomainForecast` and implement in `DomainForecast<T>`. Clean, no casts.

### 3. ModelRun fix

All 6 clients pass `now` as ModelRun. Change to `points[0].ValidFrom` truncated to the hour (via `DateTimeOffset.Floor(TimeSpan.FromHours(1))` or manual truncation). This is the first data point's timestamp, which represents when the model data starts and only changes when the API delivers a new model run.

Fallback: if points are empty, use `DateTimeOffset.MinValue` (the forecast will be null/empty anyway).

## Risks / Trade-offs

- **[HashCode non-deterministic across processes]** `HashCode.Combine` uses a random seed per process. The hash is compared within a single PollWorker actor lifetime, never serialized or persisted as a hash. The persisted `DomainPolled.Hash` is only compared within the same process. On restart, the first poll will always be "Changed" (no previous hash), which is the correct behavior. No risk.
- **[Performance]** Hashing all points (up to 96 for Forecast, ~192 for Marine) on every poll. Each point has ~20-30 fields. Total: ~5000 `HashCode.Add()` calls per domain per poll. Negligible compared to HTTP latency.
- **[ModelRun truncation]** If the API returns sub-hourly timestamps in the future, truncation to hour would mask them. Current Open-Meteo APIs all use hourly (or daily/monthly) resolution, so this is not a concern now.
