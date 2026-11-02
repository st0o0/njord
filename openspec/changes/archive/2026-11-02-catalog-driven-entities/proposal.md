## Why

E2E testing revealed that ha-njord registers domain sensors (PM10, wave_period, etc.) for every field a domain *could* carry, even when njord never requests that variable from Open-Meteo. Combined with non-optional proto fields that default to 0.0, this produces sensors that display false zero readings instead of being unavailable. The root cause is that ha-njord has no way to know which fields njord actually fetches -- it uses a hardcoded sensor list per domain.

## What Changes

- **Extend GetCatalog** with per-location resolved domain schemas: which domains are active and which proto field names within each domain carry data. Built from the already-resolved `DomainRequirements` at startup.
- **Mark nullable proto fields as `optional`** in `weather.proto` (both repos). Fields that may be absent because no feature requests them (`pm10`, `pm25`, `ozone`, `wave_height`, `wave_period`, `wave_direction`, `discharge`, and all other domain sample fields that are feature-dependent) become `optional double` so the wire distinguishes "0.0" from "not present". **BREAKING** for proto wire format (field presence changes).
- **Rewrite ha-njord domain sensor registration** to be catalog-driven: only create sensors for fields listed in the catalog's domain schema. Remove the hardcoded `DOMAIN_SENSORS` tuple. Use `_opt()` / `HasField` for all formerly non-optional proto fields. Make corresponding model fields nullable (`float | None`).

## Non-goals

- No changes to enrichment/feature entity registration (alerts, scores, conditions, lifestyle) -- already data-driven via streaming.
- No new features that require PM10 or wave_period (adding them to feature requirements is a separate decision).
- No changes to polling intervals, budget accounting, or API request logic.
- No changes to the WeatherEntity (forecast data is always present when the domain resolves).

## API-budget impact

Zero. This change does not alter which variables are requested from Open-Meteo or how often polling occurs.

## Capabilities

### New Capabilities
- `catalog-domain-schema`: GetCatalog includes per-location resolved domain schemas mapping each active domain to its fetched proto field names. ha-njord uses this to register only sensors with data.

### Modified Capabilities
- `grpc-v2-weather-service`: GetCatalogResponse gains a `DomainSchema` message; domain sample messages gain `optional` on fields that were previously bare scalars.

## Impact

- **Proto contracts** (`protos/njord/v2/weather.proto`, `protos/njord/v2/common.proto`): new message, field optionality changes. Both repo copies (njord, ha-njord) must stay in sync.
- **njord server** (`Njord.Grpc`): CatalogResult, CatalogProtoMappingExtensions, DomainDataProtoMappingExtensions gain schema-building logic. A new mapping from `*Params` flags to proto field names.
- **ha-njord** (`grpc_client.py`, `models.py`, `sensor.py`, `coordinator.py`): catalog parsing, nullable model fields, dynamic sensor registration from schema.
- **Tests**: njord Grpc tests for the new catalog fields, ha-njord tests for catalog-driven sensor creation and nullable field handling.
