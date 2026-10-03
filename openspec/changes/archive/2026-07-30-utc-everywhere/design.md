## Context

njord polls the Open-Meteo API with `timezone=auto&timeformat=unixtime`. Despite requesting unix timestamps (which are inherently UTC), the API aligns the time series to midnight in the *location's local timezone*. For Europe/Berlin (UTC+2), the first daily timestamp is `2026-07-29T22:00:00Z` — midnight CEST expressed as epoch — not midnight UTC. This causes `DateOnly.FromDateTime(dto.UtcDateTime)` to produce the previous day's date.

The `TimeZoneInfo` parsed from the API response threads through `ModelForecast` → `ConsensusEnrichment.ExtractTimeZone()` → `DailyConsensusSummary.GroupHorizonsByDay()`, adding complexity. On persistence recovery the timezone is lost (hardcoded to `TimeZoneInfo.Utc` in `ForecastSnapshotDtos`), making post-recovery daily grouping subtly wrong.

Live API probes confirmed: switching to `timezone=UTC` makes all timestamps (hourly, daily, sunrise/sunset) align to midnight UTC. Returned weather values are identical.

## Goals / Non-Goals

**Goals:**

- All timestamps in the domain are UTC with zero offset — no timezone arithmetic anywhere between ingest and egress
- Remove `TimeZoneInfo` as a domain concept — it does not appear in `ModelForecast`, enrichment, or persistence
- Eliminate the persistence timezone-loss bug by removing the need to persist timezone
- Simplify `DailyConsensusSummary` grouping to pure UTC date arithmetic

**Non-Goals:**

- Adding local-timezone display conversion at the egress layer — HA natively handles UTC timestamps and the consumer is responsible for local conversion
- Changing the `dto.Timezone` field in the Open-Meteo response DTO — the field will still be present in the JSON (`"GMT"`), we just stop parsing it into a `TimeZoneInfo`
- Persisting location timezone for future egress use — out of scope; if needed later it belongs in `LocationOptions` config, not the API response

## Decisions

### D1: `timezone=UTC` in API request

Change `OpenMeteoClient.BuildUri()` from `timezone=auto` to `timezone=UTC`. This makes the API return all time series aligned to midnight UTC. The `timezone` field in the response becomes `"GMT"` — we ignore it.

**Alternative considered:** Keep `timezone=auto` and convert timestamps at ingest. Rejected because it requires threading `TimeZoneInfo` through the domain and leaves the persistence-loss bug in place.

### D2: Remove `ModelForecast.TimeZone` property

The record changes from 7 fields to 6. All construction sites (production ingest, persistence recovery, test fakes) drop the `TimeZoneInfo` argument.

**Alternative considered:** Keep the field but always set it to `TimeZoneInfo.Utc`. Rejected because a field that is always the same value is dead weight and misleads readers into thinking timezone matters.

### D3: Simplify `DailyConsensusSummary` to UTC grouping

`GroupHorizonsByDay()` currently converts each horizon's UTC time to local time via `TimeZoneInfo.ConvertTime()` before extracting `DateOnly`. With UTC-only timestamps, the conversion is removed — `DateOnly.FromDateTime(utcTime.UtcDateTime)` gives the correct UTC date directly.

The `Aggregate()` method loses its `TimeZoneInfo tz` parameter. `ConsensusEnrichment.ExtractTimeZone()` is deleted entirely.

### D4: Remove timezone parsing from `OpenMeteoClient`

The `try/catch TimeZoneNotFoundException` block (lines 48-59) and the `tz` variable are removed. The `TimeZoneInfo.FindSystemTimeZoneById()` call — which can fail on systems with different timezone databases — is eliminated.

### D5: Persistence — no migration needed

`ForecastSnapshotDtos` already hardcodes `TimeZoneInfo.Utc` on recovery (line 126). Removing the `TimeZone` parameter from `ModelForecast` means the recovery code simply drops that argument. No DTO version bump needed because the serialized snapshot format does not include timezone — it was always reconstructed, never persisted.

## Risks / Trade-offs

**[Risk] Daily "date" semantics shift** → With `timezone=auto`, daily date "July 30" meant July 30 in local time. With `timezone=UTC`, it means July 30 UTC. For locations at UTC+12 (e.g. New Zealand), the local date could be up to one day ahead of the UTC date. This is acceptable because all comparisons within the domain use UTC consistently, and HA consumers apply their own timezone.

**[Risk] Hourly series window shifts by UTC offset** → `forecast_days=4` with `timezone=UTC` starts at midnight UTC, not midnight local. A user at UTC+2 loses 2 hours of "today" coverage at the start and gains 2 hours at the end. The effect is negligible given the 4-day window and hourly granularity.

**[Risk] Test assertions on timezone** → `OpenMeteoClientSpec` asserts on `forecast.TimeZone`. These tests need updating. The fake client in `Njord.Tests.Shared` also passes `TimeZoneInfo.Utc` — it drops the argument entirely.
