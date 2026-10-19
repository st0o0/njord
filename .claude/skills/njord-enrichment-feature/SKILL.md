---
name: njord-enrichment-feature
description: Use when adding or changing an analysis feature (alerts, derived, trends, indices) — its analyzer, alert rules, index scores or formulas in the feature library (src/Njord.Alerts, Njord.Derived, Njord.Trends, Njord.Indices), the shared consensus query vocabulary in Njord.Consensus and the result/settings contracts in Njord.Messages, the options/settings toggle, DI registration and golden-master snapshots.
---

# njord analysis feature

Rules (static entity set, `TimeProvider`, HA device cut, toggleable features) live in `AGENTS.md` — follow them. This skill is the checklist.

## Where things live

- **`Njord.Consensus`** holds only what every feature shares:
  - `ConsensusBuilder` (`ModelForecasts` → `LocationConsensus`), `ConsensusStatistics` (median, spread, agreement, ...), the consensus records, `Settings/` (`ConsensusSettings`, `ConsensusMethod`).
  - `Querying/` — the query vocabulary, extension methods on consensus data: `Find`, `Within`, `Extreme`, `Hits`/`Earliest`/`Values`/`Peak`, `AgreementAbove`, `AverageAgreement`, `MaxModelCount`, `ModelsAt`, `At`/`MedianAt` (`HorizonWindow`, `HorizonHit`, `ScanDirection`).
  - No feature logic, no DI, no `InternalsVisibleTo` except `Njord.Consensus.Tests`.
- **`Njord.Messages`** holds each feature's contracts in a domain folder (`Alerts/`, `Derived/`, `Trends/`, `Indices/`): result records (`AlertResult`, `IndexResult`, ...), enums, `SeverityThresholds` and the immutable `XxxSettings` record. No analyzer, rule or formula (`ZoneArchitectureSpec`).
- **Feature library** (`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices`; references only `Njord.Core` and below) owns its computation:

| Library | Entry point | Units | Pure math |
|---|---|---|---|
| `Njord.Alerts` | `AlertAnalyzer` | 14 × `IAlertRule` in `Rules/` (`FrostRule`, ...) | `AlertWindow` (shared 24 h window) |
| `Njord.Indices` | `IndexAnalyzer` | 8 × `IIndexScore` in `Scores/` (`RunningScore`, ...) | `Scores/ScoreFormulas`, `DaySlicer`, `FrostProtection`, `VaporPressureDeficit` |
| `Njord.Derived` | `DerivedAnalyzer` | — | `DerivedFormulas` |
| `Njord.Trends` | `TrendAnalyzer` | — | `TrendFormulas` |

Naming: `<Feature>Analyzer`, `<Name>Rule`, `<Name>Score`, `<Feature>Formulas`, `Consensus<Role>`. Never `Computer`, `Calculator`, `Scorer`, `Evaluator`, `Factory` or `Enrichment` in Consensus, Messages or a feature library (`ConventionArchitectureSpec` enforces it).

## Rules and scores

- **Stateless.** `IAlertRule.Evaluate(LocationConsensus, AlertSettings)` returns one `Alert` of its `Type`; `IIndexScore.Score(DaySlice, ResolvedPreferences)` returns a `ScoredIndex` (point score + confidence envelope) for its `Kind`. Thresholds and preferences come only from the arguments, so hot reload applies on the next call. A rule never reads another rule's output.
- **Use the vocabulary**, never a private `FindParam`/lookup loop: `consensus.Hourly.Find(param)?.Next24Hours().Extreme(ScanDirection.Min)`; classify with `SeverityThresholds.Classify`. Shared constants (24 h window, day window) live once (`AlertWindow`), not per rule.
- **Scores** pick a `DayPeriod` and a formula: `slice.Score(DayPeriod.Daylight, m => Compute(m.Temperature, ..., preferences))`; `DaySlice.Score` applies the formula to the median means and to both confidence bounds. Expose the formula as `public static int Compute(...)` so specs can call it with raw inputs.
- **Analyzer** takes `IEnumerable<IAlertRule>`/`IEnumerable<IIndexScore>`, fails fast at construction unless every `AlertType`/`IndexKind` has exactly one, and orders the output by enum declaration order (registration order must not leak).
- Target size ~120 lines per rule/score file.

## Checklist

1. **Options + settings (feature library/Messages)** — `XxxOptions` (with `bool Enabled`) and validator in `src/Njord.<Feature>/Configuration/` (consensus options live in `Njord.Pipeline`), a `ToSettings()` conversion there, registered by the feature's `AddXxx(configuration)`; the immutable `XxxSettings` record in `src/Njord.Messages/<Feature>/`, wired into `AnalysisSettings`/`AnalysisSettingsFactory` (`src/Njord.Pipeline/Configuration/AnalysisSettingsFactory.cs`) and the host's `ConfigReloadPolicy` / `AnalysisConfigView` / `EffectiveConfigMappings`. Analyzers read only `Enabled` from options.
2. **Result type** — record in `src/Njord.Messages/<Feature>/`; new alert types/index kinds extend `AlertType`/`IndexKind` (and their wire names).
3. **Rule / score / formula** — new class in the feature library (`Rules/` or `Scores/`), `public sealed`, parameterless, built on the query vocabulary.
4. **Register** — in the feature's `AddXxx(configuration)` (`src/Njord.<Feature>/ServiceCollectionExtensions.cs`): `services.AddSingleton<IAlertRule, XxxRule>();` / `services.AddSingleton<IIndexScore, XxxScore>();` next to the analyzer and options. The host calls `AddXxx()` from `src/Njord/Configuration/AnalysisSetupContainer.cs`; `src/Njord.Pipeline/ForecastPipeline.cs` calls the analyzers' `Compute(...)` with the current settings.
5. **Consumers** — MQTT state payloads: a payload record in `src/Njord.Mqtt/Payloads/` plus its `ToPayload()` in `src/Njord.Mqtt/Mapping/AnalysisPayloadMappings.cs`, wired through `src/Njord.Mqtt/StatePayloadBuilder.cs` (`FromXxx`); gRPC: `ToProto()` in `src/Njord.Grpc/Mapping/<Feature>ProtoMappings.cs` and the event switch in `src/Njord.Grpc/Mapping/EnrichmentEventMappings.cs` (`ToEnrichmentEvent`).
6. **Tests** — one spec per rule/score in the feature test project (`src/Njord.Alerts.Tests/Rules/<Type>RuleSpec.cs`, `src/Njord.Indices.Tests/Scores/<Kind>ScoreSpec.cs`), plain `[Fact]`s with a consensus built via `ConsensusBuilder` (see `AlertRuleFixture`). Analyzer specs cover fail-fast, ordering and the `AddXxx()` registration (`FeatureServices` in `src/Njord.Tests.Shared` builds analyzers from the real registrations). Golden masters: `StatePayloadGoldenMasterSpec` (`src/Njord.Mqtt.Tests`) and `ProtoMappingGoldenMasterSpec` (`src/Njord.Grpc.Tests`), inputs in `src/Njord.Tests.Shared/AnalysisFixture.cs`. A refactoring must keep them green **without re-approval**; approve new `.verified.txt` files only for intended output changes, never edit existing ones.

## Worked example — an alert rule (from `FrostRule`)

```csharp
// src/Njord.Alerts/Rules/FrostRule.cs
public sealed class FrostRule : IAlertRule
{
    public AlertType Type => AlertType.Frost;

    public Alert Evaluate(LocationConsensus consensus, AlertSettings settings)
    {
        var thresholds = settings.Frost;
        if (consensus.Hourly.Find(ParameterRegistry.Temperature2m) is not { } temperature)
        {
            return Alert.None(Type);
        }

        var window = temperature.Next24Hours();
        if (window.Extreme(ScanDirection.Min) is not { } low)
        {
            return Alert.None(Type);
        }

        bool Freezing(double value) => thresholds.CrossesAdvisory(value);
        var confidence = window.Hits(Freezing).RoundedAverage();
        var (severity, effectiveThreshold) = thresholds.Classify(low.Value);
        // ... details, Alert(Type, severity, confidence, details, ...)
    }
}

// src/Njord.Alerts/ServiceCollectionExtensions.cs
services.AddSingleton<IAlertRule, FrostRule>();
```

## Pitfalls

- Consensus is not a feature: `ConsensusBuilder` runs in the pipeline before the analyzers.
- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- Feature libraries never reference each other; share only through Core/Messages/Consensus types.
