# Enrichment Events Spec

## Event Entities

Two new event entities per location, registered alongside the existing `NjordAlertEvent`.

### event.njord_<location>_data

Translation key: `data_lifecycle`. Event types:

#### domain_updated

Fires when a domain stream message delivers a genuinely new model run for a location.

| Field | Type | Description |
|-------|------|-------------|
| location | string | Location name |
| domain | string | Domain key (forecast, air_quality, flood, marine, seasonal, satellite, ensemble) |
| model_run | string | ISO 8601 timestamp of the model run |
| updated_at | string | ISO 8601 timestamp when ha-njord received it |
| message | string | Localized display string, e.g. "Forecast data updated (model run 12:00 UTC)" |

Does NOT fire on redelivery (same model_run as previous for this location+domain).

#### features_updated

Fires when one or more enrichment feature values actually change for a location.

| Field | Type | Description |
|-------|------|-------------|
| location | string | Location name |
| features | list[string] | Feature keys whose values changed |
| count | int | Number of changed features |
| updated_at | string | ISO 8601 timestamp |
| message | string | Localized display string, e.g. "3 features updated: exercise, laundry_score, frost_alert" |

Suppressed entirely when no feature values actually changed (deduplication).

### event.njord_<location>_features

Translation key: `feature_transitions`. Event types:

#### score_threshold

Fires when a score sensor crosses a configured threshold boundary.

| Field | Type | Description |
|-------|------|-------------|
| feature | string | Feature key (e.g. "exercise", "laundry_score") |
| score | int | Current score value |
| previous_score | int | Previous score value |
| direction | string | "up" or "down" |
| threshold | int | The threshold that was crossed |
| message | string | Localized, e.g. "Exercise score crossed above 80 (now 85)" |

Fires once per threshold crossed. If a score jumps from 40 to 90 and thresholds are [50, 80], two events fire (one for 50-up, one for 80-up).

#### condition_changed

Fires when a condition sensor's primary state value changes.

| Field | Type | Description |
|-------|------|-------------|
| feature | string | Feature key |
| from_state | string | Previous state value |
| to_state | string | New state value |
| message | string | Localized, e.g. "Commute safety changed from safe to caution" |

Does not fire on the first update after startup (no previous state to compare).

#### window_opened

Fires when the current time enters an activity window's start..end range for the first time.

| Field | Type | Description |
|-------|------|-------------|
| feature | string | Feature key |
| score | int | Window score |
| ends_at | string | ISO 8601 timestamp when the window ends |
| limiting_kind | string | What limits the window (rain, uv, wind, etc.) |
| limiting_value | float | The limiting value |
| message | string | Localized, e.g. "Exercise window opened (score 87, ends at 16:00)" |

Depends on score-windows change providing TimeWindow data.

#### window_closing_soon

Fires a configurable number of minutes before a window ends.

| Field | Type | Description |
|-------|------|-------------|
| feature | string | Feature key |
| minutes_remaining | int | Minutes until window closes |
| ends_at | string | ISO 8601 timestamp |
| limiting_kind | string | Why the window is ending |
| message | string | Localized, e.g. "Exercise window ends in 30 minutes (rain expected)" |

Implemented via `async_track_point_in_time` timer. Rescheduled when enrichment updates shift `ends_at`. Cancelled when the window disappears.

#### window_closed

Fires when the current time passes a window's end, or when an enrichment update removes an active window.

| Field | Type | Description |
|-------|------|-------------|
| feature | string | Feature key |
| message | string | Localized, e.g. "Exercise window closed" |

## Threshold Configuration

Score thresholds are stored in `entry.options["score_thresholds"]` as `dict[str, list[int]]`.

Defaults (applied when no user override exists):
- Score features (laundry, outdoor, bbq, exercise, surf, allergy_severity, asthma_risk, sleep_quality): `[50, 80]`
- Other score features: `[50, 80]`

Users can override per-feature via the integration's options flow.

## Deduplication Rules

- **domain_updated**: Track last model_run per (location, domain). Same model_run = suppress.
- **features_updated**: Compare current vs previous feature data per (location, feature). Only include features whose serialized value differs. Empty diff = suppress event.
- **score_threshold**: Track previous score per feature. Only fire when a threshold boundary is actually crossed (not when score changes within the same band).
- **condition_changed**: Track previous state string per feature. Only fire on actual state change. Skip first update after startup.
- **window_opened/closed**: Track active window per feature. Only fire on transition (inactive→active, active→inactive).
