## Why

Two Open-Meteo APIs add complexity out of proportion to their value. The Geocoding API resolves location names at startup, but every Home Assistant user already has coordinates in their HA config and the Config Builder makes entering them trivial; the lookup adds startup fragility (a transient 429/5xx fails the whole host). The Climate Change API fetches 30 years of daily data (1991-2020) only to derive 12 monthly averages per location, at roughly 657 call-equivalents per fetch, for the ClimateAnomaly enrichment, which the planned `seasonal-outlook` change will supersede with its own forward-looking anomaly comparison.

## What Changes

- **BREAKING** `Latitude` and `Longitude` become required for every location; `Name` is a label only. Configurations relying on geocoding fail validation at startup.
- Remove geocoding: `LocationResolutionService`, `GeocodingClient`, `IGeocodingClient`, `GeocodingResponse`, `OpenMeteoRequestBuilder.WithName`, their DI registrations and JSON context entry; drop `LocationOptions.HasCoordinates`; `NjordOptionsValidator` requires valid coordinates.
- Remove the Climate Change API: `ClimateBaselineService`, `ClimateClient`, `IClimateClient`, `ClimateResponse`, `ClimateResponseMappings`, `IClimateBaselineStore`, `MonthlyBaseline`, DI registrations and JSON context entry.
- **BREAKING** Remove the `ClimateAnomaly` enrichment: `ClimateAnomalyHandler`, the `EnrichmentOptions.ClimateAnomaly` toggle (20 toggles become 19), `LifestyleKind.ClimateAnomaly`, its `LifestyleMappings` arms and the `ClimateAnomalyComputed` pipeline event, plus any gRPC/MQTT mapping that references them.
- Update `appsettings.Example.json`, `docker-compose.example.yml`, `e2e/docker-compose.e2e.yml`, the Config Builder (`docs/.vitepress/theme/builder`, `docs/data/enrichment.json`, `docs/builder.md`) and the AGENTS.md toggle count and hosted-service list.
- The `seasonal-outlook` change references `IClimateBaselineStore`; it is updated separately.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `climate-anomaly`: all requirements removed (baseline fetch, handler, anomaly event).
- `slow-domain`: the slow-fetch cache requirement is removed, as Climate was its only user.
- `service-configuration`: location coordinates are required and validated; the `ClimateAnomaly` toggle no longer exists.

## Non-goals

- Replacing climate anomaly with a built-in lookup table; `seasonal-outlook` owns this later.
- Editing the `seasonal-outlook` artifacts in this change.
- Removing `DomainKind` members (`Solar` stays in the model vocabulary).
- Migration shims for old configs beyond a clear validation error.

## API-budget impact

Polling is not altered; startup calls are removed. Climate: about 657 call-equivalents per location per fetch, so about 2k for 3 locations per fetch (up to about 60k/month if refreshed daily). Geocoding: 1 call per location without coordinates, per start. Poll traffic (weather 4 calls, others 1, per cycle) is unchanged, so projected usage against the 300k/month budget and `BudgetCalculator` is unchanged. Net effect is a saving and lower burst risk against the 600/min and 10k/day limits at startup.

## Impact

- **Njord.OpenMeteo**: 9 files deleted (geocoding 4, climate 5 incl. mapping); `OpenMeteoSetupContainer`, `OpenMeteoRequestBuilder`, `OpenMeteoJsonContext` modified.
- **Njord.OpenMeteo.Tests**: `GeocodingSpec`, `ClimateSpec` deleted; `OpenMeteoRequestMappingsSpec` updated.
- **Njord.Core**: `IClimateBaselineStore`, `MonthlyBaseline` deleted; `EnrichmentOptions`, `LocationOptions`, `NjordOptionsValidator` modified (plus `NjordOptionsValidatorSpec`).
- **Njord.Weather(.Tests)**: `ClimateAnomalyHandler` and its spec deleted; `ServiceCollectionExtensions` modified.
- **Njord.Messages(.Tests)**: `LifestyleKind`, `LifestyleMappings`, `PipelineEvent`, `LifestyleMappingsSpec`.
- **Njord.Grpc(.Tests), Njord host, IntegrationTests**: `CatalogProtoMappings`, `ServiceInfoMappings`, `NjordTestHost`, `ProtoMappingsSpec`, `WeatherGrpcServiceSpec` adjusted where they reference removed members.
- **Docs/config/e2e**: examples, Config Builder, E2E plan and compose.
