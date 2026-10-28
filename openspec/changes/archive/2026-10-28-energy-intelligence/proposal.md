# Energy Intelligence

## Summary

Add two energy management features that help Home Assistant users optimize heating and solar battery usage based on weather forecasts: **HeatPumpCOP** estimates air-source heat pump efficiency over the next 24 hours, and **SolarBatteryStrategy** recommends charge/feed-in decisions by comparing satellite-observed and forecast solar irradiance.

## Motivation

Smart-home users with heat pumps and solar+battery setups make daily decisions that depend on weather: "Should I heat now while it's mild, or wait?", "Should I store today's solar surplus or feed it in because tomorrow will be sunny too?" These decisions are currently gut-feel. njord already has all the raw data (temperature, humidity, satellite GTI, forecast irradiance) but no features that translate it into actionable energy advice. Direct financial impact for the user makes these high-value features.

## Scope

### njord

- Two new `FeatureRule<TInput>` implementations in `Njord.Features/Conditions/`
- Two new proto messages in `weather.proto` with enums
- Two new `IConfigSection` classes with validators
- Registration in `AddFeatures`

### ha-njord

- Two new condition sensors with translation keys
- Enum state translations in `strings.json` and `translations/de.json`

### Both

- `docs/data/features.json` entries
- Config Builder updates

## Design

### HeatPumpCOP

**Category:** Energy | **Output:** Condition | **Default:** off

**Input:** Forecast domain only (Temperature, Humidity). Uses a single-domain `ForecastPoint`-based input -- no new input type needed since the existing pattern for Forecast-only features applies.

**Logic:**

Air-source heat pump COP estimation using a simplified Carnot-based regression:

```
COP = 6.81 - 0.121 * (T_target - T_outside)
```

Where `T_target` is the heating system flow temperature (configurable) and `T_outside` is the forecast outdoor temperature per hour. The formula is a linear approximation of real-world ASHP performance curves, accurate within ~15% for typical operating ranges (-15 to +20 C outdoor).

**Hourly output** over the configured forecast horizon (default 24h):
- COP value (double)
- Recommendation enum per hour

**Recommendation enum:**

| Value | Condition | Meaning |
|-------|-----------|---------|
| `IDEAL` | COP > 4.0 | Excellent efficiency, prioritize heat pump |
| `NORMAL` | COP 2.5-4.0 | Standard operation |
| `INEFFICIENT` | COP 1.5-2.5 | Consider alternatives if available |
| `AVOID` | COP < 1.5 | Very cold, heat pump struggles |

**Headline sensor value:** Current hour's COP (double, rounded to 1 decimal).

**Config:** `HeatPumpConfig` at `Njord:Features:Energy:HeatPump`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Feature toggle |
| `TargetTemperature` | int | 35 | Flow temperature in C |
| `SystemType` | enum | FloorHeating | FloorHeating (35 C default), Radiator (55 C), HotWater (60 C) |
| `ForecastHours` | int | 24 | Hours to forecast (1-48) |

`SystemType` sets the default `TargetTemperature` if the user does not override it explicitly. Validator ensures `TargetTemperature` is in 25-65 range and `ForecastHours` in 1-48.

**DomainRequirements:** Required Forecast: `Temperature`. No optional params.

**PollDepth:** 0 (no history needed, purely forward-looking).

### SolarBatteryStrategy

**Category:** Energy | **Output:** Condition | **Default:** off

**Input:** `ForecastSolarInput` (existing -- combines Forecast + Satellite domains). Already used by `SolarYield` and `SatelliteRadiation` features.

**Logic:**

Compares actual solar production (satellite observations) with forecast production to recommend a battery strategy:

1. **Today actual** (from Satellite GTI, past hours):
   ```
   today_kwh = sum(GTI_hourly * PeakKw * SystemEfficiency) for past daylight hours
   ```

2. **Tomorrow forecast** (from Forecast ShortwaveRadiation or GlobalTiltedIrradiance):
   ```
   tomorrow_kwh = sum(Forecast_GTI_hourly * PeakKw * SystemEfficiency) for next daylight hours
   ```

3. **Confidence** from Ensemble spread if available (wide spread = low confidence).

4. **Strategy decision:**

| Strategy | Condition | Advice |
|----------|-----------|--------|
| `FEED_IN` | Tomorrow sunny (>80% of today) + today surplus | Feed in excess, plenty of sun tomorrow |
| `CHARGE_BATTERY` | Tomorrow cloudy (<50% of today) + today surplus | Store energy, tomorrow will be poor |
| `CONSERVE` | Tomorrow sunny + today underperforming | Hold battery charge, tomorrow will compensate |
| `NORMAL` | Balanced / uncertain | No strong recommendation |

**Headline sensor value:** Strategy enum (translated to localized text via strings.json).

**Config:** `SolarBatteryConfig` at `Njord:Features:Energy:SolarBattery`

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Enabled` | bool | false | Feature toggle |
| `BatteryCapacityKwh` | double | 10.0 | Total battery capacity |
| `UsablePercent` | int | 90 | Usable percentage of capacity |
| `MinSocPercent` | int | 20 | Minimum state of charge to maintain |
| `SystemEfficiency` | double | 0.85 | Overall system efficiency (panels to AC) |

Panel parameters (`PeakKw`, `Azimuth`, `Tilt`) come from the existing `SolarYieldOptions` at `Njord:SolarYield` -- no duplication. Validator ensures `BatteryCapacityKwh` > 0, `UsablePercent` 50-100, `MinSocPercent` 0-50, `SystemEfficiency` 0.5-1.0.

**DomainRequirements:** Required Forecast: `ShortwaveRadiation` or `GlobalTiltedIrradiance`. Required Satellite: `GlobalTilted` (GTI). Optional Ensemble: for confidence scoring.

**PollDepth:** 1 (needs previous cycle for today's cumulative tracking via ring buffer, though most comes from Satellite hourly data in the current LocationForecast).

### Proto Messages

```protobuf
// In weather.proto

enum HeatPumpRecommendation {
  HEAT_PUMP_RECOMMENDATION_UNSPECIFIED = 0;
  HEAT_PUMP_RECOMMENDATION_IDEAL = 1;
  HEAT_PUMP_RECOMMENDATION_NORMAL = 2;
  HEAT_PUMP_RECOMMENDATION_INEFFICIENT = 3;
  HEAT_PUMP_RECOMMENDATION_AVOID = 4;
}

message HourlyCop {
  google.protobuf.Timestamp time = 1;
  double cop = 2;
  HeatPumpRecommendation recommendation = 3;
}

message HeatPumpCop {
  ConditionData condition = 1;
  double cop = 2;                              // current hour
  HeatPumpRecommendation recommendation = 3;   // current hour
  repeated HourlyCop hourly = 4;
}

enum BatteryStrategy {
  BATTERY_STRATEGY_UNSPECIFIED = 0;
  BATTERY_STRATEGY_FEED_IN = 1;
  BATTERY_STRATEGY_CHARGE_BATTERY = 2;
  BATTERY_STRATEGY_CONSERVE = 3;
  BATTERY_STRATEGY_NORMAL = 4;
}

message HourlySolar {
  google.protobuf.Timestamp time = 1;
  double estimated_kwh = 2;
}

message SolarBatteryStrategy {
  ConditionData condition = 1;
  BatteryStrategy strategy = 2;
  double today_kwh = 3;
  double tomorrow_kwh_estimate = 4;
  int32 confidence_percent = 5;
  repeated HourlySolar hourly = 6;
}
```

New oneof cases in `EnrichmentEvent.feature`:
- `HeatPumpCop heat_pump_cop = <next_condition_number>;`
- `SolarBatteryStrategy solar_battery_strategy = <next_condition_number>;`

### i18n

All user-visible text is enum-based, translated in ha-njord:

**strings.json (EN):**
```json
{
  "entity": {
    "sensor": {
      "heat_pump_cop": {
        "name": "Heat pump COP",
        "state_attributes": {
          "recommendation": {
            "name": "Recommendation",
            "state": {
              "ideal": "Ideal",
              "normal": "Normal",
              "inefficient": "Inefficient",
              "avoid": "Avoid"
            }
          }
        }
      },
      "solar_battery_strategy": {
        "name": "Solar battery strategy",
        "state": {
          "feed_in": "Feed in",
          "charge_battery": "Charge battery",
          "conserve": "Conserve",
          "normal": "Normal"
        }
      }
    }
  }
}
```

**translations/de.json:**
```json
{
  "entity": {
    "sensor": {
      "heat_pump_cop": {
        "name": "Waermepumpen-COP",
        "state_attributes": {
          "recommendation": {
            "name": "Empfehlung",
            "state": {
              "ideal": "Ideal",
              "normal": "Normal",
              "inefficient": "Ineffizient",
              "avoid": "Vermeiden"
            }
          }
        }
      },
      "solar_battery_strategy": {
        "name": "Solar-Batteriestrategie",
        "state": {
          "feed_in": "Einspeisen",
          "charge_battery": "Batterie laden",
          "conserve": "Sparen",
          "normal": "Normal"
        }
      }
    }
  }
}
```

## API Budget Impact

- **HeatPumpCOP:** Requires only `Temperature` from Forecast -- almost always already requested by other features. **0 additional API calls.**
- **SolarBatteryStrategy:** Uses `ForecastSolarInput` (existing). `ShortwaveRadiation` may be an additional Forecast variable if not already requested by `SolarYield`. Satellite domain is already polled if `SolarYield` or `SatelliteRadiation` is enabled. Worst case: **0-1 additional Forecast call** per poll cycle if crossing a `ceil(variables/10)` boundary.
- Both features are off by default, so zero impact unless explicitly enabled.

## Non-goals

- Integration with specific heat pump brands or controllers (generic COP estimate only)
- Electricity price integration (requires HA sensor input, Phase 3)
- Battery state-of-charge reading from HA (requires HA sensor input, Phase 3)
- Real-time COP adjustment based on defrost cycles or compressor staging
- Solar panel degradation modeling

## Dependencies

- **ha-njord-i18n-foundation:** Translation key pattern must be in place before adding new translated entities
- **SolarYieldOptions:** `SolarBatteryStrategy` reuses the existing panel config -- no new panel parameters
- **ForecastSolarInput:** Already exists, used by SolarYield/SatelliteRadiation

## Risks

| Risk | Mitigation |
|------|------------|
| COP formula accuracy varies by heat pump model | Document as estimate, not measurement; formula is well-established for generic ASHP. Users with specific models can adjust TargetTemperature |
| SolarBatteryStrategy confidence depends on Ensemble availability | Default to NORMAL (no recommendation) when Ensemble data is missing or confidence is very low |
| SolarYield disabled but SolarBattery enabled | SolarBatteryConfig validator checks that SolarYieldOptions has valid PeakKw; fails startup validation otherwise |
| Satellite data delay (up to 3h) | Already handled by SolarIrradianceBlend -- strategy uses blended data, not raw satellite |
