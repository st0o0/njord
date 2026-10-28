# Tasks: enrichment-events

All work in `D:\GIT\ha-njord`, directory `custom_components/njord/`.

## 1. Data lifecycle event entity

- [x] Create `NjordDataEvent` in `event.py` (or a new `data_event.py`) following the `NjordAlertEvent` pattern:
  - Translation key: `data_lifecycle`
  - Event types: `domain_updated`, `features_updated`
  - `_attr_has_entity_name = True`
  - Subscribe to coordinator updates via `_handle_coordinator_update`
- [x] Implement `domain_updated` deduplication: track `_last_model_run: dict[tuple[str,str], str]` per (location, domain). Fire only when model_run differs. Extract `model_run` from `coordinator.data.domains[(location, domain)]`.
- [x] Implement `features_updated` deduplication: track `_previous_features: dict[tuple[str,str], Any]` per (location, feature). Compare serialized feature data, fire with only the changed feature keys. Suppress entirely when diff is empty.
- [x] Register `NjordDataEvent` in `async_setup_entry` (in the event platform or the appropriate platform file). One entity per location.

## 2. Feature transition event entity

- [x] Create `NjordFeatureEvent` in `event.py` (or a new `feature_event.py`):
  - Translation key: `feature_transitions`
  - Event types: `score_threshold`, `condition_changed`, `window_opened`, `window_closing_soon`, `window_closed`
  - Subscribe to coordinator updates
- [x] Implement `score_threshold` detection: on each update, compare current score values against configured thresholds. Track `_previous_scores: dict[str, int]`. For each threshold crossed, fire an event with feature, score, previous_score, direction, threshold.
- [x] Implement `condition_changed` detection: track `_previous_conditions: dict[str, str]`. Compare string state values, fire on change. Skip first update after startup (no previous state).
- [x] Register `NjordFeatureEvent` in `async_setup_entry`. One entity per location.

## 3. Window event support (gated behind score-windows)

- [x] Implement `window_opened` detection: track `_active_windows: dict[str, dict]` per feature. On enrichment update, check if current time is within a window's start..end range. Fire on inactive→active transition.
- [x] Implement `window_closing_soon` timer: on window_opened, schedule via `async_track_point_in_time(hass, callback, ends_at - timedelta(minutes=closing_soon_minutes))`. Store cancel callback in `_window_timers: dict[str, list[CALLBACK_TYPE]]`.
- [x] Implement `window_closed` timer: schedule at `ends_at`. On fire, clean up active window state.
- [x] Handle window shift: on enrichment update with changed `ends_at`, cancel existing timers and reschedule.
- [x] Handle window removal: if a previously active window disappears, fire `window_closed` immediately and cancel timers.
- [x] Gate all window event logic behind a check for TimeWindow data presence (graceful no-op when score-windows is not yet deployed).

## 4. Threshold configuration

- [x] Add `score_thresholds` to integration options schema with sensible defaults (dict[str, list[int]], default `{}`).
- [x] Load thresholds in `NjordFeatureEvent.__init__`: merge user overrides from `entry.options.get("score_thresholds", {})` with code-level defaults per feature.
- [x] Optionally add a step to the options flow for advanced threshold configuration (can be deferred to a follow-up).

## 5. Translations

- [x] Add `data_lifecycle` and `feature_transitions` event entity entries to `strings.json` under `entity.event`, with all event type state labels (see proposal for exact structure).
- [x] Add corresponding entries to `translations/en.json`.
- [x] Add German translations to `translations/de.json`:
  - data_lifecycle name: "Daten"
  - domain_updated: "Domäne aktualisiert"
  - features_updated: "Features aktualisiert"
  - feature_transitions name: "Features"
  - score_threshold: "Score-Schwellwert überschritten"
  - condition_changed: "Zustand geändert"
  - window_opened: "Fenster geöffnet"
  - window_closing_soon: "Fenster schließt bald"
  - window_closed: "Fenster geschlossen"

## Validation

```bash
# From D:\GIT\ha-njord
# Verify event entities are registered
grep -rn "NjordDataEvent\|NjordFeatureEvent" custom_components/njord/ --include="*.py"

# Verify translations
node -e "JSON.parse(require('fs').readFileSync('custom_components/njord/strings.json','utf8')); console.log('OK')"
node -e "JSON.parse(require('fs').readFileSync('custom_components/njord/translations/en.json','utf8')); console.log('OK')"
node -e "JSON.parse(require('fs').readFileSync('custom_components/njord/translations/de.json','utf8')); console.log('OK')"
```
