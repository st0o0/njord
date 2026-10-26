## 1. New types and configure method

- [x] 1.1 Create `src/Njord.Core/Domain/FeatureRequirementsOptions.cs` — POCO with `Requirements` (DomainRequirements), `Domains` (IReadOnlyList\<DomainKind\>), `WeatherModels` (ImmutableArray\<string\>), `PrimaryWeatherModel` (computed). All properties read-write with defaults.
- [x] 1.2 Move `ComparisonFeatureName` from `EffectiveWeatherModels` to `ModelComparison.FeatureName` (`src/Njord.Features/Forecasts/ModelComparison/ModelComparison.cs`). Update the reference in `FeatureResolution`.
- [x] 1.3 Add `FeatureResolution.Configure(FeatureRequirementsOptions, IEnumerable<IFeatureRule>, WeatherOptions)` static method in `src/Njord.Core/Domain/FeatureResolution.cs`. Contains the logic from `BuildFetchPlan` minus logging and call-weight computation.
- [x] 1.4 Write `FeatureResolutionConfigureSpec` in `src/Njord.Core.Tests/Domain/` — test the static Configure method: enabled features produce correct requirements/domains/models, no features produce defaults, model comparison toggles model list, base params are applied.

## 2. OptionsBuilder registration

- [x] 2.1 Register `FeatureRequirementsOptions` in `src/Njord/Configuration/DomainSetupContainer.cs` using `AddOptions<FeatureRequirementsOptions>().Configure<IEnumerable<IFeatureRule>, IOptions<WeatherOptions>>()` with `ValidateOnStart()`. Remove `FetchPlan` singleton and `IReadOnlySet<DomainKind>` forwarding registrations.
- [x] 2.2 Remove `ServiceInfo` lambda from `src/Njord/Configuration/ConfigSetupContainer.cs`.

## 3. Migrate domain clients (Njord.OpenMeteo)

- [x] 3.1 Migrate `WeatherClient` (`src/Njord.OpenMeteo/Weather/WeatherClient.cs`): take `IOptions<FeatureRequirementsOptions>` instead of `FetchPlan`. Read `.Requirements.AllWeather`, `.WeatherModels`, `.PrimaryWeatherModel`. Add ignored-models warning in constructor.
- [x] 3.2 Migrate `AirQualityClient`, `FloodClient`, `MarineClient`, `SeasonalClient`, `SatelliteRadiationClient` (each in `src/Njord.OpenMeteo/<Domain>/`): take `IOptions<FeatureRequirementsOptions>` instead of `FetchPlan`, read `.Requirements.All<Domain>`.
- [x] 3.3 Update `src/Njord.OpenMeteo.Tests/OpenMeteoClientSpec.cs` and related client tests: construct clients with `Options.Create(new FeatureRequirementsOptions { ... })` instead of `FetchPlan`/`EffectiveWeatherModels`.

## 4. Migrate polling (Njord.Polling)

- [x] 4.1 Migrate `PollWorker` (`src/Njord.Polling/PollWorker.cs`): take `IOptions<FeatureRequirementsOptions>` instead of `IReadOnlySet<DomainKind>`, read `.Domains`.
- [x] 4.2 Migrate `BudgetGuard` (`src/Njord.Polling/Configuration/BudgetGuard.cs`): take `FeatureRequirementsOptions` instead of `FetchPlan`. Compute `WeatherCallWeight` inline from `Requirements` + `WeatherModels.Length` + `WeatherOptions.ForecastDays`.
- [x] 4.3 Migrate `PollingOptionsValidator` (`src/Njord.Polling/Configuration/PollingOptionsValidator.cs`): take `IOptions<FeatureRequirementsOptions>` instead of `FetchPlan`, pass `.Value` to `BudgetGuard.Violation`.
- [x] 4.4 Update `src/Njord.Polling.Tests/BudgetGuardSpec.cs` and `src/Njord.Polling.Tests/PollingOptionsValidatorSpec.cs`: construct with `FeatureRequirementsOptions` instead of `FetchPlan`.
- [x] 4.5 Update PollWorker test bases (`src/Njord.Polling.Tests/PollWorkerSpecBase.cs`, `PollWorkerTriggerSpec.cs`, `PollWorkerFetchReplySpec.cs`): register `IOptions<FeatureRequirementsOptions>` instead of `IReadOnlySet<DomainKind>`.

## 5. Migrate pipeline (Njord.Pipeline)

- [x] 5.1 Optionally migrate `FeatureEngine` (`src/Njord.Pipeline/Stream/FeatureEngine.cs`) to take `FeatureRequirementsOptions` for `EnabledFeatures` and `Requirements` instead of resolving them from `IEnumerable<IFeatureRule>`. Or leave as-is since it is created manually in a stream closure.

## 6. Migrate gRPC (Njord.Grpc)

- [x] 6.1 Migrate `WeatherGrpcService` (`src/Njord.Grpc/WeatherGrpcService.cs`): take `IOptions<NjordOptions>` + `IOptions<FeatureRequirementsOptions>` instead of `ServiceInfo`. Build `CatalogResult` in constructor. Move `ToCatalog` logic from `ServiceInfoMappingExtensions`.
- [x] 6.2 Migrate `OpsGrpcService` (`src/Njord.Grpc/OpsGrpcService.cs`): take `IOptions<PollingOptions>` + `IEnumerable<IFeatureRule>` instead of `ServiceInfo`. Read budget from options, compute enrichments from features.
- [x] 6.3 Update `src/Njord.Grpc.Tests/WeatherGrpcServiceSpec.cs` and `src/Njord.Grpc.Tests/TestServiceInfo.cs`: construct with `IOptions<FeatureRequirementsOptions>` and `IOptions<NjordOptions>`.

## 7. Delete old types

- [x] 7.1 Delete `src/Njord.Core/Domain/FetchPlan.cs`.
- [x] 7.2 Delete `src/Njord.Core/Domain/EffectiveWeatherModels.cs`.
- [x] 7.3 Delete `src/Njord.Messages/Config/ServiceInfo.cs`.
- [x] 7.4 Delete `src/Njord/Configuration/ServiceInfoMappingExtensions.cs`.
- [x] 7.5 Remove `BuildFetchPlan` from `src/Njord.Core/Domain/FeatureResolution.cs` (keep `WithBaseParams`, `RequiredParams`, `RequiredDomains`).
- [x] 7.6 Delete or replace `src/Njord.Core.Tests/Domain/FetchPlanSpec.cs` and `src/Njord.Core.Tests/Domain/EffectiveWeatherModelsSpec.cs` — the new `FeatureResolutionConfigureSpec` covers this.
- [x] 7.7 Update `src/Njord.Tests.Shared/TestFetchPlan.cs` — either remove it (if no longer referenced) or replace with a `TestFeatureRequirements` helper.

## 8. Update docs and specs

- [x] 8.1 Update `AGENTS.md` and `CLAUDE.md`: replace FetchPlan/EffectiveWeatherModels/ServiceInfo references with `FeatureRequirementsOptions`. Update the solution structure table, architecture guardrails, and decisions.
- [x] 8.2 Update `openspec/specs/fetch-plan/spec.md` and `openspec/specs/domain-setup-containers/spec.md` main specs if delta archiving does not cover it.

## 9. Validation

- [x] 9.1 `dotnet build src/Njord.slnx`
- [x] 9.2 `dotnet format src/Njord.slnx --verify-no-changes`
- [x] 9.3 Run all test projects sequentially:
  ```bash
  for p in src/Njord.*Tests; do
    [ "$p" = "src/Njord.Tests.Shared" ] && continue
    dotnet run --project "$p/$p.csproj" --no-build
  done
  ```
