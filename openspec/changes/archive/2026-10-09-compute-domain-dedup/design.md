## Context

`AlertEvaluator.cs` evaluates ~12 independent alert types (Frost, Heat, Storm, HeavyRain,
Uv, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight), each as
its own method. Verified representative sample (`Frost`, lines ~56-120): scan
`paramConsensus.ByHorizon`, skip entries beyond a max-hours cutoff (parsed via the existing
`ParseHorizonHours` helper — already shared, not duplicated), track an extreme median
value and the earliest hour at which a threshold is crossed, collect `Agreement` values for
confidence, then classify the extreme value through a 3-tier `thresholds[0..2]` switch into
`AlertSeverity`. The same shape recurs with "min" vs. "max" direction and different
thresholds/predicates per alert type.

## Goals / Non-Goals

**Goals:**
- One shared scan helper, one shared classification helper, used by all ~12 alert methods.
- `AlertEvaluator.cs` shrinks substantially (target: well under 1000 lines).
- Zero change to any alert's computed severity/confidence/earliest-hour for any existing
  test input.

**Non-Goals:**
- Not changing `ParseHorizonHours` (already a shared, non-duplicated helper).
- Not touching `ConsensusComputer` or how `ByHorizon`/`Agreement` data is produced.
- Not redesigning `AlertOptions`' threshold configuration shape.

## Decisions

1. **`HorizonExtremeScanner.FindExtreme<T>(IReadOnlyDictionary<string, HorizonConsensus>
   byHorizon, int maxHours, ScanDirection direction, Func<double, bool> qualifies) ->
   HorizonExtremeResult`** where `HorizonExtremeResult` holds `ExtremeValue`,
   `EarliestQualifyingHour`, `Agreements`. `ScanDirection` is `Min`/`Max` (frost/windchill
   scan for the coldest value, heat/UV scan for the hottest/highest). Internally reuses the
   existing `ParseHorizonHours`.
   Alternative considered: one scanner per direction (`FindMinimum`/`FindMaximum`).
   Rejected — a single method with a direction parameter avoids duplicating the loop body
   itself, which is the actual duplication being removed; two methods would just move the
   duplication one level down.
2. **`ThresholdClassifier.Classify(double value, IReadOnlyList<double> thresholds,
   ScanDirection direction) -> (AlertSeverity Severity, double EffectiveThreshold)`**,
   replacing the repeated 3-tier switch. Thresholds are ordered threshold[0] (Yellow) ..
   threshold[2] (Red) regardless of direction; the comparison operator flips based on
   `direction` (`<=` for Min-direction alerts like Frost, `>=` for Max-direction alerts like
   Heat).
3. **Migrate alert methods one at a time**, verifying each against its existing
   `Njord.Compute.Tests` cases before moving to the next, in order from best-understood
   (Frost, Heat — both independently verified during this change's research) to the rest.
4. **Max-hours constant**: introduce `private const int DefaultMaxHorizonHours = 24;` (or
   thread it from `AlertOptions` if a per-alert override already exists — check during
   implementation) to replace the scattered literal `24`.
5. **Nearest-point helper**: add `Njord.Compute.Analysis.NearestPointFinder` (or a small
   extension method) used by `HistoryComputer`'s two duplicated
   `OrderBy(Abs(ValidAt-now)).FirstOrDefault()` call sites, and by `TrendComputer` if its
   extrema/decay/precip-timing methods share the same shape (confirm during implementation;
   if `TrendComputer`'s duplication is a different shape, scope it out and note why in the
   PR).

## Risks / Trade-offs

- [Risk] A shared scanner/classifier could subtly change float comparison edge cases
  (`<=` vs `<`) for one alert type if the original code wasn't perfectly consistent across
  all 12 methods. → Mitigation: migrate one alert type at a time with its existing test
  suite as the acceptance gate; do not migrate two alert types in the same commit.
- [Risk] `TrendComputer`'s "nearest point" usage might not be a true duplicate of
  `HistoryComputer`'s (different point types). → Mitigation: verify during task 4 before
  forcing a shared helper; if the shapes genuinely differ, leave `TrendComputer` alone and
  only dedup within `HistoryComputer`.

## Migration Plan

Pure refactor, single deploy, no persisted/wire format changes, trivial revert per
alert-type migration step (each is its own commit-sized chunk).

## Open Questions

None.
