## 1. Ingest — API request and response parsing

- [x] 1.1 Change `OpenMeteoClient.BuildUri()`: `timezone=auto` → `timezone=UTC`
- [x] 1.2 Remove `TimeZoneInfo` parsing block (lines 48-59) from `OpenMeteoClient.FetchAsync()` — the `try/catch TimeZoneNotFoundException` and `tz` variable
- [x] 1.3 Remove `tz` parameter from `ModelForecast` construction in `OpenMeteoClient` (line ~71)

## 2. Domain model — remove TimeZone from ModelForecast

- [x] 2.1 Remove `TimeZoneInfo TimeZone` from `ModelForecast` record definition (`Domain/Weather/ModelForecast.cs`)
- [x] 2.2 Update `ForecastSnapshotDtos.cs` recovery — drop the `TimeZoneInfo.Utc` argument from `ModelForecast` construction (line 126)

## 3. Enrichment — remove timezone-aware logic

- [x] 3.1 Delete `ConsensusEnrichment.ExtractTimeZone()` method and its call site
- [x] 3.2 Remove `TimeZoneInfo tz` parameter from `DailyConsensusSummary.Aggregate()` — change `GroupHorizonsByDay()` to use `DateOnly.FromDateTime(utcTime.UtcDateTime)` instead of `TimeZoneInfo.ConvertTime()`
- [x] 3.3 Remove `TimeZoneInfo` parameter from `DailyConsensusSummary.FindNoonWeatherCode()` if it takes one

## 4. Tests

- [x] 4.1 Update `OpenMeteoClientSpec` — assert URL contains `timezone=UTC`, remove timezone-related assertions on `forecast.TimeZone`
- [x] 4.2 Update `FakeOpenMeteoClient` in `Njord.Tests.Shared` — drop `TimeZoneInfo.Utc` from `ModelForecast` construction
- [x] 4.3 Update `StatePayloadBuilderSpec` — drop `TimeZoneInfo.Utc` from `ModelForecast` construction
- [x] 4.4 Update `DailyConsensusSummarySpec` — remove timezone parameter from `Aggregate()` calls
- [x] 4.5 Search for any remaining `TimeZoneInfo` references in test code and remove them
- [x] 4.6 Run full test suite — all 644+ tests green

## 5. Documentation

- [x] 5.1 Update CLAUDE.md Open-Meteo API section: document `timezone=UTC` and the rationale
