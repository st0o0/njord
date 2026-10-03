## Why

The domain layer has two boundary violations that blur architecture lines:

1. Four actor protocol messages (`ForecastRecorded`, `RecordSnapshot`,
   `QueryHistory`, `HistoryResponse`) live in `Domain/Analysis/ForecastHistory.cs`
   alongside legitimate domain types. These are used solely by
   `ForecastHistoryActor` in the Enrichment layer — they are actor protocol,
   not domain logic. `ForecastRecorded` is a byte-for-byte duplicate of
   `ForecastRecord` that adds no value.

2. `DailyForecastPoint` uses `IReadOnlyDictionary<ParameterDef, object?>` to
   accommodate mixed types (numeric measurements like `precipitation_sum` and
   text metadata like `sunrise`/`sunset`). This forces consumers to box/unbox,
   loses compile-time type safety, and is asymmetric with `ForecastPoint`
   (hourly) which uses `double?`. The `ParameterDef.ValueType` enum already
   distinguishes `Numeric` from `TimeString` — the type system should use it.

## What Changes

- **Move actor messages to Enrichment layer** — `RecordSnapshot`,
  `QueryHistory`, `HistoryResponse` move to
  `src/Njord/Enrichment/ForecastHistoryMessages.cs`.
- **Delete `ForecastRecorded`** — identical to `ForecastRecord`;
  `ForecastHistoryActor` uses `ForecastRecord` directly as its persistence
  event.
- **Split `DailyForecastPoint`** into two typed dictionaries:
  `NumericValues` (`IReadOnlyDictionary<ParameterDef, double?>`) and
  `MetaValues` (`IReadOnlyDictionary<ParameterDef, string?>`). Access via
  `GetNumeric(ParameterDef)` and `GetMeta(ParameterDef)`.
- **Adapt daily parsing** — the Open-Meteo response parser routes values
  into `NumericValues` or `MetaValues` based on `ParameterDef.ValueType`.
- **Adapt consumers** — `AlertEvaluator` and any other code accessing daily
  parameters switches from `Get()` (returning `object?`) to `GetNumeric()`
  or `GetMeta()`.

## Non-goals

- Changing `ForecastPoint` (hourly) — already correctly typed.
- Changing compute algorithms or scoring.
- API-budget impact: zero — purely structural.

## Capabilities

### New Capabilities

- `daily-forecast-typing`: Typed access to daily forecast values via
  separate `NumericValues` and `MetaValues` dictionaries on
  `DailyForecastPoint`.

### Modified Capabilities

*(No spec-level behavior changes — the data content is identical, only
the type representation changes.)*

## Impact

- **Moved:** 3 actor message types from `Domain/Analysis/` to `Enrichment/`.
- **Deleted:** `ForecastRecorded` record (duplicate of `ForecastRecord`).
- **Modified:** `DailyForecastPoint.cs`, `DailyForecastSeries.cs`,
  `ForecastHistory.cs`, `ForecastHistoryActor.cs`, Open-Meteo daily parser,
  `AlertEvaluator.cs` (daily parameter access).
- **Tests:** `ForecastHistoryActorSpec`, `AlertEvaluatorSpec`, daily parsing
  tests.
- **No new packages.**
