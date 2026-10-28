## Context

njord has Forecast Temperature/Humidity and Satellite/Forecast irradiance data flowing per location every poll cycle. `SolarYield` already uses `ForecastSolarInput` and `SolarYieldOptions` (PeakKw, Azimuth, Tilt) to estimate daily kWh. No feature currently turns temperature data into heating advice or compares today's solar with tomorrow's forecast for battery decisions.

Existing condition features follow a clear pattern: `FeatureRule<TInput>` with a `ConditionFeatureResult` record, a `*Config : IConfigSection`, registration via `AddNjordOptions`/`AddSingleton<IFeatureRule>`, proto message with `ConditionData`, and gRPC mapping in `FeatureResultMappingExtensions` + `EnrichmentEventMappingExtensions`.

## Goals / Non-Goals

**Goals:**
- HeatPumpCOP: estimate air-source heat pump efficiency from outdoor temperature, hourly over configurable horizon
- SolarBatteryStrategy: recommend charge/feed-in decisions by comparing satellite-observed and forecast solar irradiance
- Both features off by default, zero API cost when disabled, near-zero when enabled

**Non-Goals:**
- Brand-specific heat pump integration (generic COP model only)
- Electricity price integration (Phase 3, requires HA sensor input)
- Battery SoC reading from HA (Phase 3)
- Solar panel degradation modeling

## Decisions

### 1. HeatPumpCOP uses ForecastInput, not a new input type

The feature only needs `Temperature` from Forecast. `ForecastInput` already exists (`FeatureInputs.cs:13`) and wraps `DomainForecast<ForecastPoint>`. No need for a new struct.

**Alternative considered:** Raw `ForecastPoint` access. Rejected because all features go through typed inputs via `Project`.

### 2. COP formula: simplified Carnot regression

`COP = 6.81 - 0.121 * (T_target - T_outside)`

This is a well-established linear approximation of real-world ASHP performance curves, accurate within ~15% for typical ranges (-15°C to +20°C outdoor, 30°C to 60°C flow). The constants come from published field-study regressions.

**Alternative considered:** Carnot formula `COP = T_hot / (T_hot - T_cold)` with an empirical efficiency factor. More physically grounded but requires an extra calibration parameter users can't easily provide. The linear regression is simpler and close enough for advisory purposes.

### 3. SystemType sets default TargetTemperature

Rather than making users figure out their flow temperature, `SystemType` (FloorHeating=35°C, Radiator=55°C, HotWater=60°C) sets a sensible default. Explicit `TargetTemperature` overrides it. The validator only checks the final temperature.

### 4. SolarBatteryStrategy reuses SolarYieldOptions, not duplicated config

Panel parameters live in `SolarYieldOptions` at `Njord:SolarYield` (PeakKw, Azimuth, Tilt). `SolarBatteryConfig` adds only battery-specific options. The feature constructor takes `IOptions<SolarBatteryConfig>` and `IOptions<SolarYieldOptions>`. Validator checks `SolarYieldOptions.PeakKw > 0` when `SolarBatteryConfig.Enabled`.

### 5. SolarBatteryStrategy uses ForecastSolarInput (existing)

Same input type as `SolarYield` and `SatelliteRadiation`. The `Project` method can pattern-match Satellite as optional: return the input even when only Forecast is available (satellite adds quality but isn't mandatory).

### 6. Proto field numbers: conditions range 76+

Current conditions occupy 60-75. HeatPumpCop gets 76, SolarBatteryStrategy gets 77.

### 7. Hourly data as repeated proto messages

Both features produce hourly data (COP curve, solar kWh estimate). These are `repeated HourlyCop` and `repeated HourlySolar` in the proto. ha-njord exposes them as `extra_state_attributes` (hourly arrays), and HA template sensors or ApexCharts can render the curves.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| COP ~15% error vs. real device | Document as estimate; users adjust TargetTemperature to calibrate |
| SolarBattery without Satellite data | Degrade gracefully: use Forecast-only irradiance, set confidence to 0 |
| SolarYield disabled but SolarBattery enabled | `SolarBatteryConfigValidator` checks `SolarYieldOptions.PeakKw > 0` on startup |
| Satellite data delay up to 3h | Already mitigated by `SolarIrradianceBlend` pattern used by SolarYield |
