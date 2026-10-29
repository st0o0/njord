## Why

`Njord:Weather` mixes configuration for two distinct Open-Meteo APIs (Forecast at `api.open-meteo.com/v1/forecast` and Ensemble at `ensemble-api.open-meteo.com/v1/ensemble`) into a single section. `ForecastDays` applies to both APIs but they have different maximums (16 vs 35), `Models` is purely Ensemble-specific, and `PastHours` is purely Forecast-specific. The intermediate container `FeatureRequirementsOptions` exists only to shuttle model IDs and redundant derived fields between resolution and consumers, and the static `FeatureResolution` class contains model-filtering logic that duplicates what `EnsembleClient` already decides via the `EnsembleParams.Models` flag. Splitting the config makes each API independently tunable, eliminates the intermediate workarounds, and enables multi-model selection on the Forecast API (the parser already supports it).

## What Changes

- **BREAKING**: `Njord:Weather` section replaced by `Njord:Forecast` (ForecastDays, PastHours, Models) and `Njord:Ensemble` (ForecastDays, Models).
- **BREAKING**: `FeatureRequirementsOptions` removed. Consumers receive `DomainRequirements` directly (registered as `IOptions<DomainRequirements>` via PostConfigure).
- `FeatureResolution` static class removed. `WithBaseParams()` and `RequiredDomains()` become instance/extension methods on `DomainRequirements`. Enabled-feature filtering is inlined at each call site.
- `WeatherModelCatalog` removed. Model ID validation reduced to non-empty + no-duplicates; invalid IDs produce an API 400 at runtime.
- `ComparisonFeatureName` string-match in resolution removed. `EnsembleClient` decides multi-model mode solely from `EnsembleParams.Models` flag and reads models from `EnsembleOptions`.
- `ForecastClient` gains optional multi-model support: `ForecastOptions.Models` defaults to empty (= `best_match`); when populated, the existing `SplitByModel` parser handles per-model responses.
- `BudgetGuard` uses per-API ForecastDays for correct cost projection.
- gRPC catalog reads ensemble model list from `EnsembleOptions` directly.

### Budget impact

No change to polling frequency or domain count. Per-API ForecastDays allows operators to tune Ensemble independently (e.g. 15 days) which could increase or decrease cost vs the shared value. Default behavior unchanged: Forecast 4 days, Ensemble 4 days (same as today's shared default of 4).

## Non-goals

- Splitting the god validator (`OpenMeteoOptionsValidator`) and other config-pattern hygiene (SolarYield bridge, options project placement, `DefaultEnabled` cleanup) -- these are a separate follow-up change.
- Adding new Forecast-API model comparison features -- multi-model on Forecast is enabled by this change but no new feature rule is added.
- Hot-reload or `IOptionsMonitor` -- options remain `IOptions<T>`, read-once at startup.

## Capabilities

### New Capabilities
- `forecast-api-options`: Independent configuration section for the Forecast API (ForecastDays, PastHours, Models).
- `ensemble-api-options`: Independent configuration section for the Ensemble API (ForecastDays, Models).

### Modified Capabilities
- `feature-requirements-options`: Replaced by `DomainRequirements` registered as `IOptions<DomainRequirements>` via PostConfigure; `FeatureResolution` removed; model fields and `ComparisonFeatureName` eliminated.
- `domain-requirements`: Gains `WithBaseParams()` instance method and `RequiredDomains()` extension; registered as `IOptions<DomainRequirements>` singleton.
- `multi-model-weather`: Model list sourced from `EnsembleOptions` instead of `FeatureRequirementsOptions`; `WeatherModelCatalog` removed; multi-model gating moves to `EnsembleClient` via `EnsembleParams.Models` flag only.
- `per-domain-options`: Forecast and Ensemble join the existing per-domain pattern (each API has its own section with its own ForecastDays and API-specific settings).

## Impact

- **Config breaking**: `Njord:Weather:*` env vars / config keys must migrate to `Njord:Forecast:*` and `Njord:Ensemble:*`.
- **Projects touched**: Njord.Core (DomainRequirements, new options), Njord.Messages (remove FeatureResolution), Njord.OpenMeteo (all clients, remove WeatherModelCatalog), Njord.Polling (PollWorker, BudgetGuard, PollingOptionsValidator), Njord.Grpc (catalog mapping, WeatherGrpcService), Njord.Pipeline (FeatureEngine), Host (DomainSetupContainer registration), all corresponding test projects.
- **Downstream**: Config Builder (docs), docker-compose.example.yml, appsettings.Example.json, AGENTS.md.
