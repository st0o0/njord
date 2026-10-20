## Why

After the v2 redesign, the config still carries properties from the multi-model consensus era: `Models`, `Horizons`, `Parameters.Groups/Extra/Exclude`. A deep-grep audit confirmed: Horizons has zero consumers, Models is display-only, Parameters is load-bearing but over-exposed. The user-facing config should be near-minimal: location names + enrichment toggles + MQTT host. Everything else is either derived or has sensible defaults.

## What Changes

### Remove dead properties
- **BREAKING**: Remove `NjordOptions.Models` — catalog displays "Ensemble Mean" instead.
- **BREAKING**: Remove `NjordOptions.Horizons` — zero production consumers.
- Remove `LocationOptions.Models` — dead per-location model override.
- Remove all model/horizon validation from `NjordOptionsValidator`.
- Remove `CatalogModel` record and `ServiceInfoMappings.ResolveModels`.

### Internalize parameter system
- **BREAKING**: Remove `ParameterOptions` (Groups/Extra/Exclude) from config.
- New `WeatherOptions` (`Njord:Weather`) with `IncludeSolar: bool` (default false) and `ForecastDays: int` (default 4, moved from NjordOptions).
- `WeatherClient` owns its variable list internally (hardcoded standard weather vars + optional solar).
- MQTT reads the variable list from the client's published list, not from config.
- Budget weight from the client's variable count.

### Merge StorageOptions
- `StorageOptions.PersistencePath` → `PersistenceOptions.DataPath`.
- Delete `StorageOptions` class and its validator.

### Simplify NjordOptions
- After removals: `NjordOptions` has only `Locations` list.

### Update consumers
- `ServiceInfo` simplified: no model list, catalog just shows location names.
- `BudgetCalculator`: weight from `WeatherClient.ApiCallWeight` instead of resolved parameter set.
- gRPC `GetCatalog` returns locations without per-model detail.

## Non-goals

- Changing enrichment config (just done in enrichment-driven-config).
- Changing MQTT, Persistence, Polling options (they're already clean).
- API budget impact: none (same calls, same variables).

## Capabilities

### New Capabilities

- `weather-options`: WeatherOptions class with IncludeSolar toggle and ForecastDays.

### Modified Capabilities

- `service-configuration`: NjordOptions simplified to Locations only. Models, Horizons, Parameters removed.

## Impact

- **Njord.Core**: Remove ParameterDef, ParameterRegistry, ResolvedParameterSet, ParameterSelection, ParameterOptions, StorageOptions. Add WeatherOptions.
- **Njord.OpenMeteo**: WeatherClient owns variable list. Publishes variable names for MQTT.
- **Njord.Mqtt**: Reads weather variables from client, not config.
- **Njord.Grpc**: Simplified catalog response.
- **Njord (host)**: ServiceInfoMappings simplified, NjordOptions slimmed.
- **Config Builder**: Remove Models/Horizons/Parameters, add Weather.IncludeSolar.
- **docker-compose.example.yml**: Dramatically simplified.
