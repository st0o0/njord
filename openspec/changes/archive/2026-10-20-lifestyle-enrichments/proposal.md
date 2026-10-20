## Why

njord's alert system covers weather hazards, air quality thresholds, and flood risk — but Home Assistant users want actionable lifestyle recommendations, not just warnings. Seven new IDomainHandler enrichments turn raw multi-domain data into decisions a user (or an automation) can act on immediately: when to open windows, whether it's safe to exercise, whether laundry will dry, and more.

## What Changes

- **VentilationWindowHandler** (Weather × AirQuality): scans next 24h for hours where AQ is good (PM2.5 < 15, low pollen) AND temperature is comfortable AND no rain. Emits `VentilationAdvised` with best start/end window.
- **ExerciseAdvisoryHandler** (Weather × AirQuality): scores next 12h on AQ quality (PM2.5, ozone) and weather comfort (temp, humidity, UV). Emits `ExerciseAdvised` with Safe/Caution/Avoid and reasons.
- **LaundryScoreHandler** (Weather): computes 0–100 score from temp, humidity, wind, rain probability, sunshine. Emits `LaundryScoreComputed` with score and best drying window.
- **CommuteSafetyHandler** (Weather): checks next 2h for ice, fog, heavy rain, strong wind. Emits `CommuteSafety` with Safe/Caution/Dangerous and identified risks.
- **PollenTrendHandler** (AirQuality): compares current vs previous pollen values per type. Emits `PollenTrendComputed` with Rising/Falling/Stable per type and peak timing.
- **NightVentilationHandler** (Weather): if tomorrow's peak > 28°C and tonight's low < 20°C, recommends opening windows overnight. Emits `NightVentilationAdvised` with open/close times.
- **ConfidenceTrendHandler** (Weather): compares ensemble StdDev current vs previous. Emits `ConfidenceTrend` with Converging/Diverging/Stable.
- 7 new PipelineEvent record types in Njord.Messages.
- ForecastQueries.Upcoming helper added to Njord.Core.

## Non-goals

- Configurable thresholds for lifestyle enrichments (hardcoded for now).
- MQTT/gRPC consumer support for the new events (they flow through the BroadcastHub but consumers ignore unknown types).
- API budget impact: zero (uses existing domain data).

## Capabilities

### New Capabilities

- `lifestyle-enrichments`: Seven IDomainHandler enrichments producing actionable lifestyle events from multi-domain data.

### Modified Capabilities

_(none)_

## Impact

- **Njord.Weather**: 6 new handlers in `Lifestyle/` subfolder.
- **Njord.AirQuality**: 1 new handler (PollenTrend).
- **Njord.Messages**: 7 new PipelineEvent record types.
- **Njord.Core**: ForecastQueries.Upcoming utility.
- **Tests**: One spec per handler (3-4 facts each).
