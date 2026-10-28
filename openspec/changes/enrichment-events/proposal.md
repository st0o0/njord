# Enrichment Events

## Summary

Add data lifecycle and feature transition event entities to ha-njord so that HA automations can trigger on new data arrivals, score threshold crossings, condition state changes, and activity window transitions. This is a ha-njord-only change; njord sends no additional data.

## Motivation

Today only alert state transitions fire HA events (`NjordAlertEvent`: `alert_started`, `alert_escalated`, `alert_deescalated`, `alert_cleared`). All other data updates -- scores, conditions, trends, domain refreshes -- silently update sensor values. Users have no automation-friendly way to react to:

- Fresh forecast data arriving for a location.
- A score crossing a meaningful threshold (e.g. allergy severity rising above 70).
- A condition flipping state (e.g. inversion detected / resolved).
- An activity window opening, closing soon, or closing.

These events unlock a new class of automations: "notify me when good jogging weather starts", "switch ventilation to recirculate when inversion is detected", "remind me to bring in laundry 30 minutes before the window closes".

## Scope

**In scope:**

- Two new event entities per location in ha-njord.
- Transition detection logic following the existing alert-event pattern.
- Timer-based pre-closing events for activity windows.
- Configurable score thresholds via integration options.
- i18n for event type labels via `strings.json` / `translations/<lang>.json`.

**Out of scope:**

- Any njord (backend) changes -- all logic lives in ha-njord.
- Calendar entities and the window data model itself (see `score-windows`).
- Composite / smart events that combine multiple features (e.g. "weather shift", "compound risk") -- future work.

## Design

### Data Events

A new event entity `event.njord_<location>_data` fires on raw data lifecycle transitions.

**Event types:**

| Type | When | event_data |
|------|------|------------|
| `domain_updated` | A domain stream message delivers a new model run for a location | `location`, `domain`, `model_run` (ISO timestamp), `updated_at` |
| `features_updated` | One or more enrichment features change value for a location | `location`, `features` (list of feature keys that changed), `updated_at` |

**Deduplication:** `domain_updated` tracks the last `model_run` timestamp per `(location, domain)`. A stream message whose `model_run` matches the previous one is a redelivery and does not fire an event. `features_updated` compares the serialized feature data against the previous snapshot; unchanged features are excluded from the `features` list, and the event is suppressed entirely when nothing changed.

**Implementation:** A new `NjordDataEvent` entity class subscribing to the coordinator. On `_handle_coordinator_update`, it compares the incoming domain/feature data against its `_previous_*` snapshots (same pattern as `NjordAlertEvent`).

### Feature Transition Events

A new event entity `event.njord_<location>_features` fires on meaningful feature state changes.

**Event types:**

| Type | When | event_data |
|------|------|------------|
| `score_threshold` | A score sensor crosses a configured threshold | `feature`, `score`, `direction` (`up` / `down`), `threshold` |
| `condition_changed` | A condition sensor's primary state value changes | `feature`, `from_state`, `to_state` |
| `window_opened` | The current time enters a window's start..end range | `feature`, `score`, `ends_at`, `limiting_kind`, `limiting_value` |
| `window_closing_soon` | A configurable lead time before a window's end | `feature`, `minutes_remaining`, `limiting_kind` |
| `window_closed` | The current time passes a window's end | `feature` |

**Transition detection:** On each coordinator update, the entity compares the current feature state map against `_previous_features`. For scores, it checks each configured threshold for crossings. For conditions, it compares the string state directly. Window events use a different mechanism (see below).

### Window Events

Window events require timer-based scheduling rather than pure state comparison, because `window_closing_soon` must fire at a calculated future time.

**Flow:**

1. On enrichment update containing a window (from `score-windows` change), ha-njord compares the new window against the previous one.
2. If a new window is now active (current time within start..end) and was not before: fire `window_opened`.
3. Schedule `window_closing_soon` at `ends_at - closing_soon_minutes` using `async_track_point_in_time`.
4. Schedule `window_closed` at `ends_at`.
5. If a subsequent enrichment shifts `ends_at`, cancel existing timers and reschedule.
6. If the window disappears (e.g. rain moved earlier), fire `window_closed` immediately and cancel timers.

**Configuration:** `closing_soon_minutes` is an integration option (default: 30). Per-feature override is possible but not required for the initial implementation.

### Threshold Configuration

Score thresholds are configured in the integration's options flow, stored per feature:

```
score_thresholds:
  exercise: [60, 80]
  allergy_severity: [50, 70]
  laundry: [70]
```

Each value is a boundary. When the score crosses any boundary in either direction, a `score_threshold` event fires with the crossed `threshold` and the `direction`. Defaults are provided per feature category:

- Score features: `[50, 80]` (below average, above good)
- Alert-derived scores: `[30, 70]` (low risk, high risk)

Users can override in the integration's options UI.

### i18n

Event type labels are declared in `strings.json` under the event entity's `translation_key`, following the existing `weather_alert` pattern:

```json
{
  "entity": {
    "event": {
      "data_lifecycle": {
        "name": "Data",
        "state_attributes": {
          "event_type": {
            "state": {
              "domain_updated": "Domain updated",
              "features_updated": "Features updated"
            }
          }
        }
      },
      "feature_transitions": {
        "name": "Features",
        "state_attributes": {
          "event_type": {
            "state": {
              "score_threshold": "Score threshold crossed",
              "condition_changed": "Condition changed",
              "window_opened": "Window opened",
              "window_closing_soon": "Window closing soon",
              "window_closed": "Window closed"
            }
          }
        }
      }
    }
  }
}
```

`event_data` contains structured data (enum keys, numbers, timestamps) -- never pre-localized strings. An optional `event_data.message` field carries a notification-friendly localized string built by ha-njord from the translation files and the structured data, for use in notification automations without template gymnastics.

## Non-goals

- Changes to the njord backend or gRPC protocol.
- Composite events that correlate multiple features ("weather is shifting", "compound risk rising").
- Calendar entity support (handled by `score-windows`).
- Per-user language selection (HA uses a single installation language; per-user is a HA platform limitation).
- Actionable notifications (HA notification actions are user-automation territory, not integration territory).

## Dependencies

- **ha-njord-i18n-foundation**: The translation_key pattern must be established before adding new translated entities.
- **score-windows** (partial): `window_opened`, `window_closing_soon`, and `window_closed` events require the `TimeWindow` data from `score-windows`. The data lifecycle events and `score_threshold` / `condition_changed` events are independent and can ship first.

## Risks

| Risk | Mitigation |
|------|------------|
| Event spam on frequent polls | Deduplication via model_run and state comparison; `features_updated` only fires when values actually change |
| Timer drift for `window_closing_soon` | Use HA's `async_track_point_in_time` (clock-based, not sleep-based); reschedule on every enrichment update |
| Threshold configuration complexity | Ship sensible defaults; only expose override in advanced options |
| Window events without `score-windows` landed | Gate window event types behind a feature check; ship data + threshold events independently |
