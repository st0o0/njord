## Context

Wave 3 adds cross-domain features that combine data from Weather, AirQuality, and Marine domains. Unlike single-domain alerts that check one threshold, these features use multi-variable scoring, trend detection across poll cycles, and cross-domain input types. All required API variables are already fetched after Waves 1-2.

## Goals / Non-Goals

**Goals:**
- Implement 4 new features and 1 enhancement using only existing data flows
- Use the typed input system and domain requirements to express cross-domain dependencies
- Exploit PollDepth for trend-based features (PollenBlast)
- Keep each feature self-contained in its own directory with config

**Non-Goals:**
- Adding new API variables or endpoints
- Changing the pipeline or polling architecture
- Statistical or ML-based forecasting

## Decisions

### D1: FireRisk uses weighted scoring, not simple thresholds

**Decision:** FireRisk computes a weighted risk score from 5 components (temperature: 25%, humidity: 25%, wind: 20%, soil moisture: 20%, dry spell: 10%) rather than requiring all thresholds to be exceeded simultaneously. Severity maps from the total score: >0.7 Red, >0.4 Orange, >0.2 Yellow.

**Rationale:** Simple all-or-nothing thresholds miss the compounding nature of fire risk. A location with 38C, 15% humidity, moderate wind, and dry soil is extremely dangerous even if wind is below 20 km/h. The weighted model captures this.

**Alternative considered:** Simple threshold conjunction (all conditions must be met). Rejected because it would miss many real fire-risk scenarios.

### D2: PollenBlast uses PollDepth=8 for rain-to-dry transition

**Decision:** PollenBlast declares PollDepth=8 and compares the current poll's weather against the history to detect when precipitation stops. The transition triggers the alert if pollen concentrations are also rising.

**Rationale:** Pollen release events happen specifically at the transition from wet to dry weather. A single poll snapshot cannot detect this. 8 previous polls at 60-minute intervals covers the typical transition window (8 hours).

### D3: ThunderstormEnhanced modifies the existing alert, does not create a new one

**Decision:** The existing `ThunderstormAlert` gains optional CIN and lifted_index parameters. If present, they modulate severity up or down by one step. Missing data is a no-op.

**Alternative considered:** A separate `ThunderstormEnhancedAlert` alongside the original. Rejected because having two thunderstorm alerts is confusing for the operator and they share the same threshold logic.

**Implementation:** Add `ConvectiveInhibition` and `LiftedIndex` to optional weather params (not required, so missing data does not prevent the alert). In `Evaluate`, if both values are present at the peak hour, apply the severity modifier.

### D4: CoastalFlood uses MarineWeatherEnsembleInput

**Decision:** Create a new `MarineWeatherEnsembleInput` record struct for the cross-domain combination of Marine + Weather + Ensemble. The alert's `Project` returns null when Marine data is absent (inland location), which is the standard behavior.

**Rationale:** The existing `MarineWeatherInput` does not include Ensemble. Adding a variant with Ensemble follows the established pattern (WeatherEnsembleInput, WeatherAqInput, etc.).

### D5: GrowingDegree is a Forecast, not a Condition

**Decision:** GrowingDegree is an `OutputKind.Forecast` because it projects multi-day accumulated values, not a binary state. It produces a `GrowingDegreeResult` with daily GDD and cumulative total.

**Rationale:** Conditions are binary (on/off) or scored. GDD is an accumulated metric over the forecast horizon, which fits the Forecast output kind.

## Risks / Trade-offs

- **[FireRisk weighted scoring is opinionated]** -- The weight distribution is a reasonable default but may not match all climates. Mitigation: all thresholds and potentially weights are configurable.
- **[PollenBlast has a narrow trigger window]** -- The rain-to-dry transition must happen within the PollDepth window. If polling interval is 60 min and PollDepth=8, the alert can detect transitions up to 8 hours back. Longer wet spells followed by gradual drying may be missed. Acceptable for the initial implementation.
- **[CoastalFlood only works for coastal locations]** -- Marine data returns NotFound for inland locations, so CoastalFlood silently does nothing there. This is by design.
- **[ThunderstormEnhanced CIN/LI data may not be available in all models]** -- CIN and lifted_index are not supported by all forecast models. The enhancement is optional (no-op when missing) so this is safe.
