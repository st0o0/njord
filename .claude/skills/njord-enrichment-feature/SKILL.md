---
name: njord-enrichment-feature
description: Use when adding or changing an enrichment (alerts today) - its domain handler, analyzer and alert rules in the domain project (src/Njord.Weather), the result/settings contracts in Njord.Messages, the handler/rule `Name` + `FeatureDescriptor` (toggle `Njord:Features:<Category>:<Name>`; add the entry to `docs/data/features.json`), DI registration as an IDomainHandler and golden-master snapshots.
---

# njord enrichment feature

Rules (static entity set, `TimeProvider`, toggleable features) live in `AGENTS.md` - follow them. This skill is the checklist.

## Where things live

- **`Njord.Core/Domain`** holds the framework every enrichment plugs into: `IDomainHandler` (`Feature` descriptor plus `Handle(current, previous, time)`, returning `PipelineEvent`s), `IAlertRule<T>` / `AlertOptions` / `AlertClock` and `ForecastQueries` (window and point queries on `DomainForecast<T>`).
- **`Njord.Messages`** holds each feature's contracts in a domain folder (`Alerts/`): result records (`AlertResult`), enums, `SeverityThresholds`. No analyzer, rule or formula (`ZoneArchitectureSpec`).
- **Feature library** (`Njord.Weather`, `Njord.AirQuality`, `Njord.Flood`; each references only `Njord.Core` and below) owns its computation:

| Library | Entry point | Units | Helpers |
|---|---|---|---|
| `Njord.Weather` | handler | 14 x `IAlertRule<DomainForecast<WeatherPoint>>` in `Rules/` (`FrostRule`, ...) | `AlertWindow` (shared 24 h window) |

The handler of alerts is `WeatherAlertHandler` (triggered by the weather domain), which turns every non-`None` alert into an `AlertResult` (`WireKey` = lowercase rule `Name`).

Naming: `<Feature>Analyzer`, `<Name>Rule`, `<Domain><Feature>Handler`. Never `Computer`, `Calculator`, `Scorer`, `Evaluator` or `Factory` in Messages or a feature library (`ConventionArchitectureSpec` enforces it).

## Rules

- **Stateless.** `IAlertRule.Evaluate(DomainForecast<WeatherPoint>, AlertSettings, DateTimeOffset)` returns one `Alert` (the rule's `Name` identifies it). Thresholds come only from the arguments, so hot reload applies on the next call. A rule never reads another rule's output.
- **Use the helpers**, never a private window loop: `forecast.Next24Hours(now)`, `SeverityThresholds.Classify`, `settings.Build(...)`. Shared constants (24 h window) live once (`AlertWindow`), not per rule.
- **Analyzer** takes `IEnumerable<IAlertRule>`, fails fast at construction unless every rule `Name` is unique, and orders the output by `Name` (registration order must not leak).
- Target size ~120 lines per rule file.

## Checklist

1. **Options + settings** - `AlertOptions` (with `bool Enabled`) and validator in `src/Njord.Core/Configuration/`, threshold accessors there (`AlertOptionsThresholds`), registered by the feature's `AddWeatherDomain()`. Rules take `AlertOptions` directly.
2. **Result type** - record in `src/Njord.Messages/<Feature>/`; non-alert features return one of `ScoreResult`, `ConditionResult`, `TrendResult`, `ForecastResult` (`src/Njord.Messages/Pipeline/FeatureResult.cs`); there is no per-feature enum, the lowercase `Name` is the wire key.
3. **Rule** - new class in the feature library (`Rules/`), `public sealed`, parameterless, built on the helpers.
4. **Register** - in the feature's `AddWeatherDomain()` (`src/Njord.Weather/ServiceCollectionExtensions.cs`): `services.AddSingleton<IAlertRule<DomainForecast<WeatherPoint>>, XxxRule>();` next to the analyzer, options and the `IDomainHandler` registration. The host calls `AddWeatherDomain()` from `src/Njord/Configuration/DomainSetupContainer.cs`; `src/Njord.Pipeline/Stream/ProjectionEngine.cs` invokes every registered `IDomainHandler` whose `Feature.Requirements` cover the changed domain and whose required fields are present.
5. **Consumers** - gRPC: `ToProto()` in `src/Njord.Grpc/Mapping/AlertProtoMappings.cs` and the event switch in `src/Njord.Grpc/Mapping/EnrichmentEventMappings.cs` (`ToEnrichmentEvent`).
6. **Tests** - one spec per rule in `src/Njord.Weather.Tests/Rules/<Type>RuleSpec.cs`, plain `[Fact]`s with a hand-built `DomainForecast<WeatherPoint>`. Registration specs cover one rule per type and the `AddWeatherDomain()` registration (`FeatureServices` in `src/Njord.Tests.Shared` builds the rules and handlers from the real registrations). Golden masters: `ProtoMappingGoldenMasterSpec` (`src/Njord.Grpc.Tests`), inputs in `src/Njord.Tests.Shared/AlertScenario.cs` (built with `WeatherForecastBuilder`). A refactoring must keep them green **without re-approval**; approve new `.verified.txt` files only for intended output changes, never edit existing ones.

## Worked example - an alert rule (from `FrostRule`)

```csharp
// src/Njord.Weather/Rules/FrostRule.cs
public sealed class FrostRule : IAlertRule<DomainForecast<WeatherPoint>>
{
    public string Name => "Frost";

    public Alert Evaluate(DomainForecast<WeatherPoint> forecast, AlertOptions options, DateTimeOffset now)
    {
        var thresholds = settings.Frost;
        var window = forecast.Next24Hours(now);
        if (window.LowestBy(p => p.Temperature) is not { } coldest)
        {
            return Alert.None();
        }

        var (severity, effectiveThreshold) = thresholds.Classify(coldest.Temperature);
        // ... details, settings.Build(Type, severity, coldest, details, ...)
    }
}

// src/Njord.Weather/ServiceCollectionExtensions.cs
services.AddSingleton<IAlertRule, FrostRule>();
```

## Pitfalls

- A feature's entities depend on config (`Enabled`), never on what a poll returned; missing values are `unavailable` states, not missing entities.
- A handler returns no events when a required domain is missing; it is skipped, not failed.
- Feature libraries never reference each other; share only through Core/Messages types.
