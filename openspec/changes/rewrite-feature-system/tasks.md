All test projects run sequentially from `src/`: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj`. Implementation is TDD (red first); xUnit v3, `Spec` suffix, sealed classes, `[Fact(Timeout = 5000)]`, BDD-style names, `TestContext.Current.CancellationToken`. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Core Abstractions

- [x] 1.1 Add `FeatureDescriptor` without `Name`: `OutputKind Output`, `FeatureCategory Category`, `bool DefaultEnabled`, `DomainRequirements Requirements`, `int PollDepth`; update all existing usages; specs in `Njord.Core.Tests`
- [x] 1.2 Add `IFeatureRule` interface (`FeatureDescriptor Descriptor`, `bool Enabled`) and `FeatureRule<TInput>` abstract base with `Project`, `Evaluate`, internal `Execute` in `Njord.Core/Domain`; specs for the base class behavior (skip on null Project, skip on null Evaluate, execute produces PipelineEvents) in `Njord.Core.Tests`
- [x] 1.3 Add typed input records in `Njord.Core/Domain/FeatureInputs.cs`: `WeatherInput`, `WeatherAqInput`, `WeatherSolarInput`, `FloodWeatherInput`, `FloodMarineWeatherInput`, `MarineWeatherInput`, `SeasonalWeatherInput`, `AqInput`
- [x] 1.4 Add `FeatureContext<TInput>` readonly record struct (`Current`, `History`, `Now`) in `Njord.Core/Domain`
- [x] 1.5 Add `RingBuffer<T>` (fixed-size circular buffer with `Push`, indexed access, `Count`) in `Njord.Core/Domain`; specs in `Njord.Core.Tests`
- [x] 1.6 Validate: `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`

## 2. Feature Result Hierarchy

- [x] 2.1 Add abstract base records per OutputKind in `Njord.Messages/Pipeline`: `AlertFeatureResult(Location, Severity, StartsAt, EndsAt)`, `ScoreFeatureResult(Location, Score, BestStart, BestHours)`, `ConditionFeatureResult(Location, State)`, `TrendFeatureResult(Location, Direction, Magnitude)`, `ForecastFeatureResult(Location)` — all extending `PipelineEvent`
- [x] 2.2 Add feature-specific sealed result records: `FrostAlertResult`, `HeatAlertResult`, `StormAlertResult`, `HeavyRainAlertResult`, `UvAlertResult`, `FogAlertResult`, `SnowAlertResult`, `PressureDropAlertResult`, `ThunderstormAlertResult`, `IceAlertResult`, `WindChillAlertResult`, `VisibilityAlertResult`, `TropicalNightAlertResult`, `HumidityAlertResult`, `PollenAlertResult`, `Pm25AlertResult`, `DustAlertResult`, `FloodRiskAlertResult`, `FloodRainAlertResult`, `SwimmingConditionsAlertResult` in `Njord.Messages`
- [x] 2.3 Add feature-specific sealed result records for scores, conditions, trends, forecasts: `BbqScoreResult`, `LaundryScoreResult`, `OutdoorScoreResult`, `SurfConditionsResult`, `CommuteSafetyResult`, `VentilationWindowResult`, `NightVentilationResult`, `ExerciseAdvisoryResult`, `GardeningResult`, `FloodTimelineResult`, `SeasonalOutlookResult`, `SolarYieldResult`, `SatelliteRadiationResult`, `ConfidenceTrendResult`, `ApproachingFrontResult`, `PollenTrendResult` in `Njord.Messages`
- [x] 2.4 Specs: verify each result type carries the correct base fields plus feature-specific fields; golden-master serialization in `Njord.Messages.Tests`
- [x] 2.5 Validate: `dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj`

## 3. Per-Feature Config Types

- [x] 3.1 Create config types for all 14 weather alert features (`FrostConfig`, `HeatConfig`, `StormConfig`, `HeavyRainConfig`, `UvConfig`, `FogConfig`, `SnowConfig`, `PressureDropConfig`, `ThunderstormConfig`, `IceConfig`, `WindChillConfig`, `VisibilityConfig`, `TropicalNightConfig`, `HumidityConfig`) in `Njord.Alerts/Configuration/`, each with `Enabled` + feature-specific thresholds migrated from `AlertOptions`
- [x] 3.2 Create config types for AQ alerts (`PollenConfig`, `Pm25Config`, `DustConfig`), flood alerts (`FloodRiskConfig`, `FloodRainConfig`), marine alerts (`SwimmingConditionsConfig`) in their respective projects
- [x] 3.3 Create config types for scores (`BbqConfig`, `LaundryConfig`, `OutdoorConfig`, `SurfConditionsConfig`), advisories (`CommuteSafetyConfig`, `VentilationWindowConfig`, `NightVentilationConfig`, `ExerciseAdvisoryConfig`, `GardeningConfig`, `FloodTimelineConfig`), insights (`SeasonalOutlookConfig`, `SolarYieldConfig`, `SatelliteRadiationConfig`, `ConfidenceTrendConfig`, `ApproachingFrontConfig`, `PollenTrendConfig`) — each with `Enabled` flag
- [x] 3.4 Create validators for configs with thresholds (alert configs); register via `AddNjordOptions<T>` pattern; specs for threshold ordering validation in `Njord.Core.Tests`
- [x] 3.5 Validate: `dotnet build Njord.slnx`

## 4. Feature Implementations — Alerts

- [x] 4.1 Convert all 14 weather alert rules to `FeatureRule<WeatherInput>` implementations in `Njord.Alerts`: `FrostAlert`, `HeatAlert`, `StormAlert`, `HeavyRainAlert`, `UvAlert`, `FogAlert`, `SnowAlert`, `PressureDropAlert`, `ThunderstormAlert`, `IceAlert`, `WindChillAlert`, `VisibilityAlert`, `TropicalNightAlert`, `HumidityAlert`; each takes `IOptions<XConfig>` via DI
- [x] 4.2 Convert AQ alerts to `FeatureRule<AqInput>`: `PollenAlert`, `Pm25Alert`, `DustAlert`
- [x] 4.3 Convert flood alerts: `FloodRiskAlert` as `FeatureRule<FloodWeatherInput>`, `FloodRainAlert` as `FeatureRule<FloodWeatherInput>`
- [x] 4.4 Convert marine alert: `SwimmingConditionsAlert` as `FeatureRule<MarineWeatherInput>`
- [x] 4.5 Delete `WeatherAlertHandler`, `AirQualityAlertHandler`, `FloodRiskHandler`, `FloodRainHandler`, `SwimmingConditionsHandler`, `AlertWindow`, `AirQualityAlertWindow`, all old rule classes
- [x] 4.6 Update `Njord.Alerts/ServiceCollectionExtensions.cs`: register per-feature configs + `IFeatureRule` singletons
- [x] 4.7 Rewrite alert specs in `Njord.Alerts.Tests`: test each feature's `Evaluate` directly with constructed input and config, verify result type and fields; preserve test coverage count
- [x] 4.8 Validate: `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj`

## 5. Feature Implementations — Scores, Insights, Advisories

- [x] 5.1 Convert score handlers to `FeatureRule<WeatherInput>`: `BbqScore`, `LaundryScore`, `OutdoorScore`; `SurfConditions` as `FeatureRule<MarineWeatherInput>`; keep `Scoring` utilities
- [x] 5.2 Update `Njord.Scores/ServiceCollectionExtensions.cs`; rewrite specs in `Njord.Scores.Tests`
- [x] 5.3 Convert insight handlers: `SeasonalOutlook` as `FeatureRule<SeasonalWeatherInput>`, `SolarYield` and `SatelliteRadiation` as `FeatureRule<WeatherSolarInput>`, `ConfidenceTrend` and `ApproachingFront` as `FeatureRule<WeatherInput>`, `PollenTrend` as `FeatureRule<AqInput>`
- [x] 5.4 Update `Njord.Insights/ServiceCollectionExtensions.cs`; rewrite specs in `Njord.Insights.Tests`
- [x] 5.5 Convert advisory handlers: `CommuteSafety`, `VentilationWindow`, `NightVentilation`, `ExerciseAdvisory`, `Gardening` as `FeatureRule<WeatherInput>`; `FloodTimeline` as `FeatureRule<FloodWeatherInput>`
- [x] 5.6 Update `Njord.Advisories/ServiceCollectionExtensions.cs`; rewrite specs in `Njord.Advisories.Tests`
- [x] 5.7 Validate: all four feature test projects green

## 5a. Consolidate into Njord.Features

- [x] 5a.1 Create `src/Njord.Features` and `src/Njord.Features.Tests`; add both to `Njord.slnx`; reference only `Njord.Core` (and below)
- [x] 5a.2 Move every feature with `git mv` into `<OutputKind>/<Feature>/` (`Alerts/`, `Scores/`, `Conditions/`, `Trends/`, `Forecasts/`; the folder follows the descriptor's `OutputKind`), with its rule, config and result side by side; split the grouped config files (`WeatherAlertConfigs.cs` etc.) into one file per feature
- [x] 5a.3 Shared code per OutputKind in `<OutputKind>/_Shared/`: `AlertWindow`, `AirQualityAlertWindow`, result base records, `Scoring` (moved back from `Njord.Core/Domain`), `SolarIrradianceBlend`, `SurfConditionsRule`; typed input records in `Inputs/`
- [x] 5a.4 Namespaces follow folders (`Njord.Features.Alerts.Frost`, ...); one `AddFeatures(configuration)` replaces the four `Add*Features` extensions, explicit registration (no assembly scanning)
- [x] 5a.5 Move the specs into `Njord.Features.Tests` with the same folder layout; delete `Njord.{Alerts,Scores,Insights,Advisories}` and their test projects
- [x] 5a.6 Host and `Njord.Tests.Shared` reference `Njord.Features`; build green; `dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj` green with the same test count as the four old projects combined

## 6. FeatureEngine and Pipeline Integration

- [x] 6.1 Implement `FeatureEngine`: constructor takes `IEnumerable<IFeatureRule>`, computes enabled features and ring depth; `Process(DomainDataReceived)` updates LocationForecast, pushes ring, evaluates features, returns events; `Requirements` property for resolution
- [x] 6.2 Rewrite `FeatureResolution` to use `IFeatureRule` and `Enabled` property instead of `FeatureOptions` + `IDomainHandler`
- [x] 6.3 Update `ProjectionEngine` to use `FeatureEngine` instead of `IReadOnlyList<IDomainHandler>`
- [x] 6.4 Update `ForecastPipeline` to construct `FeatureEngine` from `IEnumerable<IFeatureRule>` instead of `IEnumerable<IDomainHandler>` + `IOptions<FeatureOptions>`
- [x] 6.5 Specs for `FeatureEngine`: ring buffer behavior, feature skip on missing data, PollDepth history, enabled filtering, requirements union; in `Njord.Pipeline.Tests`
- [x] 6.6 Update `ForecastPipelineInputSpec` and other pipeline specs for the new engine
- [x] 6.7 Validate: `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`, `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`

## 7. Proto Contract and gRPC Mapping

- [x] 7.1 Add base proto messages (`AlertData`, `ScoreData`, `ConditionData`, `TrendData`, `ForecastData`) and per-feature messages (`FrostAlert`, `HeatAlert`, ..., `BbqScore`, ...) to `protos/njord/v2/weather.proto`
- [x] 7.2 Replace `EnrichmentEvent` payload: remove `type_name`, `AlertUpdate`, `LifestyleUpdate`; add `oneof feature` with all typed messages; reserve old field numbers
- [x] 7.3 Update `GetEnrichmentsResponse` to use typed feature messages
- [x] 7.4 Rewrite `Njord.Grpc` mapping: pattern match `FeatureResult` subtypes to proto messages; delete old `LifestyleUpdate` and `AlertUpdate` mapping
- [x] 7.5 Update `GrpcConsumer` and `WeatherGrpcService` for the new event/response types
- [x] 7.6 Specs: verify each feature result maps to its proto message correctly; golden-master serialization; in `Njord.Grpc.Tests`
- [x] 7.7 Validate: `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`

## 8. Delete Old Abstractions

- [x] 8.1 Delete `IDomainHandler.cs`, `IMultiFeatureHandler.cs`, `IAlertRule.cs`, `IFeature.cs`, `FeatureKeys` from `Njord.Core/Domain`
- [x] 8.2 Delete `AlertOptions.cs`, `AlertOptionsValidator.cs`, `FeatureOptions.cs` from `Njord.Core/Configuration`
- [x] 8.3 Delete old `FeatureResult.cs` (with `WireKey`) from `Njord.Messages/Pipeline`
- [x] 8.4 Delete `LifestyleUpdate` proto message and old `AlertUpdate` mapping code
- [x] 8.5 Update `Njord/Configuration/DomainSetupContainer.cs`: remove old handler registration calls, add per-feature registration
- [x] 8.6 Update `Njord.Tests.Shared/FeatureServices.cs` for new registration pattern
- [ ] 8.7 Delete `ConfigView` and `Njord.Tests.Shared/TestConfigView.cs`: `BudgetTracker`, `PollManager` and `PipelineHealthCheck` inject the `IOptions<NjordOptions>` / `IOptions<PollingOptions>` they read; drop the registration in `ConfigSetupContainer`; update the Budget/Polling specs, `openspec/specs/domain-setup-containers`, `openspec/specs/prometheus-metrics`, AGENTS.md and the `njord-actor-spec` skill
- [ ] 8.8 Delete `ConfigSections` (reflection on a `SectionName` constant): options types implement an interface with `static abstract string SectionName`, and `AddNjordOptions<T>`, `CollectionDefaults` and `AppSettingsDefaults` read `T.SectionName`

## 8b. Core Cleanup

- [ ] 8b.1 Options at FunkArr level: every options class is `public sealed class XOptions` with `public const string SectionName`, defaults as property initializers; registration is `AddOptions<T>().BindConfiguration(T.SectionName).ValidateOnStart()` (or a thin `AddNjordOptions<T>` on it). Delete the reflection-based `ConventionalValidator`, `CollectionDefaults` reflection and the one-line range validators; keep explicit `IValidateOptions<T>` validators only where cross-field rules exist, collecting failures with context in the message
- [ ] 8b.2 Per-feature threshold validators (three-step severity ordering) so invalid thresholds fail at startup; delete the unreachable `throw new InvalidOperationException("Invalid ... thresholds")` branches in the features
- [ ] 8b.3 Move options to their owners: AirQuality (+ region, extensions), Marine, Seasonal, Satellite and SolarYield options to `Njord.OpenMeteo`; `PersistenceOptions`/`PersistenceProvider` to the Host; `PollingOptionsValidator` to `Njord.Polling`. Remove the `SolarYieldOptions` registration from the alert options; delete dead `AlertOptionsThresholds`, `AddNjordFeatureOptions` and `ToWireText`
- [ ] 8b.4 Dissolve `Njord.Core/Configuration/Polling`: `BudgetCalculator.ComputeWeight`/`GetEffectiveBudget` to Budget/OpenMeteo, `Validate`/guard constants next to the polling validator; delete `BudgetValidation` (fields only read in tests); fold `BudgetAllowance` + `PollingOptionsExtensions` into `Njord.Budget` as `BudgetAllowance.From(PollingOptions)`; `RequestBudget` gets a `DailyLimit` and the free-tier constants move to OpenMeteo; `FetchTimeouts` leaves the Configuration namespace
- [ ] 8b.5 Move actor plumbing to its owner: `ShardMessageExtractor` to Polling, `StreamSupervision` to Pipeline, `GrpcStreamMessages` to Grpc
- [ ] 8b.6 Small cleanups: `FindLocation` becomes an instance method on `NjordOptions`; inline `WeatherParamExtensions.FlagCount`; fold `AlertClock` into `ForecastWindow`; delete the Core `Telemetry` meter if it stays without instruments (and its entry in `CoreSetupContainer.NjordMeters`)
- [x] 8b.7 Typed feature output instead of magic strings: replace `FeatureData` string maps and keys with typed result properties; closed-set labels (commute hazards/levels, exercise level, humidity, wind chill, UV, confidence/pollen trend, ventilation, gardening, flood timeline) become enums with explicit `ToProto()` switches, a spec per conversion and proto enums; human-readable sentences leave the service (ha-njord translates); `SeasonalOutlook` months as typed dates, not `"yyyy-MM"` strings
- [ ] 8b.8 Extension naming: every static class with `this` methods ends in `Extensions` and lives in a file of the same name; mapping classes become `<Domain><Target>MappingExtensions` (FunkArr style, e.g. `BudgetPersistenceMappings` -> `BudgetPersistenceMappingExtensions`); state extensions move out of the state files (`BudgetTrackerState.cs`, `GrpcConsumerState.cs`, `PollManagerState.cs`, `PollWorkerState.cs`, `AlertSeverity.cs`, `ConfidenceLevel.cs`); helpers without receiver (`ServiceInfoMappings.ToServiceInfo`, `PollWorkerStateExtensions.RetryBackoff`, `PollingRegistration`, `TestAkkaConfig`) become extensions on a real receiver or move to a non-extension helper; all call sites use extension syntax
- [ ] 8b.9 Update the AGENTS.md conventions (mappings, options, Core description); the `Extensions` naming is a convention enforced in review, not an ArchUnit rule

## 9. Architecture, Docs, and Final Validation

- [x] 9.1 Trim `Njord.Architecture.Tests/ArchitectureSpec.cs` to the FunkArr level: plain `[Fact]`s on explicit per-assembly layer fields, no `[Theory]`, no regex-derived namespace sets. Rules: Forecast/Messages/Persistence isolation, no Akka in Messages/Persistence, each feature library (`OpenMeteo`, `Budget`, `Polling`, `Pipeline`, `Features`, `Grpc`) depends on no other feature library, concrete classes sealed. Drop the per-folder `Feature_folders_do_not_depend_on_other_feature_folders` theory
- [x] 9.2 Delete `FeatureConfigKeySpec` and `FeaturesDataSpec` (the C# type is the feature identity, the compiler guarantees uniqueness; no data-only specs)
- [ ] 9.2a Test audit across all test projects: delete data-only, constructor and property specs and theories that only re-list a mapping table without behavior; keep one behavioral spec per rule/branch; enum-mapping specs stay (one per conversion, AGENTS.md); report the new total and update the test counts in AGENTS.md
- [x] 9.3 Update `Njord.IntegrationTests` for new feature registration and pipeline
- [ ] 9.4 Update `AGENTS.md` architecture section for the new feature system
- [ ] 9.5 Update `docs/data/features.json` generation for type-based features
- [ ] 9.6 Update `appsettings.Example.json` and `docker-compose.example.yml` for new config structure
- [ ] 9.7 Update Config Builder (`docs/.vitepress/theme/builder/`) for new config structure
- [ ] 9.8 Final validation from `src/`: `dotnet build Njord.slnx`, `dotnet format Njord.slnx --verify-no-changes`, every test project sequentially via `dotnet run --project`; from the repo root `openspec validate rewrite-feature-system --no-interactive`
