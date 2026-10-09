## Why

A cross-codebase review against the sibling FunkArr project found that njord's pure
compute domain (`Njord.Compute/Analysis`) has far more duplication than FunkArr's equivalent
compute domain (`FunkArr.Scoring`, which decomposes its matching logic into small focused
private methods with no repeated blocks). `AlertEvaluator.cs` is 1383 lines: the same
"scan `ByHorizon` up to a max-hours cutoff, track an extreme median value + earliest
qualifying hour + an agreement list, then map the extreme value through a 3-tier threshold
switch to a severity" shape is written out by hand for every alert type (Frost, Heat,
Storm, HeavyRain, Uv, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility,
TropicalNight). `TrendComputer` and `HistoryComputer` separately repeat a "nearest point by
time" `OrderBy(...).FirstOrDefault()` lookup in multiple methods. Extracting shared helpers
for these two repeated shapes would cut `AlertEvaluator` by an estimated 35-40% with no
behavior change, and makes adding a new alert type a matter of supplying thresholds/a
predicate instead of copying a ~50-150 line method.

## What Changes

- Add a shared helper (name TBD in design, e.g. `HorizonExtremeScanner.FindExtreme(consensus,
  maxHours, direction, predicate) -> (extremeValue, earliestQualifyingHour, agreements)`) to
  `Njord.Compute/Analysis`, replacing the hand-written scan loop currently duplicated across
  `AlertEvaluator`'s per-alert-type methods (Frost ~69-99, Heat ~165-193, Storm ~249-280,
  HeavyRain ~339-367, Uv ~445-458, Snow ~620-655, PressureDrop ~726-738, Thunderstorm
  ~813-838, Ice ~890-918, WindChill ~967-993, Visibility ~1054-1078, TropicalNight
  ~1128-1159).
- Add a shared `ThresholdClassifier.Classify(value, thresholds, direction) -> (AlertSeverity,
  double effectiveThreshold)` helper, replacing the repeated 3-tier threshold switch (Frost
  ~110-116, Heat ~200-209, Storm ~291-297, WindChill ~1004-1010, Visibility ~1089-1095,
  TropicalNight ~1169-1175, Humidity ~1249-1255).
- Replace the magic-number `24` (max hours) scattered 6+ times in `AlertEvaluator` with a
  single named constant or a parameter threaded from `AlertOptions`.
- Add a shared `nearest point by time` helper to `Njord.Compute/Analysis` used by both
  `HistoryComputer` (currently duplicated at two call sites) and, if applicable,
  `TrendComputer`'s extrema/decay/precip-timing methods, replacing the repeated
  `OrderBy(Abs(ValidAt-now)).FirstOrDefault()` pattern.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `compute-library`: `threshold-alerts`, `new-alert-types`, and `trend-analysis` describe
  observable computation results (which alert fires, at what severity, with what
  confidence), not implementation structure, and are unaffected — this change does not
  alter any alert's firing condition, severity thresholds, or confidence computation.
  `compute-library` gets one new requirement about shared helpers for the horizon-scan and
  threshold-classification shapes that currently repeat per alert type.

## Impact

- `src/Njord.Compute/Analysis/AlertEvaluator.cs` (1383 lines; expected to shrink
  substantially)
- `src/Njord.Compute/Analysis/TrendComputer.cs`
- `src/Njord.Compute/Analysis/HistoryComputer.cs`
- New: `src/Njord.Compute/Analysis/HorizonExtremeScanner.cs`,
  `src/Njord.Compute/Analysis/ThresholdClassifier.cs`
- Matching tests: `src/Njord.Compute.Tests/Analysis/`
- No change to `Njord.Enrichment` (the actor layer consuming these compute results is
  untouched) or to any persisted/wire format.

## Non-goals

- No change to any alert's firing threshold values, severity tiers, or confidence
  calculation.
- No change to `AlertOptions`/`HistoryOptions`/`IndexOptions` configuration shape.
- Not addressing the `Njord.Grpc` or `Njord.Mqtt` duplication (separate changes).

## API budget impact

None. This change touches only pure in-process computation; it does not affect Open-Meteo
polling behavior or request volume.
