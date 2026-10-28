## Context

njord has 53 features across 5 output kinds. Health-related data correlations (pressure→migraine, air quality→asthma, temperature→sleep) are a documented unmet need. The feature system already supports cross-domain inputs (`ForecastAqInput`) and ring-buffer history for trend analysis. This change adds three health/wellness features following established patterns.

## Goals / Non-Goals

**Goals:**
- MigraineRisk alert from barometric pressure trends (Forecast-only, ring buffer)
- SleepQuality score from overnight comfort factors (Forecast + AirQuality)
- AsthmaRisk score from multi-factor respiratory assessment (Forecast + AirQuality)
- All three follow the `FeatureRule<TInput>` pattern with `IConfigSection` configs, proto messages, gRPC mappings, and registration
- Structured enums for i18n (no human-readable strings in njord)

**Non-Goals:**
- Medical-grade accuracy or clinical validation
- Indoor sensor input from HA (Phase 3)
- Health app integration
- Per-user health profiles beyond `PollenSensitive` boolean

## Decisions

### 1. MigraineRisk uses PollDepth 6 for pressure trend analysis

Pressure drops are measured over 6h/24h windows. At 60-minute polling, PollDepth=6 gives 6h of ring buffer history. The 24h threshold uses the hourly forecast data (looking ahead), not the ring buffer.

**Consequence:** MigraineRisk cannot evaluate until 6 polls after startup (~6h). This is expected and documented.

**Alternative considered:** PollDepth=24 for full 24h history. Rejected: too much memory per location, and the 24h drop is better computed from forecast-ahead data.

### 2. SleepQuality night window uses configurable hours, not timezone

`NightStartHour` (default 22) and `NightEndHour` (default 6) are interpreted against the forecast timestamps (UTC). Users in non-UTC zones must adjust these values.

**Why not timezone-aware:** njord works in UTC throughout. Adding timezone support for one feature would be inconsistent. The configuration is simple (e.g., UTC+2 user sets NightStartHour=20).

**Alternative considered:** Accept IANA timezone string and convert. Rejected: adds dependency, inconsistent with other config patterns, and the hour offset is trivial.

### 3. AsthmaRisk uses weighted component model with validated weights

Five components weighted 0.35/0.25/0.20/0.10/0.10. The weights are configurable and validated to sum to 1.0 (±0.01 tolerance) by `AsthmaConfigValidator`.

**Why configurable:** Different users have different respiratory sensitivities. An asthmatic in a high-ozone area may want to increase the ozone weight.

### 4. Föhn detection is generalized to "warm dry wind"

The proposal's Föhn trigger detects any warm dry wind from ~south (WindDirection 150-210°) with rising pressure + rising temperature + falling humidity. This covers Alpine Föhn, Chinook, Santa Ana, and similar patterns.

**Risk:** False positives in non-mountain regions. Mitigated by: feature off by default, and the trigger adds to severity rather than triggering alone.

### 5. MigraineRisk result uses the standard AlertFeatureResult pattern

`MigraineRiskAlertResult` extends `AlertFeatureResult` with `MigraineTrigger DominantTrigger`, `double PressureChangeHpa`, `double TemperatureChangeC`. This follows the same pattern as `PressureDropAlertResult`, `FrostAlertResult`, etc.

### 6. SleepQuality and AsthmaRisk extend ScoreFeatureResult with extra fields

`SleepQualityResult` adds `SleepRecommendation`, `SleepFactor`, `double MinNightTemperature`. `AsthmaRiskResult` adds `AsthmaDominantFactor`, `int Pm25Component`, `int OzoneComponent`, `int PollenComponent`. Both extend `ScoreFeatureResult(Location, Score, BestStart, BestHours)`.

### 7. Medical disclaimer

Features are informational. Documentation must state they are not medical devices and should not replace professional medical advice. Features are off by default.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Medical liability from user reliance | Disclaimer in docs; features off by default |
| Föhn false positives outside mountain regions | Generalized pattern; off by default; adds to severity, doesn't trigger alone |
| 6h startup delay for MigraineRisk | Expected; documented |
| Weight validation rejecting user config | Clear error message naming the invalid weights |
| Night window UTC confusion | Documented; configurable; trivial offset for users |
