## MODIFIED Requirements

### Requirement: Heavy rain alert evaluates daily precipitation sum in addition to hourly
`AlertEvaluator.EvaluateHeavyRain` SHALL, in addition to its existing hourly scan, check each model's `DailyForecastSeries` for `precipitation_sum` over the next 1–2 days. If the daily sum exceeds `HeavyRainDailyThreshold` for any model, that model counts as "agreeing" for the daily evaluation. The final severity and confidence SHALL be the maximum of the hourly-based and daily-based evaluations.

#### Scenario: Daily sum exceeds threshold but hourly doesn't
- **WHEN** no single hourly precipitation exceeds the hourly threshold, but 4 of 5 models show daily precipitation_sum > 30 mm for tomorrow
- **THEN** severity is determined by the daily evaluation with confidence 0.8

#### Scenario: Both hourly and daily trigger
- **WHEN** hourly scan yields severity=Yellow confidence=0.5, and daily scan yields severity=Orange confidence=0.75
- **THEN** the final alert uses severity=Orange, confidence=0.75 (the higher severity wins)

#### Scenario: Daily data unavailable
- **WHEN** DailyForecastSeries is empty or precipitation_sum is not in resolved daily parameters
- **THEN** the daily evaluation is skipped; alert is based on hourly scan only (existing behavior)

### Requirement: UV alert evaluates daily uv_index_max
`AlertEvaluator.EvaluateUv` SHALL, in addition to its existing hourly scan, check each model's `DailyForecastSeries` for `uv_index_max`. The daily UV max is a more reliable peak indicator than the hourly series (which may miss the exact peak hour). The final severity SHALL be the maximum of hourly-based and daily-based evaluations.

#### Scenario: Daily UV max higher than hourly peak
- **WHEN** hourly UV scan finds max=7 across models, but daily uv_index_max shows [8, 9, 8] across 3 models
- **THEN** severity is computed from the daily values (higher) rather than the hourly scan

#### Scenario: Daily UV not available
- **WHEN** uv_index_max is not in the resolved daily parameter set
- **THEN** UV alert uses only the hourly scan (existing behavior preserved)

### Requirement: Snow alert evaluates daily snowfall sum
`AlertEvaluator.EvaluateSnow` SHALL, in addition to its existing hourly scan, check each model's `DailyForecastSeries` for `snowfall_sum` over the next 1–2 days. The final severity and confidence SHALL be the maximum of the hourly-based and daily-based evaluations.

#### Scenario: Significant daily snowfall
- **WHEN** 4 of 6 models predict snowfall_sum > 10 cm for tomorrow
- **THEN** snow alert severity reflects the daily sum evaluation with confidence 0.667

#### Scenario: Daily snowfall not available
- **WHEN** snowfall_sum is not in resolved daily parameters
- **THEN** snow alert uses only the hourly accumulation scan
