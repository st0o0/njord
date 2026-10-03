## Context

Index scores are computed in `IndexResult.Compute` from `ConsensusSnapshot` hourly data. The current implementation averages all parameters from h0 to min(cutoffHour, 24) via `Mean24hFromConsensus`, producing a single score set per location per poll cycle. The `is_day` hourly parameter (0/1) is already available in consensus data but only used for sunshine percentage calculation. `sunrise`/`sunset` are available as daily parameters.

The enrichment pipeline flows: `ConsensusSnapshot` → `IndexEnrichment.Compute` → `EgressEvent.EnrichmentUpdate` → `StatePayloadBuilder.FromIndices` → single MQTT state message on `njord/<location>/indices`. Discovery registers one device per location with 34+ sensor components (8 scores × 4 fields + frost + VPD).

## Goals / Non-Goals

**Goals:**
- Compute index scores per calendar day (d0/d1/d2) with time-of-day filtering
- Activity scores use daylight hours only; Night Ventilation uses nighttime only; utility scores use full days
- One state topic per day offset, enabling HA automations per day
- Preserve existing scorer formulas — only the aggregation window changes

**Non-Goals:**
- Intra-day slices (morning/afternoon) — future work
- Forecast horizons beyond 3 days
- Changes to Frost Protection or VPD computation
- Changes to alert evaluation

## Decisions

### D1: Day boundaries use UTC midnight, not local time

Calendar days are defined as UTC midnight to UTC midnight, matching the API's `timezone=UTC` setting. The `is_day` parameter within each day determines daylight vs. nighttime hours.

**Why over local time**: All forecast timestamps are UTC epoch seconds with `timezone=UTC`. Converting to local time would require a timezone per location (not currently in config) and introduce DST edge cases. UTC midnight alignment is consistent and matches how Open-Meteo delivers daily parameters.

**Alternative considered**: Using `sunrise`/`sunset` daily values as exact day boundaries. Rejected because `is_day` hourly already encodes this as a per-hour binary flag, which is simpler to filter on and doesn't require timestamp parsing of the daily time strings.

### D2: New `DaySlice` record as the unit of per-day scoring

A `DaySlice` record holds the aggregated parameter means for one calendar day and one time window (day/night/full). `IndexResult.Compute` produces a `DaySliceIndexResult` containing a list of `DayScoreSet` (one per day d0/d1/d2), plus the existing `FrostProtection` and `Vpd` (unchanged, computed once).

```
DaySliceIndexResult
├── Location: string
├── Days: IReadOnlyList<DayScoreSet>    // [d0, d1, d2]
│   └── DayScoreSet
│       ├── DayOffset: int              // 0, 1, 2
│       ├── Laundry, Outdoor, Running, Cycling, Bbq, Irrigation, Solar: int
│       ├── NightVentilation: int
│       ├── *Envelope for each score
│       └── HoursIncluded: int          // transparency for "today" shrinking
├── FrostProtection: FrostProtectionInfo?
└── Vpd: VpdInfo?
```

**Why a list, not d0/d1/d2 properties**: A list scales cleanly if we later add more days. It maps naturally to per-day MQTT topics and discovery iteration.

### D3: `TimeSliceAggregator` — pure function for filtered parameter means

A new static class `TimeSliceAggregator` in `Domain/Analysis/` provides:
- `AggregateDaySlices(ConsensusSnapshot, TimeProvider) → IReadOnlyList<DaySlice>`
- Each `DaySlice` contains: `DayOffset`, `DayMeans` (is_day > 0.5 hours), `NightMeans` (is_day ≤ 0.5 hours), `FullDayMeans` (all hours), and `DaylightHoursCount`/`NighttimeHoursCount`.

The aggregator resolves which consensus hours (h0, h1, ...) belong to which calendar day relative to `timeProvider.GetUtcNow()`, then partitions each day's hours into day/night using the `is_day` consensus parameter.

**Hour-to-day mapping**: Given `now = timeProvider.GetUtcNow()`, compute `todayMidnight = now.Date` (UTC). Hour hN maps to `now + N hours`. Day offset = `floor((now + N hours - todayMidnight).TotalDays)`. Hours before `now` (negative offset from today's midnight — not possible since h0 = now) are excluded.

**"Today" shrinks naturally**: If it's 20:00 UTC, today's daytime hours (say 06:00–20:00 = h0 only if is_day) may already be mostly past. The score is computed from whatever daylight hours remain. `HoursIncluded` communicates this honestly.

### D4: NightVentilation replaces Ventilation

The `Ventilation` scorer method is renamed to `NightVentilation`. The formula is identical (outdoor-indoor temp delta, humidity, wind, rain) but receives nighttime-only means. The score name in preferences, discovery, and state payloads changes from `ventilation` to `night_ventilation`.

**Why rename, not keep both**: A 24h Ventilation score is misleading in summer — daytime heat dominates and the score says "don't open windows" even though nighttime would be perfect. Keeping both adds entity clutter without clear user value.

### D5: MQTT topic structure — one state topic per day offset

Current: `njord/<location>/indices` (single topic, single JSON)

New: `njord/<location>/indices/d0`, `njord/<location>/indices/d1`, `njord/<location>/indices/d2`

Each topic gets a JSON payload with the same structure as today (score fields + envelopes), plus `hours_included`. Frost and VPD go on `d0` only (they are "from now" values, not per-day).

**Discovery**: The indices device keeps one device per location. Components gain a day-offset dimension: `outdoor_d0`, `outdoor_d1`, `outdoor_d2`, each pointing at its respective state topic. This triples the score components (8→24 scores, 24→72 envelope fields) but adds meaningful comparison capability. Frost/VPD components remain on d0 topic.

**Alternative considered**: Separate devices per day (njord_lucerne_indices_d0, etc.). Rejected because HA groups entities by device — having all indices on one device lets users see the 3-day view in one place.

### D6: IndexResult record becomes DaySliceIndexResult

The `IndexResult` record is replaced by `DaySliceIndexResult`. This is a **breaking change** to the wire format. The persistence DTO version must be bumped. Old `IndexResult` snapshots must remain deserializable (recovery handles version ≥ 1).

### D7: Score name mapping in PreferenceResolver

`PreferenceResolver.ScoreNames` changes: `"Ventilation"` → `"NightVentilation"`. The cascade resolution logic is unchanged. Config key `Ventilation` in `ScoreOverrides` silently stops matching (logged as unknown score warning, consistent with existing behavior for unknown keys).

## Risks / Trade-offs

- **Entity count increase** → 3× score sensors per location. For 2 locations: ~204 index sensors (vs ~68 today). HA handles this fine but users see more entities in the UI. **Mitigation**: Good entity naming (`Outdoor Today`, `Outdoor Tomorrow`, etc.) and suggested HA dashboard grouping in docs.

- **"Today" score instability late in the day** → With few remaining daylight hours, the today-daytime score may be volatile (one remaining hour dominates). **Mitigation**: `hours_included` attribute lets HA templates/automations ignore scores with < N hours. Could add a minimum-hours threshold later.

- **Breaking config change (Ventilation → NightVentilation)** → Users with `ScoreOverrides.Ventilation` in config lose their overrides silently. **Mitigation**: Unknown score key already logs a warning. Document in release notes.

- **Breaking MQTT topic change** → Old topic `njord/<location>/indices` becomes orphaned (retained). New topics are `njord/<location>/indices/d0` etc. **Mitigation**: Discovery cleanup (tombstone old topic with empty retained message) on first startup after upgrade.

## Open Questions

- Should `hours_included` below a threshold (e.g., < 3 daylight hours) suppress the score entirely (publish `unavailable` instead)? Leaning yes with a configurable threshold, but could also leave it to HA templates.
