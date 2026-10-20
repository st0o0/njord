## Why

The feature system has three layers of abstraction for one job: `IAlertRule<DomainForecast<T>>` evaluates a single-domain alert, `IDomainHandler` wraps one or more rules into pipeline events, and `IMultiFeatureHandler` groups independently toggled rules under one handler. Alerts use all three layers; scores, conditions, trends and forecasts use only `IDomainHandler` — two different models for the same concern. The `IAlertRule<T>` generic constrains rules to one domain type, so cross-domain alerts (flood + marine + weather) cannot be expressed. Features are identified by `string Name` properties, matched against config via string lookup (`FeatureOptions : Dictionary<string, Dictionary<string, bool>>`), and carried on the wire as `string type_name` / `string type` in generic map-of-string payloads. The monolithic `AlertOptions` bundles thresholds for all 19 alerts in one class. Adding a feature requires touching handler registration, rule registration, container handler wiring, `AlertOptions`, `FeatureOptions`, the proto contract and the gRPC mapping — six places for one concept.

## What Changes

- **BREAKING**: delete `IDomainHandler`, `IMultiFeatureHandler`, `IAlertRule<T>`, `IFeature`, `FeatureKeys` from `Njord.Core/Domain`. Replace with `FeatureRule<TInput>` abstract base: a typed projection from `LocationForecast` to a narrow input record, plus a pure evaluation function.
- **BREAKING**: delete `FeatureOptions` (string-based dictionary) and `AlertOptions` (monolithic threshold bag) from `Njord.Core/Configuration`. Each feature gets its own config type (e.g. `FrostConfig`, `BbqConfig`) bound via `IOptions<T>` to its own config section (`Njord:Features:Weather:Frost`). The `Enabled` flag lives on the per-feature config, not in a separate toggle map.
- **BREAKING**: delete `FeatureResult` base and its five subtypes from `Njord.Messages/Pipeline`. Replace with a typed result hierarchy: abstract bases per `OutputKind` (`AlertFeatureResult`, `ScoreFeatureResult`, etc.) with feature-specific sealed records carrying feature-specific data (e.g. `FrostAlertResult` carries `MinTemperature` and `DewPoint`).
- Delete all 22 handler classes and all 19 alert rule classes. Replace with ~35 `FeatureRule<TInput>` implementations: `FrostAlert`, `HeatAlert`, `BbqScore`, `CommuteSafety`, `ForecastDrift`, etc.
- Add ~6-8 typed input records (`WeatherInput`, `WeatherAqInput`, `FloodMarineWeatherInput`, etc.) to `Njord.Core/Domain`. Each feature's `Project` method extracts its declared domains — it never sees `LocationForecast` directly.
- Add `FeatureContext<TInput>` carrying `Current`, `History` (bounded ring of projected inputs from past polls), and `Now`. Features declare `PollDepth` in their descriptor; the engine provides that many historical entries.
- Add `FeatureEngine` in `Njord.Core/Domain`: a single class that replaces the handler iteration in `ProjectionEngine`. It holds a `RingBuffer<LocationForecast>` per location (depth = max `PollDepth` across all enabled features), evaluates all enabled features sequentially, and wraps results as `PipelineEvent`s. The ring does not survive restarts; features with `PollDepth > 0` produce no result until enough polls have accumulated.
- `FeatureDescriptor` drops its reliance on `IFeature.Name` — there is no `Name` property. Category, OutputKind, DefaultEnabled, Requirements, and PollDepth remain. Feature identity is the C# type.
- `FeatureResolution` simplifies: iterate all features, check each feature's `Config.Enabled`, union their `Requirements`. No `IMultiFeatureHandler` special case.
- **BREAKING (wire)**: replace `EnrichmentEvent.type_name` (string) and `LifestyleUpdate.type` (string) + `map<string, string> data` with a proto `oneof feature` carrying typed messages per feature. Each feature message embeds a base (`AlertData`, `ScoreData`, etc.) plus feature-specific fields. The proto field name within the `oneof` is the wire identity — no string identifier, no enum. Old ha-njord clients are incompatible (breaking, acceptable at 0.x).
- Registration: one `AddNjordOptions<FrostConfig>(config, section)` + one `AddSingleton<IFeatureRule, FrostAlert>()` per feature, in the existing `ServiceCollectionExtensions` per project.

## Capabilities

### New Capabilities
- `feature-rule-abstraction`: The `FeatureRule<TInput>` base, `IFeatureRule` interface, `FeatureContext<TInput>`, typed input records, and typed result hierarchy.
- `feature-engine`: The `FeatureEngine` class, ring buffer, sequential evaluation, integration with `ProjectionEngine` and `ForecastPipeline`.
- `per-feature-config`: Per-feature config types with `Enabled` flag, bound via `IOptions<T>`. Replaces `AlertOptions` and `FeatureOptions`.
- `typed-feature-wire`: Protobuf `oneof` with per-feature messages, gRPC mapping via pattern match on C# types.

### Modified Capabilities
- `grpc-v2-weather-service`: `EnrichmentEvent` and `GetEnrichmentsResponse` change to typed feature messages.
- `feature-catalog`: `FeatureDescriptor` drops `Name` dependency; `FeatureConfigKeySpec` adapted; `features.json` generation changes.

## Non-goals

- No new features or enrichments (roadmap features come after this lands).
- No gateway, polling, or budget changes.
- No new domains or variables.
- No persistence changes.
- No ha-njord implementation (noted as a required follow-up for the new proto contract).

## API-budget impact

None. No polling, request, or variable changes.

## Impact

- `src/Njord.Core`: `Domain/` — delete `IDomainHandler.cs`, `IMultiFeatureHandler.cs`, `IAlertRule.cs`, `IFeature.cs`; add `IFeatureRule.cs`, `FeatureRule.cs`, `FeatureContext.cs`, `FeatureInputs.cs`, `FeatureEngine.cs`; modify `FeatureDescriptor.cs`, `FeatureResolution.cs`, `LocationForecast.cs` (remains but features no longer access it directly). `Configuration/` — delete `AlertOptions.cs`, `AlertOptionsValidator.cs`, `FeatureOptions.cs`; add per-feature config types (or place them in the feature projects).
- `src/Njord.Messages/Pipeline`: delete `FeatureResult.cs`; add typed result hierarchy.
- `src/Njord.Alerts`, `Njord.Scores`, `Njord.Insights`, `Njord.Advisories`: rewrite all handlers/rules as `FeatureRule<TInput>` implementations, then merge the four projects (and their test projects) into one `src/Njord.Features` with folders `<OutputKind>/<Feature>/` and one `AddFeatures(configuration)`.
- `src/Njord.Pipeline`: `ProjectionEngine` delegates to `FeatureEngine`; `ForecastPipeline` wiring updated.
- `src/Njord.Grpc`: rewrite `EnrichmentEvent` / `LifestyleUpdate` mapping to typed per-feature messages.
- `protos/njord/v2/weather.proto`: new typed feature messages, `oneof feature` in `EnrichmentEvent`.
- `src/Njord/Configuration/DomainSetupContainer.cs`: registration updated.
- All test projects: specs rewritten for the new abstractions.
- `AGENTS.md`, `docs/data/features.json`, `docker-compose.example.yml`, `appsettings.Example.json`: updated.
- `src/Njord.Architecture.Tests`: zone rules and feature config spec updated.
