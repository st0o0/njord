## Why

Users must manually configure an IANA timezone per location (`Timezone` in `LocationOptions`), but Open-Meteo already knows the timezone for any coordinate pair and returns it in every response when `&timezone=auto` is sent. The manual config is redundant, error-prone, and adds friction to setup. Removing it simplifies configuration and makes the system self-describing from the data it ingests.

## What Changes

- **Add `&timezone=auto`** to Open-Meteo API requests so the response carries the real IANA timezone for the queried coordinates.
- **Deserialize the `timezone` field** from `OpenMeteoForecastResponse` and map it to a `TimeZoneInfo` on `ModelForecast`.
- **Remove `Timezone` property** from `LocationOptions` and the related `ResolveTimeZone()` method. **BREAKING** for any config that sets `Timezone` — the field is silently ignored.
- **Remove timezone validation** from `NjordOptionsValidator`.
- **`ConsensusEnrichment`** derives the timezone from the snapshot data (first forecast for a location) instead of a pre-built config dictionary.

## Non-goals

- Persisting the timezone in snapshot DTOs — it arrives fresh with every fetch.
- Adding a fallback timezone config for offline scenarios — the service requires API connectivity regardless.
- Changing any wire format (gRPC, MQTT, persistence DTOs).

## API-budget impact

Adding `&timezone=auto` does not add requests or change call weight. It is a query-string parameter on the existing forecast call. Zero budget impact.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `openmeteo-client`: The client adds `&timezone=auto` to requests and maps the response `timezone` field onto `ModelForecast`.
- `daily-consensus-aggregation`: Timezone is sourced from `ModelForecast` data instead of `LocationOptions` config. The "No timezone configured defaults to UTC" scenario changes: the fallback to UTC now applies when no forecasts are available for a location, not when no config is set.
- `service-configuration`: The `Timezone` property is removed from `LocationOptions`; timezone validation is removed from the options validator.

## Impact

- **Ingest**: `OpenMeteoDtos`, `OpenMeteoClient` — request building and response parsing.
- **Domain**: `ModelForecast` record gains a `TimeZoneInfo` field.
- **Enrichment**: `ConsensusEnrichment` — timezone lookup changes from config-driven to data-driven.
- **Config**: `LocationOptions`, `NjordOptionsValidator` — property and validation removal.
- **Tests**: `LocationOptionsSpec`, `OpenMeteoClientSpec`, `ConsensusEnrichment`-related specs, `DailyConsensusSummarySpec`, snapshot serialization verified.txt.
- **Config files**: `appsettings.Development.json` — remove any `Timezone` entries.
