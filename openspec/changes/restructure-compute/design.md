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
