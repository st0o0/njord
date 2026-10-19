---
name: njord-enrichment-feature
description: Use when adding or changing an enrichment (alerts today) - its projector, enrichment, analyzer and alert rules in the feature library (src/Njord.Alerts), the result/settings contracts in Njord.Messages, the options/settings toggle, DI registration as a projection group and golden-master snapshots.
---

# njord enrichment feature

Rules (static entity set, `TimeProvider`, toggleable features) live in `AGENTS.md` - follow them. This skill is the checklist.

## Where things live

- **`Njord.Core/Domain`** holds the framework every enrichment plugs into: `IProjectionGroup` / `ProjectionGroup<TInput>` (a projector plus its enrichments), `IProjector<TInput>` (turns a `LocationForecast` and its previous value into an `IEnrichmentInput`, or null when the required domains are missing), `IEnrichment<TInput>` (turns an input into `EnrichmentEvent`s) and `ForecastQueries` (window and point queries on `DomainForecast<T>`).
- **`Njord.Messages`** holds each feature's contracts in a domain folder (`Alerts/`): result records (`AlertResult`), enums, `SeverityThresholds` and the immutable `AlertSettings` record. No analyzer, rule or formula (`ZoneArchitectureSpec`).
- **Feature library** (`Njord.Alerts`; references only `Njord.Core` and below) owns its computation:

| Library | Entry point | Units | Helpers |
|---|---|---|---|
| `Njord.Alerts` | `AlertAnalyzer` | 14 x `IAlertRule` in `Rules/` (`FrostRule`, ...) | `AlertWindow` (shared 24 h window) |

The projection group of alerts is `WeatherAlertProjector` (requires the weather domain) plus `WeatherAlertEnrichment`, which turns every non-`None` alert into an `AlertRaisedEvent`.

Naming: `<Feature>Analyzer`, `<Name>Rule`, `<Domain><Feature>Projector`, `<Domain><Feature>Enrichment`. Never `Computer`, `Calculator`, `Scorer`, `Evaluator` or `Factory` in Messages or a feature library (`ConventionArchitectureSpec` enforces it).

## Rules

- **Stateless.** `IAlertRule.Evaluate(DomainForecast<WeatherPoint>, AlertSettings, DateTimeOffset)` returns one `Alert` of its `Type`. Thresholds come only from the arguments, so hot reload applies on the next call. A rule never reads another rule's output.
- **Use the helpers**, never a private window loop: `forecast.Next24Hours(now)`, `SeverityThresholds.Classify`, `settings.Build(...)`. Shared constants (24 h window) live once (`AlertWindow`), not per rule.
- **Analyzer** takes `IEnumerable<IAlertRule>`, fails fast at construction unless every `AlertType` has exactly one, and orders the output by enum declaration order (registration order must not leak).
- Target size ~120 lines per rule file.

## Checklist

1. **Options + settings** - `AlertOptions` (with `bool Enabled`) and validator in `src/Njord.Alerts/Configuration/`, a `ToSettings()` conversion there (`AlertSettingsConversion`), registered by the feature's `AddAlerts(configuration)`; the immutable `AlertSettings` record in `src/Njord.Messages/Alerts/`. The analyzer reads only `Enabled` from options.
2. **Result type** - record in `src/Njord.Messages/<Feature>/`; new alert types extend `AlertType` (and its wire names).
3. **Rule** - new class in the feature library (`Rules/`), `public sealed`, parameterless, built on the helpers.
4. **Register** - in the feature's `AddAlerts(configuration)` (`src/Njord.Alerts/ServiceCollectionExtensions.cs`): `services.AddSingleton<IAlertRule, XxxRule>();` next to the analyzer, options and the `ProjectionGroup<WeatherAlertInput>` registration. The host calls `AddAlerts()` from `src/Njord/Configuration/AnalysisSetupContainer.cs`; `src/Njord.Pipeline/ProjectionEngine.cs` evaluates every registered `IProjectionGroup` when a domain it requires changes.
5. **Consumers** - MQTT state payloads: a payload record in `src/Njord.Mqtt/Payloads/` plus its `ToPayload()` in `src/Njord.Mqtt/Mapping/AnalysisPayloadMappings.cs`, wired through `src/Njord.Mqtt/StatePayloadBuilder.cs` (`FromAlerts`); gRPC: `ToProto()` in `src/Njord.Grpc/Mapping/AlertProtoMappings.cs` and the event switch in `src/Njord.Grpc/Mapping/EnrichmentEventMappings.cs` (`ToEnrichmentEvent`).
6. **Tests** - one spec per rule in `src/Njord.Alerts.Tests/Rules/<Type>RuleSpec.cs`, plain `[Fact]`s with a hand-built `DomainForecast<WeatherPoint>`. Analyzer specs cover fail-fast, ordering and the `AddAlerts()` registration (`FeatureServices` in `src/Njord.Tests.Shared` builds the analyzer from the real registrations). Golden masters: `StatePayloadGoldenMasterSpec` (`src/Njord.Mqtt.Tests`) and `ProtoMappingGoldenMasterSpec` (`src/Njord.Grpc.Tests`), inputs in `src/Njord.Tests.Shared/AnalysisFixture.cs`. A refactoring must keep them green **without re-approval**; approve new `.verified.txt` files only for intended output changes, never edit existing ones.

## Worked example - an alert rule (from `FrostRule`)

```csharp
// src/Njord.Alerts/Rules/FrostRule.cs
public sealed class FrostRule : IAlertRule
{
    public AlertType Type => AlertType.Frost;

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertSettings settings, DateTimeOffset now)
    {
        var thresholds = settings.Frost;
        var window = forecast.Next24Hours(now);
        if (window.LowestBy(p => p.Temperature) is not { } coldest)
        {
            return Alert.None(Type);
        }

        var (severity, effectiveThreshold) = thresholds.Classify(coldest.Temperature);
        // ... details, settings.Build(Type, severity, coldest, details, ...)
    }
}

// src/Njord.Alerts/ServiceCollectionExtensions.cs
services.AddSingleton<IAlertRule, FrostRule>();
```

## Pitfalls

- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- A projector returns null when a required domain is missing; the enrichment is then skipped, not failed.
- Feature libraries never reference each other; share only through Core/Messages types.
