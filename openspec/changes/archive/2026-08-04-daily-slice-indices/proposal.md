## Why

Index scores (Outdoor, BBQ, Running, etc.) currently average forecast parameters over a rolling 24h window from "now." This makes scores unstable — they shift every poll cycle as the window slides — and prevents users from comparing days ("Is tomorrow better for cycling than today?"). The rolling mean also mixes day and night hours, diluting activity scores with irrelevant nighttime data and making the Ventilation score useless in summer (daytime heat drowns out the nighttime cooling signal that actually matters).

## What Changes

- **Daily slices instead of rolling window**: Scores are computed per calendar day (today, tomorrow, day-after-tomorrow) instead of a single rolling h0–h24 mean. Each slice produces its own full score set with envelopes.
- **Time-of-day awareness**: Activity scores (Outdoor, Running, Cycling, BBQ, Solar) average only daylight hours (`is_day > 0.5`). Utility scores (Laundry, Irrigation) average full calendar days.
- **Night Ventilation**: The existing Ventilation score is replaced by a Night Ventilation score that averages only nighttime hours (`is_day ≤ 0.5`). Same formula (outdoor-indoor delta, humidity, wind, rain) but evaluated over the window where it actually matters.
- **Three-day comparison**: Users see scores for today, tomorrow, and day-after-tomorrow side by side, enabling planning ("Best BBQ day this week is tomorrow").
- **Frost Protection and VPD unchanged**: Frost stays a countdown (hours until frost, confidence). VPD stays a momentary snapshot. Neither is day-sliced.

## Non-goals

- **Longer forecast horizons** (>3 days): Model confidence degrades significantly beyond 72h; three days is the useful planning window.
- **Intra-day slices** (morning/afternoon/evening): Could be added later but adds entity complexity for limited user value.
- **New scoring formulas**: The scorer weights and sub-score calculations remain identical — only the input aggregation window changes.
- **API request changes**: No additional Open-Meteo parameters or calls needed. `is_day` and `sunrise`/`sunset` are already requested and present in consensus data.

## Capabilities

### New Capabilities

- `daily-slice-scoring`: Daily time-slice aggregation logic — splitting consensus hourly data into calendar-day slices with day/night filtering via `is_day`, producing per-slice parameter means for scorer input.
- `night-ventilation`: Night Ventilation score computed from nighttime-only hours, replacing the current 24h Ventilation score.

### Modified Capabilities

- `activity-indices`: IndexResult changes from a single score set to three daily score sets. Score computation uses day-filtered means instead of rolling 24h means. Ventilation is replaced by Night Ventilation. Entity count per location changes (3× day slices × scores).
- `index-preferences`: PreferenceResolver and ResolvedPreferences must support the renamed Ventilation → NightVentilation score name.

## Impact

- **Domain**: `IndexResult`, `IndexScorer` (new `NightVentilation` method, `Ventilation` removed), new `DaySlice`/`TimeSliceAggregator` types in `Domain/Analysis/`.
- **Enrichment**: `IndexEnrichment` iterates over day slices instead of computing a single score set.
- **Egress**: Discovery payload for the indices device changes — sensors are now prefixed/grouped by day offset (d0/d1/d2). State topic structure changes to include day offset.
- **Config**: `IndexOptions` score name `Ventilation` renamed to `NightVentilation`. **BREAKING** for users with existing Ventilation overrides in config — migration note needed.
- **Tests**: All `IndexResultSpec` and `IndexEnrichmentSpec` tests need updating for the new multi-slice output. `IndexScorerSpec` tests for Ventilation renamed to NightVentilation.
- **HA entities**: Entity IDs change (gain day-offset suffix). Users will see new entities and orphaned old ones until discovery cleanup.
