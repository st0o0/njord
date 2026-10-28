# Tasks: score-windows

## 1. Proto: Add TimeWindow, LimitingFactor, LimitingKind to weather.proto

- [x] Add `LimitingKind` enum to `protos/njord/v2/weather.proto` with values: UNSPECIFIED(0), RAIN(1), UV(2), WIND(3), TEMPERATURE_HIGH(4), TEMPERATURE_LOW(5), HUMIDITY(6), POLLEN(7), AIR_QUALITY(8), VISIBILITY(9), THUNDERSTORM(10), DARKNESS(11)
- [x] Add `LimitingFactor` message with `LimitingKind kind = 1`, `google.protobuf.Timestamp from = 2`, `double value = 3`
- [x] Add `TimeWindow` message with `google.protobuf.Timestamp start = 1`, `google.protobuf.Timestamp end = 2`, `int32 score = 3`, `LimitingFactor limiting = 4`
- [x] Add `repeated TimeWindow windows = 4` to existing `ScoreData` message

Validation: `dotnet build src/Njord.slnx`

## 2. Domain model: ActivityWindow and LimitingKind in Njord.Messages

- [x] Add `LimitingKind` enum to `src/Njord.Messages/Pipeline/` (new file `ActivityWindow.cs`): None, Rain, Uv, Wind, TemperatureHigh, TemperatureLow, Humidity, Pollen, AirQuality, Visibility, Thunderstorm, Darkness
- [x] Add `LimitingFactor` record: `(LimitingKind Kind, DateTimeOffset From, double Value)`
- [x] Add `ActivityWindow` record: `(DateTimeOffset Start, DateTimeOffset End, int Score, LimitingFactor? Limiting)`
- [x] All in one file `src/Njord.Messages/Pipeline/ActivityWindow.cs`

Validation: `dotnet build src/Njord.slnx`

## 3. Extend ScoreFeatureResult with Windows

- [x] In `src/Njord.Messages/Pipeline/TypedFeatureResults.cs`, add `IReadOnlyList<ActivityWindow> Windows` property to `ScoreFeatureResult`. Use a default value of `[]` so existing derived records don't break.
- [x] Update the 4 concrete score results (`BbqScoreResult`, `LaundryScoreResult`, `OutdoorScoreResult`, `SurfConditionsResult`) to accept and pass through an optional `Windows` parameter (default `[]`)

Validation: `dotnet build src/Njord.slnx`

## 4. WindowScanner utility in Njord.Features/Shared

- [x] Create `src/Njord.Features/Shared/WindowScanner.cs` with a static `Scan` method
- [x] Input: `IReadOnlyList<HourlyScore> scores, int minScore = 50, int minHours = 2, int maxWindows = 3` where `HourlyScore` is `(DateTimeOffset Time, double Score, LimitingKind? WorstComponent, double? WorstValue)`
- [x] Algorithm: find contiguous runs above minScore, extract best sub-window per run, identify limiting factor at boundary, rank by score descending, trim to maxWindows
- [x] Return `IReadOnlyList<ActivityWindow>`
- [x] `HourlyScore` record in the same file (or as a nested type)

Validation: `dotnet build src/Njord.slnx`

## 5. gRPC mapping: TimeWindow and LimitingFactor

- [x] In `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs`, update `ToData(this ScoreFeatureResult r)` to map `r.Windows` to `proto.Windows` (repeated `V2.TimeWindow`)
- [x] Add `ToProto(this ActivityWindow w)` mapping: Start/End to Timestamp, Score, Limiting to `V2.LimitingFactor`
- [x] Add `ToProto(this LimitingFactor f)` mapping: Kind to `V2.LimitingKind` (explicit switch), From to Timestamp, Value
- [x] Add `ToProto(this LimitingKind k)` mapping: explicit switch, default to UNSPECIFIED

Validation: `dotnet build src/Njord.slnx`

## 6. Retrofit LaundryScore to populate windows

- [x] In `src/Njord.Features/Scores/Laundry/LaundryScore.cs`, extract `ScoreHour` to a reusable static method (it already is static)
- [x] After computing hourly scores, call `WindowScanner.Scan()` with the hourly data
- [x] For limiting factor identification: at each window boundary, determine which score component (rain, temperature, humidity, wind, cloud) contributed most to the drop
- [x] Pass the resulting `ActivityWindow[]` to `LaundryScoreResult` via the new `Windows` parameter
- [x] Add `WindowHorizonHours`, `MinWindowScore`, `MinWindowHours`, `MaxWindows` to `LaundryConfig`

Validation: `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj --no-build`

## 7. Tests for WindowScanner and gRPC mapping

- [x] Create `src/Njord.Features.Tests/Shared/WindowScannerTests.cs`:
  - Fact: empty scores returns empty windows
  - Fact: all scores below threshold returns empty
  - Fact: single contiguous run extracts one window
  - Fact: multiple runs extracts ranked windows
  - Fact: maxWindows trims result
  - Fact: limiting factor captured at boundary
  - Fact: minHours filters short runs
- [x] Create `src/Njord.Grpc.Tests/Mapping/WindowMappingTests.cs`:
  - Fact: ActivityWindow maps to proto TimeWindow
  - Fact: LimitingFactor maps to proto LimitingFactor
  - Fact: LimitingKind switch covers all values (Theory per value)
  - Fact: empty windows list maps to empty repeated field
  - Fact: ScoreFeatureResult.ToData includes windows

Validation:
```bash
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj --no-build
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build
```

## 8. ha-njord: Calendar entity platform

- [x] Create `D:\GIT\ha-njord\custom_components\njord\calendar.py` with `NjordActivityCalendar` entity
- [x] One calendar per location, translation_key `activity_windows`
- [x] Build CalendarEvents from all window-enabled score features' `ScoreData.windows`
- [x] Translate feature names and limiting factors using own translation loading from `translations/<lang>.json`
- [x] Register platform in `__init__.py` (`PLATFORMS` list)
- [x] Add `calendar.activities` section to `strings.json`, `translations/en.json`, `translations/de.json`

Validation: manual review (no automated tests for ha-njord calendar)

## 9. ha-njord: Window lifecycle events

- [x] Add window transition detection to enrichment update handler
- [x] Compare previous vs current windows per feature to detect opened/closed transitions
- [x] Create `NjordFeatureEvent` entity per location (or extend existing event entity)
- [x] Schedule `async_track_point_in_time` timers for `window_closing_soon` and `window_closed`
- [x] Reschedule timers when enrichment updates shift window end times
- [x] Add `closing_soon_minutes` to integration options (default 30)
- [x] Add event types and translations to `strings.json`, `en.json`, `de.json`

Validation: manual review

## 10. ha-njord: Score sensor extended attributes

- [x] Extend score sensor `extra_state_attributes` with `windows` list (dicts with start/end/score/limiting), `next_start`, `next_end`
- [x] Parse `TimeWindow` from gRPC `ScoreData.windows` in the data model
- [x] Add `LimitingKind` translation entries to `strings.json`, `en.json`, `de.json`

Validation: manual review

## 11. Full validation

- [x] `dotnet build src/Njord.slnx`
- [x] `dotnet format src/Njord.slnx --verify-no-changes`
- [x] Run all test projects:
```bash
for p in src/Njord.*Tests; do
  [ "$p" = "src/Njord.Tests.Shared" ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
