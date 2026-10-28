## Context

ha-njord fires HA events only for alert state transitions (`NjordAlertEvent`). Score changes, condition flips, domain data arrivals, and activity-window lifecycle are all silent — sensors update but nothing automation-friendly fires. This limits the automations users can build.

The coordinator already calls `_handle_coordinator_update` on every subscriber when domain or enrichment data arrives. `NjordAlertEvent` demonstrates the pattern: compare previous state against current, fire events on transitions. The same approach extends to data lifecycle, score thresholds, and condition changes.

## Goals / Non-Goals

**Goals:**
- Two new event entities per location: `event.njord_<loc>_data` and `event.njord_<loc>_features`
- Data lifecycle events: `domain_updated`, `features_updated`
- Feature transition events: `score_threshold`, `condition_changed`
- Window lifecycle events (gated behind score-windows): `window_opened`, `window_closing_soon`, `window_closed`
- Configurable score thresholds via integration options flow
- All event type labels translated (EN + DE)

**Non-Goals:**
- Any njord/gRPC changes
- Composite/smart events correlating multiple features
- Calendar entities (score-windows change)
- Per-user language support

## Decisions

### 1. Two event entities, not one

Data lifecycle events (`domain_updated`, `features_updated`) and feature transition events (`score_threshold`, `condition_changed`, `window_*`) serve different automation audiences. Splitting them into separate entities lets users subscribe selectively. A single overloaded entity would force filtering in every automation.

**Alternative considered:** One entity with all event types. Rejected because HA's event trigger UI shows all event types for an entity and mixing data-level with feature-level events is noisy.

### 2. Transition detection via coordinator subscriber

Both new event entities subscribe to the coordinator (same as all other entities). On each `_handle_coordinator_update`:

- **NjordDataEvent** compares `coordinator.data.domains[(location, domain)]` timestamps and `coordinator.data.features[(location, feature)]` values against `_previous_*` snapshots.
- **NjordFeatureEvent** compares score values against threshold boundaries and condition string states against previous states.

This is zero-cost when nothing changes and O(features) per update otherwise.

### 3. Timer-based window events

`window_closing_soon` cannot be detected via state comparison — it must fire at a calculated future time. Use HA's `async_track_point_in_time(hass, callback, fire_at)`:

```
On enrichment update with active window:
  if window.start <= now < window.end AND not previously active:
    → fire window_opened
    → schedule closing_soon at (window.end - lead_time)
    → schedule window_closed at window.end

On enrichment update that shifts window.end:
    → cancel existing timers, reschedule

On enrichment update that removes the window:
    → fire window_closed immediately, cancel timers
```

Timers are tracked in a dict `_window_timers: dict[str, tuple[CALLBACK_TYPE, CALLBACK_TYPE]]` keyed by feature name, holding the cancel callbacks from `async_track_point_in_time`.

### 4. Threshold configuration in options flow

Score thresholds live in `entry.options["score_thresholds"]` as a `dict[str, list[int]]`. The options flow adds an optional "Score Thresholds" step where users can override defaults. Defaults are set per-feature in code (not in options storage) so new features get defaults automatically.

### 5. Deduplication

- **domain_updated**: track `_last_model_run: dict[(location, domain), str]`. Fire only when `model_run` differs.
- **features_updated**: track `_previous_feature_data: dict[(location, feature), Any]`. Compare serialized values; fire only for actually-changed features. Suppress the event entirely when the changed-features list is empty.

### 6. Optional localized message in event_data

Each event includes a `message` field built from translation templates and the structured data. This is convenience for notification automations. The structured fields (`feature`, `score`, `direction`, etc.) remain the authoritative data; `message` is display-only and language-dependent.

## Risks / Trade-offs

- **Event spam risk** → Mitigated by deduplication (model_run, value comparison). features_updated fires at most once per enrichment stream message, containing only features that actually changed.
- **Timer accuracy** → `async_track_point_in_time` is clock-based (not sleep-based), so drift is negligible. Rescheduling on every enrichment update handles forecast shifts.
- **Window events without score-windows** → Gate `window_*` event types behind a check for window data presence. Data + threshold events ship independently.
- **Options flow complexity** → Ship sensible defaults. Advanced threshold configuration is optional; most users never touch it.
