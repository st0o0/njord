## Why

Parameter identity lives in strings scattered across 7 production files —
51 individual string-to-parameter resolutions using 4 different mechanisms
(dictionary lookup, O(n) linear scan, raw string arrays, string-keyed
dictionaries). A typo in any of these silently returns `null`, which
downstream scoring treats as "average" (score 50), making bugs actively
invisible. Meanwhile, the foundation for typed access already exists:
`ParameterDef` is a proper value object and `ForecastPoint` already uses it
as a dictionary key. The gap is one layer up: Compute methods discover their
`ParameterDef` instances by string instead of receiving them typed.

Additionally, `ForecastSeries` has no querying API — every consumer
re-implements time-window filtering, and `Mean24h` is duplicated
character-for-character in `IndexResult` and `EnergyResult`.

## What Changes

- **Static typed properties on `ParameterRegistry`** — `Temperature2m`,
  `WindSpeed10m`, etc. for the ~15 parameters referenced in domain Compute
  methods. Non-nullable; a missing parameter is a startup error, not a
  silent null.
- **Dictionary-backed lookup API on `ResolvedParameterSet`** —
  `Get(ParameterDef)` for O(1) lookup and `Contains(ParameterDef)`. Replaces
  all 30 `FirstOrDefault(p => p.ApiName == "...")` linear scans.
- **Querying API on `ForecastSeries`** — `Window(from, to)`, `Mean(param,
  from, to)`, `Values(param, from, to)`. Eliminates 5+ copies of
  time-window filtering and the duplicated `Mean24h`.
- **All Compute methods migrated** — `IndexResult`, `EnergyResult`,
  `DerivedResult`, `TrendResult`, `ConsensusResult`, `HistoryResult`,
  `AlertEvaluator` switch from string lookups to typed registry properties.
- **TrendResult** — `string[] TrendParams` becomes `ParameterDef[]`,
  `Dictionary<string, double> Thresholds` becomes
  `Dictionary<ParameterDef, double>`.
- **HistoryAnalyzer / ForecastHistory** — entire API surface switches from
  `string paramApiName` to `ParameterDef`.
- **Mean24h deleted** — both copies removed, replaced by
  `ForecastSeries.Mean(...)`.

## Non-goals

- Changing compute algorithms, scoring weights, or thresholds.
- Changing `IndexScorer`'s default-50 on null — that is a domain decision.
- Changing `ParameterDef`'s fields or identity semantics.
- Modifying the Open-Meteo response parser — it legitimately works with
  strings from the JSON response.
- API-budget impact: zero — purely structural, no additional HTTP requests.

## Capabilities

### New Capabilities

- `parameter-typed-access`: Static typed properties on `ParameterRegistry`,
  dictionary-backed lookup on `ResolvedParameterSet`, and querying API on
  `ForecastSeries` (`Window`, `Mean`, `Values`).

### Modified Capabilities

*(No spec-level behavior changes — the requirements for what each enrichment
computes are unchanged. This change only affects how parameters are
resolved internally.)*

## Impact

- **Modified:** `ParameterRegistry.cs` (add ~15 static properties),
  `ResolvedParameterSet` (add lookup API), `ForecastSeries.cs` (add
  querying methods), `DailyForecastSeries.cs` (add `Window`).
- **Modified:** `IndexResult.cs`, `EnergyResult.cs`, `DerivedResult.cs`,
  `TrendResult.cs`, `ConsensusResult.cs`, `HistoryResult.cs`,
  `AlertEvaluator.cs` (replace string lookups with typed access).
- **Modified:** `HistoryAnalyzer.cs`, `ForecastHistory.cs` (replace
  string-keyed API with `ParameterDef`).
- **Deleted code:** `Mean24h` in `IndexResult` and `EnergyResult`.
- **Tests:** All Result spec files, `AlertEvaluatorSpec`,
  `HistoryAnalyzerSpec`, new `ForecastSeriesSpec` querying tests.
- **No new packages.**
