## Context

See proposal.md for motivation. Usage map of the current helpers (production code only):

| Helper | Used by |
|---|---|
| `ConsensusComputer` (statistics) | `ConsensusSnapshotFactory`, `AlertEvaluator` |
| `HorizonExtremeScanner`, `ThresholdClassifier` | `AlertEvaluator` only |
| `TimeSliceAggregator`, `DaySlice`, `IndexScorer` | `IndexComputer` only |
| `DerivedComputer` | `DerivedResultComputer` only |
| `TrendCalculator` | `TrendComputer` only |
| `FindParam` (private copies) | `AlertEvaluator`, `IndexComputer`, `DerivedResultComputer`, `TrendComputer`, `TimeSliceAggregator` |

So every feature-specific helper has exactly one feature as its user and can move with it.
Only consensus statistics and the scan/lookup shapes are truly shared.

This change assumes the two earlier changes are done: `Horizon`, `LocationId`, enums and
typed alert details exist, and analyzers receive settings records per call.

## Goals / Non-Goals

**Goals:** small, single-purpose classes; one naming scheme; feature logic in feature
libraries; shared lookups written once.

**Non-Goals:** different numeric results, new features, project additions or removals.

## Decisions

### D1: Query vocabulary as extension methods on consensus types
`src/Njord.Compute/Querying/ConsensusQueries.cs` provides:

```
snapshot.Hourly.Find(ParameterRegistry.Temperature2m)       → ParameterConsensus?
series.Within(Horizon.Hours(24))                            → HorizonWindow
window.Extreme(ScanDirection.Min, v => v <= t)              → HorizonHit? (Value, Horizon, Agreement, ModelCount)
window.AgreementAbove(threshold) / window.AverageAgreement()
window.MaxModelCount() / series.ModelsAt(Horizon)
```

`HorizonExtremeScanner`'s logic becomes `Extreme`. `HorizonWindow` is a lightweight value
over the ordered entries, so there is no copying.
*Alternative:* a wrapper class `ConsensusView`. Rejected because extension methods keep
the records as they are, and every feature can use them without allocating.

### D2: Rules and scores are stateless, registered in DI, with a fixed order
```
public interface IAlertRule { AlertType Type { get; } Alert Evaluate(ConsensusSnapshot c, AlertSettings s); }
public interface IIndexScore { IndexKind Kind { get; } ScoreEnvelope Score(DaySlice slice, ResolvedPreferences p); }
```
`AddAlerts()` registers all 14 rules as singletons. `AlertAnalyzer` takes
`IEnumerable<IAlertRule>`, checks at construction that every `AlertType` has exactly one
rule (fail fast), and orders the output by `AlertType`. The same pattern applies to
indices with `IndexKind`.
*Alternative:* a static array of rules. It works too, but DI lets the specs swap a single
rule and keeps registration in the feature's extension, which the spec requires.

### D3: Each feature library is laid out the same way
```
Njord.Alerts/   AlertAnalyzer.cs  IAlertRule.cs  Rules/FrostRule.cs … (14)  ServiceCollectionExtensions.cs
Njord.Indices/  IndexAnalyzer.cs  IIndexScore.cs  Scores/RunningScore.cs … (8)
                DaySlicer.cs  FrostProtection.cs  VaporPressureDeficit.cs  ServiceCollectionExtensions.cs
Njord.Derived/  DerivedAnalyzer.cs  DerivedFormulas.cs  ServiceCollectionExtensions.cs
Njord.Trends/   TrendAnalyzer.cs  TrendFormulas.cs  ServiceCollectionExtensions.cs
Njord.Compute/  Consensus/ (ConsensusBuilder, ConsensusStatistics, snapshot records)
                Querying/  Contracts/ (results, settings, SeverityThresholds)
```

### D4: Renames
| Old | New |
|---|---|
| `ConsensusSnapshotFactory` | `ConsensusBuilder` |
| `ConsensusComputer` | `ConsensusStatistics` |
| `AlertEvaluator` | 14 × `<Type>Rule` + `AlertAnalyzer` |
| `IndexComputer` + `IndexScorer` | `IndexAnalyzer` + 8 × `<Kind>Score` |
| `TimeSliceAggregator` | `DaySlicer` |
| `DerivedComputer` + `DerivedResultComputer` | `DerivedFormulas` + `DerivedAnalyzer` |
| `TrendCalculator` + `TrendComputer` | `TrendFormulas` + `TrendAnalyzer` |
| `HorizonExtremeScanner` | `ConsensusQueries.Extreme` |

### D5: Specs move with the code
Rule and score specs live in the feature test projects (`Njord.Alerts.Tests/Rules/…`).
Existing `AlertEvaluatorSpec` cases are split per rule, keeping their inputs and expected
values unchanged. This proves behavior did not change, together with the golden masters.

### D6: Architecture rules enforce the scheme
`Njord.Architecture.Tests` gains rules for the forbidden name parts, for "Compute contains
no `IAlertRule`/`IIndexScore` implementations", and for "no `InternalsVisibleTo` from
Compute to non-test assemblies".

## Risks / Trade-offs

- [Splitting a 1040-line class can subtly change the evaluation order or shared defaults
  (e.g. `MaxHorizonHours = 24`)] → Move one rule at a time and keep the golden masters
  green after each. Shared constants go into `AlertSettings` or a single `AlertDefaults`.
- [More files] → Each file has one purpose and ~30–80 lines, which is the point of the
  change.
- [DI-registered rules: a missing registration only shows at runtime] → The analyzer's
  fail-fast completeness check, plus a spec that resolves `AlertAnalyzer` from
  `AddAlerts()`.

## Migration Plan

Internal only; no deployment step. Each task group leaves the solution building and the
golden masters green, so the work can stop between groups.

## Deviations

Adaptations made during implementation, where the plan above or `tasks.md` no longer
matched the code left by `typed-analysis-models` and `split-options-per-feature`:

- **1.1 Query vocabulary shape.** `ConsensusQueries` provides the seven named queries plus
  four small helpers the rules needed to replace their private loops: `Values()` (all
  window entries with a median, as `HorizonHit`s, in series order), `Hits(qualifies)`,
  `Earliest(qualifies)` (lowest offset), `Peak(seed)` (the confidence-interval-upper peak
  `HorizonExtremeScanner` tracked), plus `At(horizon)` / `MedianAt(horizon)` lookups that
  also accept a missing (`null`) series. `HorizonWindow` is a `readonly record struct`
  over a `ParameterConsensus` and the last `Horizon` of the window; it keeps the series'
  own entry order and filters by horizon kind, so no entry is copied. `HorizonHit` is a
  sealed record class (`Value`, `Horizon`, `Agreement`, `ModelCount`).
- **1.2 Consensus folder.** Besides `ConsensusBuilder` and `ConsensusStatistics`, the
  consensus records (`ConsensusSnapshot`, `HourlyConsensus`, `DailyConsensus`,
  `ParameterConsensus`, `HorizonConsensus`, `OutlierInfo`, `ConfidenceIntervalInfo`,
  `ConsensusResult`) moved to `Njord.Compute/Consensus/` (namespace
  `Njord.Compute.Consensus`). The renamed specs live in `Njord.Compute.Tests/Consensus/`
  (`ConsensusBuilderSpec`, formerly `ConsensusSnapshotSpec`; `ConsensusStatisticsSpec`).
  The method names on `ConsensusStatistics` (`ComputeMedian`, ...) are unchanged.
- **1.3 Settings stay in `Settings/`.** `split-options-per-feature` already put the settings
  records in `Njord.Compute/Settings/` (namespace `Njord.Compute.Settings`); they stay
  there. Only the result records, `SeverityThresholds`, `ScanDirection` and the result
  enums moved to `Njord.Compute/Contracts/` (namespace `Njord.Compute.Contracts`). The
  specs of these contracts moved to `Njord.Compute.Tests/Contracts/`.
- **2.1 Analyzer signature.** `AlertAnalyzer(IEnumerable<IAlertRule>, IOptions<AlertOptions>)`
  no longer takes a `TimeProvider`: the old one was passed to `EvaluateAll` but never read.
  The completeness check also rejects a duplicated alert type, not only a missing one.
  While the rules were extracted one at a time, a transitional `EvaluatorRule` adapter
  registered the not-yet-extracted `AlertEvaluator` methods, so the golden masters ran
  through `AlertAnalyzer` and DI after every single rule; the adapter was deleted in 2.5.
- **2.1 Test wiring.** `Njord.Tests.Shared/FeatureServices` builds analyzers from the real
  `AddXxx()` registrations (empty `IConfiguration`, given `TimeProvider`). `AnalysisFixture`
  (the golden-master input), `DisabledAnalyzers` and `ForecastPipelineSettingsSpec` use it,
  so the golden masters exercise the registered rules and scores.
- **2.2-2.4 Shared alert window.** The 24 h horizon and the one-day daily window live once
  in `Njord.Alerts/AlertWindow.cs` (`Hours`, `LastHour`, `LastDay`, `Next24Hours()`,
  `HourlyHorizons`, `DaytimeHours()`/`NighttimeHours()` for the `is_day` split,
  `DailyMax()`, `RoundedAverage()`), not in `AlertSettings`: these values are not
  configurable today, and adding them to the settings would change the options surface.
  Rules that align several parameters per hour (fog, thunderstorm, ice, tropical night,
  humidity) keep an explicit loop over `HourlyHorizons` with `At(horizon)`, because the
  vocabulary scans one series at a time.
- **2.2-2.4 Rule specs.** The `AlertEvaluatorSpec` cases were ported unchanged into
  `Njord.Alerts.Tests/Rules/<Type>RuleSpec.cs`. Where a case passed explicit thresholds,
  they equal the `AlertOptions` defaults, so the specs pass `AlertOptions` defaults and
  override only the values that differed (`FogPersistentHours = 25`,
  `PressureDropSevereThreshold = 15`). `FrostRuleSpec` adds the two `analysis-rules`
  scenarios (rule in isolation, changed threshold on the next evaluation).
- **2.5 `ThresholdClassifier`** no longer existed (replaced by `SeverityThresholds` in
  `typed-analysis-models`); only `HorizonExtremeScanner` and its spec were deleted.
- **3.x Order of the index split.** To check every score against the golden masters, the
  index code first moved unchanged into `Njord.Indices` (Compute cannot reference a
  feature library), then `DaySlicer` replaced `TimeSliceAggregator`, then the scores were
  swapped into the old `IndexComputer` one at a time with a golden-master run after each,
  and finally `IndexAnalyzer` replaced `IndexComputer`.
- **3.1 `DaySlice` carries everything a score needs.** Besides the daylight/night/full-day
  median means (`PeriodMeans`, with `DayMeans`/`NightMeans`/`FullDayMeans` kept as
  accessors), a slice carries the sunshine share (`SunshinePercent`) and, when at least one
  temperature horizon has two models, the pessimistic/optimistic confidence-bound means and
  their average agreement (`ConfidenceBounds`). These were recomputed inside
  `IndexComputer` before. `DaySlicer.Slice(consensus, now)` takes the current time instead
  of a `TimeProvider`; `IndexAnalyzer` reads `TimeProvider.GetUtcNow()` once per analysis
  (the old code read it three times per call, which only differs across an hour boundary
  within one call).
- **3.1/3.3 No `ResolvedParameterSet` dependency.** `IndexComputer` looked parameters up via
  `ResolvedParameterSet.Get(...)` before searching the consensus. The hourly consensus only
  contains resolved hourly parameters, so looking up the registry definition directly is
  equivalent; `IndexAnalyzer` no longer needs the parameter set.
- **3.1 Frost-protection and VPD specs.** `VaporPressureDeficitSpec` holds the three ported
  `VpdCategory` cases. Frost protection had no direct spec before (only
  `IndexResultSpec.Frost_protection_computed_once_not_per_day`, now in
  `IndexAnalyzerSpec`); `FrostProtectionSpec` adds three direct cases.
- **3.2 Score shape.** `IIndexScore.Score(DaySlice, ResolvedPreferences)` returns
  `ScoredIndex(int Value, ScoreEnvelope? Envelope)` instead of only a `ScoreEnvelope`:
  `DayScoreSet` needs both the point score and the envelope. `DaySlice.Score(period,
  formula)` applies one formula to the median means (point score) and to both bound means
  (envelope), so the confidence-envelope logic exists once. Each score exposes its formula
  as `public static int Compute(...)`, which the ported `IndexScorerSpec` cases call with
  their original inputs (the `BreezeScore` cases call `OutdoorScore.Breeze`). Formula parts
  shared by several scores live in `Scores/ScoreFormulas.cs`.
- **3.3 Analyzer.** `IndexAnalyzer(IEnumerable<IIndexScore>, IOptions<IndexOptions>,
  TimeProvider)` checks for exactly one score per `IndexKind` (fail fast), evaluates them in
  `IndexKind` order and maps them into the fixed `DayScoreSet` fields. `IndexResultSpec`
  cases were ported into `IndexAnalyzerSpec` with their own inputs (`ResultT0`,
  `ResultParameters`).
