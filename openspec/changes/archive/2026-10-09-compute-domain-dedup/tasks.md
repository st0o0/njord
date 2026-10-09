## 1. Shared horizon-scan and threshold-classification helpers

- [x] 1.1 Add `src/Njord.Compute/Analysis/HorizonExtremeScanner.cs` with `FindExtreme(byHorizon, maxHours, direction, qualifies) -> HorizonExtremeResult` (ExtremeValue, EarliestQualifyingHour, Agreements), reusing the existing `ParseHorizonHours` helper.
- [x] 1.2 Add `src/Njord.Compute/Analysis/ThresholdClassifier.cs` with `Classify(value, thresholds, direction) -> (AlertSeverity, double EffectiveThreshold)`.
- [x] 1.3 Add `HorizonExtremeScannerSpec` and `ThresholdClassifierSpec` in `src/Njord.Compute.Tests/Analysis/` covering both directions, empty/no-qualifying-entry cases, and all three severity tiers.

## 2. Migrate Frost and Heat (verified during research)

- [x] 2.1 Migrate `AlertEvaluator.EvaluateFrost` to use `HorizonExtremeScanner`/`ThresholdClassifier`; run existing frost test cases in `src/Njord.Compute.Tests/Analysis/AlertEvaluatorSpec.cs` (or equivalent) to confirm identical output.
- [x] 2.2 Migrate `AlertEvaluator.EvaluateHeat` the same way; confirm identical output.

## 3. Migrate remaining alert types (one at a time, test-gated)

- [x] 3.1 Storm
- [x] 3.2 HeavyRain
- [x] 3.3 Uv
- [x] 3.4 Snow
- [x] 3.5 PressureDrop
- [x] 3.6 Thunderstorm
- [x] 3.7 Ice
- [x] 3.8 WindChill
- [x] 3.9 Visibility
- [x] 3.10 TropicalNight
- [x] 3.11 Humidity (threshold-classification only, if it doesn't use the horizon scanner)
- [x] 3.12 After each sub-task, run `Njord.Compute.Tests` and confirm the corresponding alert-type test cases pass unmodified before moving to the next.

## 4. Max-hours constant and nearest-point helper

- [x] 4.1 Replace the scattered literal `24` max-hours cutoff in `AlertEvaluator` with a single named constant (or thread it from `AlertOptions` if an existing per-alert override already exists — check first).
- [x] 4.2 Investigate `HistoryComputer`'s two `OrderBy(Abs(ValidAt-now)).FirstOrDefault()` call sites and `TrendComputer`'s extrema/decay/precip-timing methods; if they share the same point-lookup shape, add `NearestPointFinder` (or an extension method) and migrate both; if `TrendComputer`'s shape genuinely differs, dedup only within `HistoryComputer` and note why `TrendComputer` was left alone.
- [x] 4.3 Add/extend tests covering the nearest-point helper in `src/Njord.Compute.Tests/Analysis/`.

## Validation

- [x] 5.1 `dotnet build Njord.slnx` (run from `src/`)
- [x] 5.2 `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build`
- [x] 5.3 `dotnet slopwatch analyze -d . --fail-on warning` (run from repo root)
