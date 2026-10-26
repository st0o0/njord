## Why

Startup-resolved configuration is spread across three intermediate types (FetchPlan, EffectiveWeatherModels, ServiceInfo), two setup containers with cross-container DI lambdas, and a forwarding registration. Each consumer reads only 1-2 fields from these bundles. The .NET OptionsBuilder pattern (`AddOptions<T>().Configure<TDep>()`) provides a framework-native way to compute derived values from services, replacing the ad-hoc materialisation with a single options class that integrates with ValidateOnStart.

## What Changes

- **New `FeatureRequirementsOptions`** — a POCO options class in `Njord.Core/Domain/` holding the resolved feature requirements: `DomainRequirements`, `Domains`, `WeatherModels`, `PrimaryWeatherModel`.
- **New `FeatureResolution.Configure()` static method** — populates a `FeatureRequirementsOptions` from `IEnumerable<IFeatureRule>` + `WeatherOptions`. Testable without DI.
- **OptionsBuilder registration** in `DomainSetupContainer` using `Configure<IEnumerable<IFeatureRule>, IOptions<WeatherOptions>>()` with `ValidateOnStart()`.
- **Consumer migration** — all consumers that took `FetchPlan`, `EffectiveWeatherModels`, `ServiceInfo`, or `IReadOnlySet<DomainKind>` switch to `IOptions<FeatureRequirementsOptions>`.
- **`ComparisonFeatureName`** moves from `EffectiveWeatherModels` to `ModelComparison` (the feature that owns it).
- **Ignored-models warning** moves from `BuildFetchPlan` to `WeatherClient` constructor.
- **Delete `FetchPlan.cs`** — replaced by `FeatureRequirementsOptions`.
- **Delete `EffectiveWeatherModels.cs`** — `ModelIds`/`PrimaryWeatherModel` live on the options class; `Resolve()` inlined into `Configure()`.
- **Delete `ServiceInfo.cs`** — gRPC services take `IOptions<FeatureRequirementsOptions>` + their own options directly.
- **Delete `ServiceInfoMappingExtensions.cs`** — `ToCatalog` moves to WeatherGrpcService or an extension taking `FeatureRequirementsOptions`.
- **Remove `IReadOnlySet<DomainKind>` forwarding** in `DomainSetupContainer`.
- **Remove `FeatureResolution.BuildFetchPlan()`** — replaced by `Configure()`.

## Non-goals

- Changing feature config registration (`AddFeatures`, `IConfigSection`, `AddNjordOptions`). The 37-feature registration stays as-is.
- Consolidating setup containers. The container split by concern stays.
- Splitting `FeatureDescriptor.Name` into separate config-key and wire-identity concerns.
- Changing the Options pattern for non-feature options (NjordOptions, WeatherOptions, PollingOptions, etc.).
- No polling or API changes; no budget impact.

## Capabilities

### New Capabilities
- `feature-requirements-options`: The OptionsBuilder-based resolution of feature requirements into a single `FeatureRequirementsOptions`, replacing FetchPlan, EffectiveWeatherModels, ServiceInfo, and the domain-set DI forwarding.

### Modified Capabilities
- `fetch-plan`: FetchPlan is removed entirely; its spec becomes historical.
- `domain-setup-containers`: DomainSetupContainer registration changes from FetchPlan singleton + forwarding to OptionsBuilder chain; ConfigSetupContainer loses ServiceInfo lambda.

## Impact

- **Production code**: ~15 files changed across Njord.Core, Njord.OpenMeteo (6 clients), Njord.Polling (PollWorker, BudgetGuard, PollingOptionsValidator), Njord.Pipeline (FeatureEngine), Njord.Grpc (2 gRPC services), Njord (2 setup containers). 4 files deleted.
- **Test code**: ~10 test files updated (client specs, BudgetGuardSpec, PollingOptionsValidatorSpec, gRPC specs, FetchPlanSpec, EffectiveWeatherModelsSpec, TestFetchPlan, TestServiceInfo). FetchPlanSpec and EffectiveWeatherModelsSpec become FeatureRequirementsOptionsSpec and FeatureResolutionConfigureSpec.
- **Messages**: `ServiceInfo.cs` and `CatalogResult` stay in `Njord.Messages`; ServiceInfo is deleted, CatalogResult is unchanged but built elsewhere.
- **No proto changes, no wire-format changes, no persistence changes.**
