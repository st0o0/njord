# Score Windows

## Summary

Extend `ScoreData` with a repeated `TimeWindow` message so score features can report ranked activity windows (start, end, score, limiting factor). Add a Calendar entity, window binary sensors, and window lifecycle events to ha-njord so users see when to act and get notified before a window closes.

## Motivation

Score features already compute a single best-start/best-hours pair, but users need richer answers: "what are my top 3 windows for jogging this week?", "why does the window end?", and "remind me before it closes." No other HA weather add-on offers activity-window intelligence with calendar integration and proactive notifications.

The data is already there -- hourly forecast, air quality, and ensemble data cover 48-96 hours. The gap is structured output and HA entity representation.

## Scope

### njord

- New proto messages: `TimeWindow`, `LimitingFactor`, `LimitingKind` enum.
- Additive extension of `ScoreData` (new `repeated TimeWindow windows` field; existing fields untouched).
- Sliding-window algorithm as a shared utility in `Njord.Features/Shared/`.
- Existing score features (Laundry, Outdoor, Bbq) updated to populate windows.

### ha-njord

- New `calendar.py` platform: one Calendar entity per location aggregating all window-enabled features.
- Binary sensor per window-enabled feature: ON when inside the best window.
- Score sensor attributes extended: `windows`, `next_start`, `next_end`, `limiting_factor`.
- Window lifecycle events: `window_opened`, `window_closing_soon`, `window_closed`.
- Translation entries for all new entities, enum states, and calendar summaries (EN + DE).

## Design

### njord Proto

Additive changes to `weather.proto`:

```protobuf
enum LimitingKind {
  LIMITING_KIND_UNSPECIFIED = 0;
  LIMITING_KIND_RAIN = 1;
  LIMITING_KIND_UV = 2;
  LIMITING_KIND_WIND = 3;
  LIMITING_KIND_TEMPERATURE_HIGH = 4;
  LIMITING_KIND_TEMPERATURE_LOW = 5;
  LIMITING_KIND_HUMIDITY = 6;
  LIMITING_KIND_POLLEN = 7;
  LIMITING_KIND_AIR_QUALITY = 8;
  LIMITING_KIND_VISIBILITY = 9;
  LIMITING_KIND_THUNDERSTORM = 10;
  LIMITING_KIND_DARKNESS = 11;
}

message LimitingFactor {
  LimitingKind kind = 1;
  google.protobuf.Timestamp from = 2;
  double value = 3;
}

message TimeWindow {
  google.protobuf.Timestamp start = 1;
  google.protobuf.Timestamp end = 2;
  int32 score = 3;
  LimitingFactor limiting = 4;
}
```

`ScoreData` gains one field:

```protobuf
message ScoreData {
  int32 score = 1;
  google.protobuf.Timestamp best_start = 2;
  int32 best_hours = 3;
  repeated TimeWindow windows = 4;  // NEW -- top-N ranked windows
}
```

Backward compatible: consumers that ignore field 4 are unaffected.

### njord Feature Algorithm

A shared `WindowScanner` utility in `Njord.Features/Shared/`:

1. **Score each forecast hour** for the activity using the feature's domain-specific scoring function. Input: hourly forecast points for the next 48 hours (configurable via `WindowHorizonHours`).
2. **Find contiguous regions** where hourly score exceeds `MinScore` (feature config, default 50).
3. **Extract best sub-window** of `MinWindowHours` (feature config, default 2) within each region by taking the sub-range with the highest minimum score.
4. **Identify limiting factor** at each window boundary: the score component with the largest negative contribution at the hour where the window ends. Map to `LimitingKind` enum + timestamp + raw value.
5. **Rank and trim** to `MaxWindows` (feature config, default 3) by score descending.

Each score feature calls `WindowScanner.Scan(hourlyScores, config)` and maps the result to `repeated TimeWindow`. The feature's `Evaluate` still returns a `ScoreFeatureResult`; the gRPC mapping layer reads the windows from the result and populates `ScoreData.windows`.

### ha-njord Calendar

New `calendar.py` platform registers one `NjordActivityCalendar` per location.

- Entity id: `calendar.njord_{location}_activity_windows`
- `translation_key`: `activity_windows`
- Uses `has_entity_name = True`

Calendar events are built from `ScoreData.windows` of all window-enabled score features for that location:

```
CalendarEvent(
    summary = f"{translated_feature_name} ★{score}",
    start   = window.start,
    end     = window.end,
    description = f"Score {score}/100\n{translated_limiting_text}"
)
```

**Calendar summary i18n:** HA has no built-in framework for translating calendar event summaries. ha-njord loads its own `translations/<lang>.json` at startup (keyed on `hass.config.language`) and uses it to resolve feature names and limiting-factor descriptions in calendar text. The translation dict is a subset of the main `translations/<lang>.json` under a `calendar.activities` key.

### ha-njord Window Events

Window lifecycle tracking in ha-njord, not njord. A new `NjordFeatureEvent` entity per location fires:

| Event type | When | event_data |
|---|---|---|
| `window_opened` | Score sensor updates and current time is inside a window that was not active before | `feature`, `score`, `ends_at`, `limiting_kind`, `limiting_value` |
| `window_closing_soon` | Timer fires at `ends_at - closing_soon_minutes` | `feature`, `minutes_remaining`, `reason_kind`, `reason_value` |
| `window_closed` | Timer fires at `ends_at` or next update shows window no longer active | `feature` |

Implementation:
- On each enrichment update, compare previous vs current windows to detect transitions.
- For each newly opened window, schedule `async_track_point_in_time` timers for closing_soon and closed.
- If a subsequent update shifts `ends_at`, cancel and reschedule the timers.
- `closing_soon_minutes` is configurable in the integration options (default: 30).

### i18n

All new text follows the structured-data-with-translation-key pattern:

- `LimitingKind` enum values map to translation keys in `strings.json` under `entity.sensor.<feature>.state_attributes.limiting.state`.
- Calendar feature names in `strings.json` under `calendar.activities.<feature>`.
- Window event types in `strings.json` under `entity.event.features.event_type`.
- Binary sensor names (`<feature> Window`) in `strings.json` under `entity.binary_sensor.<feature>_window.name`.
- All entries duplicated in `translations/de.json`.
- njord sends only enums and numbers, never localized strings.

## Non-goals

- New `OutputKind` (windows are an extension of Score, not a separate kind).
- HA sensor input / bidirectional channel (Phase 3).
- Composite/smart events combining multiple features.
- Per-user language selection (HA uses one installation-wide language).
- Window features for non-Score output kinds (Alerts, Conditions).

## Dependencies

- **ha-njord-i18n-foundation**: the `translation_key` pattern must be in place before this change adds new translated entities. Without it, the new entities would follow the old hardcoded pattern and need a second migration.

## Risks

| Risk | Mitigation |
|---|---|
| Calendar entity with many events (5 locations x 5 features x 3 windows = 75 events) may be slow | One calendar per location, not per feature. MaxWindows default 3. Trim to 48h horizon. |
| Window timer drift if enrichment updates are delayed | Timers are rescheduled on every enrichment update. Stale timers fire harmlessly (window_closed on already-closed window is a no-op). |
| Proto `repeated TimeWindow` adds payload size | Bounded by MaxWindows (default 3). Each TimeWindow is ~40 bytes. Negligible vs existing forecast data. |
| Calendar event summaries cannot use HA's translation framework | Own translation loading from `translations/<lang>.json` keyed on `hass.config.language`. Documented as a known limitation with fallback to EN. |
| `WindowScanner` scoring disagrees with main feature score | WindowScanner uses the same scoring function as the feature's `Evaluate`. Single source of truth by extracting scoring into a shared static method per feature. |
