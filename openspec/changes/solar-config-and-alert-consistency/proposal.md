## Why

The config area has three remaining inconsistencies: (1) `SolarYieldOptions` mixes API parameters (Tilt/Azimuth) with feature parameters (PeakKw/SystemEfficiency) and forces a cross-project dependency; `SolarBatteryStrategy` duplicates PeakKw/SystemEfficiency with a different default (0.85 vs 0.8) and compares tilted satellite data with horizontal forecast data. (2) Five alerts (Uv, Pm25, Pollen, FloodRain, FloodRisk) have hardcoded thresholds that operators cannot tune, while 14 other alerts follow the configurable-thresholds pattern. (3) Only RainProbability and Shower expose a configurable window; 17 other alerts use a fixed 24h default that operators cannot adjust.

## What Changes

### Solar consolidation
- **BREAKING**: `SolarYieldOptions` (`Njord:SolarYield`) removed. Tilt and Azimuth move into `SatelliteOptions` (`Njord:Satellite`). PeakKw and SystemEfficiency move into `SolarYieldConfig` (`Njord:Features:SolarYield`).
- **BREAKING**: `SolarBatteryStrategy` merged into `SolarYield`. `SolarYieldConfig` gains `BatteryCapacityKwh` (default 0 = no battery). The `SolarBatteryStrategy` feature, config, and tests are deleted. The battery strategy is computed when `BatteryCapacityKwh > 0`, using GTI consistently (fixing the ShortwaveRadiation vs GlobalTiltedIrradiance mismatch).
- ForecastClient and EnsembleClient read Tilt/Azimuth from `IOptions<SatelliteOptions>` instead of `SolarYieldOptions`.
- `SolarYieldOptionsValidator` removed; Tilt/Azimuth validation moves into `SatelliteOptionsValidator`, PeakKw/SystemEfficiency validation into a new `SolarYieldConfigValidator`.

### Alert threshold consistency
- Uv, Pm25, Pollen: hardcoded severity thresholds become configurable `Thresholds[]` with `IHasArrayDefaults` and a validator, following the existing pattern (Frost, Heat, Storm, etc.).
- FloodRain: `HeavyRainThreshold` (30.0) and `ForecastDays` (7) become configurable.
- FloodRisk: `MedianFactor` (2.0) and `ForecastDays` (7) become configurable.
- Thunderstorm: add the missing validator for its `CapeThresholds[]`.

### WindowHours for all relevant alerts
- Every alert that uses `NextHoursInclusive(ctx.Now)` with the default 24h window gains an optional `WindowHours` config property (default 24). This includes: Frost, Heat, Humidity, Storm, Snow, WindChill, Ice, Visibility, TropicalNight, ClearSkyUv, Dust, WetBulbDanger, Uv, Pm25, Pollen, PressureDrop, Fog, HeavyRain. No behavior change at defaults.

### Cleanup
- `ForecastOptions`: remove `IHasArrayDefaults` (ApplyDefaults is a no-op; empty Models means best_match).
- Document the clean config patterns in AGENTS.md.

### Budget impact

No change to polling frequency or domain count. Zero budget impact.

## Non-goals

- Making `ConfidenceThresholds.Default` configurable (would need a cross-cutting config mechanism).
- Adding new alerts or features.
- Changing the gRPC wire format (the merged SolarYield emits both yield and battery strategy results using existing result types).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `satellite-radiation`: Gains Tilt and Azimuth properties (from dissolved SolarYieldOptions).
- `per-domain-options`: SatelliteOptions gains Tilt/Azimuth; SolarYieldOptions section removed.
- `per-feature-config`: Alert configs gain WindowHours; Uv/Pm25/Pollen gain Thresholds; FloodRain/FloodRisk gain scalar config; Thunderstorm gains validator; SolarYieldConfig gains PeakKw/SystemEfficiency/BatteryCapacityKwh.
