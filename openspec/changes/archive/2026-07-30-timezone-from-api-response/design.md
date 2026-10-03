## Context

Timezone is currently a manual config property on `LocationOptions`. It's consumed in one place: `ConsensusEnrichment` builds a `Dictionary<string, TimeZoneInfo>` at construction from config, then passes the timezone to `DailyConsensusSummary.Aggregate` for calendar-day bucketing.

Open-Meteo returns `timezone` (IANA id) and `utc_offset_seconds` in every response. When `&timezone=auto` is sent, the API resolves the timezone from the coordinates. Currently the DTO doesn't deserialize these fields.

## Goals / Non-Goals

**Goals:**
- Timezone arrives with the data — no user configuration needed.
- `ModelForecast` carries its `TimeZoneInfo`, making it self-describing.
- `LocationOptions.Timezone` and related validation are removed entirely.

**Non-Goals:**
- Persisting timezone in snapshot DTOs — it arrives fresh every poll cycle.
- Handling offline timezone resolution — the service requires API connectivity.
- Changing any external contract (gRPC, MQTT payloads, persistence format).

## Decisions

### D1: Timezone on ModelForecast, not on ModelSnapshot

**Choice:** Add `TimeZoneInfo TimeZone` to the `ModelForecast` record.

**Why:** `ModelForecast` is the natural data carrier — it represents one API response for one location+model. The timezone is an attribute of the response, not a snapshot-level concern. All models for the same location return the same timezone, so there's redundancy, but it keeps the type self-contained and avoids a separate lookup structure.

**Alternative:** A `Dictionary<string, TimeZoneInfo>` on `ModelSnapshot`. Rejected because it adds a parallel data structure that must be kept in sync with the entries dictionary — the current config-driven approach already has this shape and we're explicitly moving away from it.

### D2: Parse timezone string in OpenMeteoClient, not in domain

**Choice:** `OpenMeteoClient` calls `TimeZoneInfo.FindSystemTimeZoneById` on the response's `timezone` string and passes the resolved `TimeZoneInfo` to `ModelForecast`.

**Why:** Keeps timezone resolution at the system boundary (ingest). If the API returns an unrecognized timezone id, the fetch fails as `MalformedPayload` — the same path as any other unparseable response. The domain layer never deals with timezone strings.

### D3: ConsensusEnrichment extracts timezone from snapshot entries

**Choice:** When computing consensus for a location, `ConsensusEnrichment` picks the `TimeZoneInfo` from the first `ModelForecast` entry for that location. Falls back to `TimeZoneInfo.Utc` if no entries exist (defensive — shouldn't happen in practice since consensus requires ≥2 models).

**Why:** Replaces the config-driven `_locationTimeZones` dictionary with a data-driven lookup. All models for the same coordinates return the same timezone, so any entry works.

### D4: FakeOpenMeteoClient and test helpers default to UTC

**Choice:** `FakeOpenMeteoClient` and test builders produce `ModelForecast` with `TimeZoneInfo.Utc` unless a test explicitly needs a specific timezone.

**Why:** Most tests don't care about timezone. Only `DailyConsensusSummarySpec` and specific `ConsensusEnrichment` tests need non-UTC timezones.

## Risks / Trade-offs

- **[Risk] Open-Meteo returns an unknown timezone id** → `TimeZoneInfo.FindSystemTimeZoneById` throws → caught as `MalformedPayload` failure. Acceptable: if the system doesn't know the timezone, the forecast data is unusable for day-bucketing anyway.
- **[Risk] Breaking config change for users who set `Timezone`** → The field is silently ignored (unknown JSON properties are ignored by the binder). No crash, but the behavior changes: timezone now comes from the API, which should return the same value. Low risk since the only config source is `appsettings.Development.json` under our control.
- **[Trade-off] Redundant timezone on every ModelForecast** → Accepted. The memory cost is negligible (one `TimeZoneInfo` reference per forecast), and self-contained records are easier to reason about.
