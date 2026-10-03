## Why

The codebase mixes UTC and local timezone in inconsistent ways. Open-Meteo's `timezone=auto` returns unix timestamps aligned to midnight *local time*, not midnight UTC — even with `timeformat=unixtime`. This causes off-by-one day errors for daily forecasts in UTC+ timezones (e.g. midnight CEST = 22:00 UTC previous day). The `TimeZoneInfo` parsed from the API response threads through domain, enrichment, and persistence layers adding complexity and a known data-loss bug on actor recovery (timezone hardcoded to UTC after deserialization).

Switching the API request to `timezone=UTC` makes all timestamps true UTC at the source, eliminating timezone arithmetic from the entire domain. Verified via live API probes: values are identical, only timestamp alignment changes.

## What Changes

- Open-Meteo request URL: `timezone=auto` → `timezone=UTC`
- Remove `TimeZoneInfo` parsing from `OpenMeteoClient` API response handling
- Remove `ModelForecast.TimeZone` property and all code that reads it
- Remove `ConsensusEnrichment.ExtractTimeZone()` and timezone-aware grouping in `DailyConsensusSummary`
- Remove timezone recovery hack in `ForecastSnapshotDtos` (hardcoded `TimeZoneInfo.Utc`)
- Sunrise/sunset ISO strings become UTC-aligned (HA handles UTC natively)
- **BREAKING**: `ModelForecast` record loses its `TimeZoneInfo TimeZone` field — any downstream consumer that relied on it must use its own timezone source

## Capabilities

### New Capabilities

- `utc-timestamps`: All timestamps throughout the system are UTC — from API ingest through domain, enrichment, persistence, and egress. No timezone conversion logic in the domain.

### Modified Capabilities

## Impact

- **Ingest**: `OpenMeteoClient.cs` — URL construction, response parsing, `MapDaily`, timezone parsing block
- **Domain**: `ModelForecast.cs` — remove `TimeZone` property; `DailyConsensusSummary.cs` — simplify grouping
- **Enrichment**: `ConsensusEnrichment.cs` — remove `ExtractTimeZone()`
- **Persistence**: `ForecastSnapshotDtos.cs` — remove timezone recovery; snapshot DTO version may need increment
- **Tests**: Any test asserting on timezone behavior or constructing `ModelForecast` with a `TimeZoneInfo`
- **CLAUDE.md**: Update Open-Meteo API section to document `timezone=UTC`
