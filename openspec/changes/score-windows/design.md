## Context

Score features (Laundry, Outdoor, Bbq, Surf) already compute a per-hour score and extract a single best window via `Scoring.BestWindow` / `Scoring.LongestRun`. The result (`ScoreFeatureResult`) carries `Score`, `BestStart`, `BestHours`. The proto `ScoreData` mirrors this with `score`, `best_start`, `best_hours`. ha-njord parses this into `FeatureData.score` but (as of the i18n-foundation change) only exposes `best_start` and `best_hours` as sensor attributes.

Users need ranked windows with limiting-factor context, calendar integration, and proactive notifications ("window closing in 30 min because of rain").

## Goals / Non-Goals

**Goals:**
- Score features can report multiple ranked time windows with limiting-factor metadata
- ha-njord shows windows in a native HA Calendar entity per location
- ha-njord fires window lifecycle events (opened, closing_soon, closed)
- Additive, backward-compatible change to proto and domain model

**Non-Goals:**
- New OutputKind (windows extend Score, they are not a separate kind)
- Window support for non-Score features (Alerts, Conditions)
- HA sensor input / bidirectional channel
- Per-user language for calendar summaries

## Decisions

### 1. Extend ScoreData rather than new OutputKind

ScoreData already has `best_start` + `best_hours`. A `repeated TimeWindow windows` field is a natural generalization. Adding a new OutputKind would require changes throughout the pipeline (FeatureEngine dispatch, gRPC mapping, ha-njord entity creation) for what is semantically still a score.

**Alternative considered:** Separate `WindowResult` OutputKind. Rejected because it fragments the scoring concept and doubles the entity types a score feature produces.

### 2. WindowScanner as shared utility, not base-class override

The sliding-window algorithm is a pure function: `(hourlyScores[], config) -> TimeWindow[]`. It lives in `Njord.Features/Shared/WindowScanner.cs` alongside `Scoring.cs`. Features call it explicitly in their `Evaluate` method rather than having the base class call it, because:
- Not all score features want windows (some may have non-temporal scores)
- The limiting-factor identification is feature-specific (each feature knows which component limits the score)
- Keeps `FeatureRule<T>` unchanged

### 3. Domain model: IReadOnlyList on ScoreFeatureResult

`ScoreFeatureResult` gains an optional `IReadOnlyList<ActivityWindow> Windows` property (default empty). The `ActivityWindow` record lives in `Njord.Messages/Pipeline/` beside the result types. The gRPC mapping layer reads this list and populates `ScoreData.windows`.

The record is named `ActivityWindow` (not `TimeWindow`) to avoid collision with the proto-generated `TimeWindow` class.

### 4. LimitingKind as domain enum in Njord.Messages

`LimitingKind` is a closed enum in `Njord.Messages` (not Core -- it's a result concern). The proto `LimitingKind` mirrors it. Mapping is an explicit switch in `FeatureResultMappingExtensions`.

### 5. Calendar: one per location, not per feature

5 locations x 5 window features x 3 windows = 75 events total. One calendar per location keeps the entity count at 5 (one per location). Events from different features are distinguished by the summary text, which includes the translated feature name.

**Alternative considered:** One calendar per feature per location. Rejected because it creates too many entities (25+) and fragments the "what's good today?" overview.

### 6. Window lifecycle events in ha-njord, not njord

njord polls hourly. The "closing in 30 min" notification needs sub-poll-interval precision. ha-njord uses HA's `async_track_point_in_time` to schedule timers based on `window.end`. Timers are rescheduled when a new enrichment shifts `end`. This keeps njord as a pure data producer.

### 7. Calendar summary i18n: own translation loading

HA has no built-in framework for translating CalendarEvent summaries. ha-njord loads `translations/<lang>.json` at startup keyed on `hass.config.language` and uses it to resolve feature names and limiting-factor descriptions. Fallback is always EN.

## Risks / Trade-offs

- **WindowScanner scoring may diverge from main Evaluate:** Mitigated by having each feature extract its `ScoreHour` as a static method that both `Evaluate` and window scanning call.
- **Calendar events are rebuilt on every enrichment update:** Bounded by MaxWindows (3) x enabled features (3-5). Rebuilding is cheap (string formatting).
- **Timer management complexity in ha-njord:** Each active window needs two timers (closing_soon, closed). With 5 features x 3 windows = 15 timers per location. HA's timer system handles this fine; the risk is in cancel/reschedule logic. Mitigated by tracking timers in a dict keyed by `(feature, window_start)`.
