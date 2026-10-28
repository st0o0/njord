# Score Windows Spec

## Proto

### ScoreData extension

`ScoreData` gains a `repeated TimeWindow windows` field (field number 4). Existing fields (`score`, `best_start`, `best_hours`) are unchanged and remain populated for backward compatibility.

### New messages

```
TimeWindow:
  start:    Timestamp  — window start (inclusive)
  end:      Timestamp  — window end (exclusive)
  score:    int32      — minimum hourly score within the window
  limiting: LimitingFactor — what ends or limits the window (optional)

LimitingFactor:
  kind:  LimitingKind enum
  from:  Timestamp — when the limiting condition starts
  value: double    — the raw metric value (e.g. UV index 8.2, mm/h 5.0)

LimitingKind enum:
  UNSPECIFIED, RAIN, UV, WIND, TEMPERATURE_HIGH, TEMPERATURE_LOW,
  HUMIDITY, POLLEN, AIR_QUALITY, VISIBILITY, THUNDERSTORM, DARKNESS
```

## Domain model

### ActivityWindow record (Njord.Messages)

```
ActivityWindow(DateTimeOffset Start, DateTimeOffset End, int Score, LimitingFactor? Limiting)
LimitingFactor(LimitingKind Kind, DateTimeOffset From, double Value)
LimitingKind enum: mirrors the proto enum
```

### ScoreFeatureResult extension

`ScoreFeatureResult` gains a `Windows` property (`IReadOnlyList<ActivityWindow>`, default empty list). Derived score results (LaundryScoreResult, etc.) pass it through.

## WindowScanner algorithm

A pure static utility `WindowScanner.Scan` in `Njord.Features/Shared/`:

**Input:**
- `hourlyScores`: array of (DateTimeOffset time, double score, LimitingKind? worstComponent, double? worstValue) per hour
- `minScore`: minimum score for a viable hour (default 50)
- `minHours`: minimum window length in hours (default 2)
- `maxWindows`: maximum number of windows to return (default 3)

**Output:** `IReadOnlyList<ActivityWindow>` ranked by score descending.

**Behavior:**
1. Find all contiguous runs of hours where score >= minScore
2. Within each run, find the sub-window of `minHours` length with the highest minimum score
3. Identify the limiting factor at the window boundary (the hour after the window where score drops below threshold, or the worst component at the boundary hour)
4. Rank all windows by score descending, return top `maxWindows`
5. If no viable window exists, return empty list (the feature still reports its single best score via `Score`/`BestStart`/`BestHours`)

## ha-njord: Calendar entity

- One `NjordActivityCalendar` per location
- Entity id: `calendar.njord_{location_slug}_activity_windows`
- Translation key: `activity_windows`
- Combines windows from all window-enabled score features for that location
- CalendarEvent fields:
  - `summary`: `"{feature_name} ★{score}"` (feature name translated)
  - `start`/`end`: from TimeWindow
  - `description`: `"Score {score}/100\n{limiting_text}"` (limiting_text translated from LimitingKind + value)
- Events are rebuilt on each enrichment update (no persistence)

## ha-njord: Window binary sensors

Each window-enabled score feature gets a binary sensor:
- Entity id: `binary_sensor.njord_{location}_{feature}_window`
- Translation key: `{feature}_window`
- State: ON when current time falls within any window for that feature, OFF otherwise
- Extra attributes: `window_start`, `window_end`, `score`, `limiting_kind`

## ha-njord: Window lifecycle events

A single `NjordFeatureEvent` entity per location fires:

| Event type | Trigger | event_data |
|---|---|---|
| `window_opened` | Current time enters a window not previously active | feature, score, ends_at, limiting_kind, limiting_value |
| `window_closing_soon` | Timer at `ends_at - closing_soon_minutes` | feature, minutes_remaining, reason_kind, reason_value |
| `window_closed` | Timer at `ends_at` or window disappears from update | feature |

- `closing_soon_minutes` configurable in integration options (default 30)
- Timers managed via HA's `async_track_point_in_time`
- Timers rescheduled when enrichment updates shift `ends_at`
- Keyed by `(feature, window_start)` to avoid duplicates

## ha-njord: Score sensor attributes

Score-type sensors gain additional attributes when windows are present:
- `windows`: list of dicts with `start`, `end`, `score`, `limiting_kind`, `limiting_value`
- `next_start`: ISO timestamp of the next upcoming window start (or None)
- `next_end`: ISO timestamp of the next upcoming window end (or None)

## i18n

- LimitingKind values: translation keys in `strings.json` entity section
- Calendar feature names: under `calendar.activities` in translation files
- Calendar entity name: `entity.calendar.activity_windows.name`
- Binary sensor names: `entity.binary_sensor.{feature}_window.name`
- Window event types: `entity.event.features.state_attributes.event_type.state`
- All entries in EN + DE
- njord sends only enum values and numbers, never localized strings
