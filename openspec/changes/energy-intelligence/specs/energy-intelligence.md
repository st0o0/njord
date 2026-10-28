# Energy Intelligence Spec

## HeatPumpCOP

### Behavior

- Category **Energy**, output kind **Condition**, default **off**
- Estimates air-source heat pump COP from forecast outdoor temperature over a configurable horizon
- Headline sensor value: current hour's COP rounded to 1 decimal
- Produces an hourly COP curve with per-hour recommendation

### Input

- Domain: Forecast only
- Required params: `ForecastParams.Temperature`
- Input type: `ForecastInput`
- PollDepth: 0

### Formula

```
COP = 6.81 - 0.121 * (TargetTemperature - OutdoorTemperature)
```

COP is clamped to [1.0, 8.0] to avoid nonsensical values at extreme temperatures.

### Recommendation enum

| Value | Condition | ha-njord EN | ha-njord DE |
|-------|-----------|-------------|-------------|
| Ideal | COP > 4.0 | Ideal | Ideal |
| Normal | 2.5 <= COP <= 4.0 | Normal | Normal |
| Inefficient | 1.5 <= COP < 2.5 | Inefficient | Ineffizient |
| Avoid | COP < 1.5 | Avoid | Vermeiden |

### Config

Section: `Njord:Features:Energy:HeatPump`

| Option | Type | Default | Validation |
|--------|------|---------|------------|
| Enabled | bool | false | — |
| TargetTemperature | int | 35 | 25-65 |
| SystemType | enum | FloorHeating | FloorHeating/Radiator/HotWater |
| ForecastHours | int | 24 | 1-48 |

SystemType sets default TargetTemperature if not explicitly overridden:
- FloorHeating → 35
- Radiator → 55
- HotWater → 60

### Result record

```
HeatPumpCopResult(Location, double Cop, HeatPumpRecommendation Recommendation,
                  ImmutableArray<HourlyCopEntry> Hourly)
    : ConditionFeatureResult(Location)

HourlyCopEntry(DateTimeOffset Time, double Cop, HeatPumpRecommendation Recommendation)
```

### Proto

- Message `HeatPumpCop` with `ConditionData condition`, `double cop`, `HeatPumpRecommendation recommendation`, `repeated HourlyCop hourly`
- Enum `HeatPumpRecommendation` (UNSPECIFIED=0, IDEAL=1, NORMAL=2, INEFFICIENT=3, AVOID=4)
- Message `HourlyCop` with `Timestamp time`, `double cop`, `HeatPumpRecommendation recommendation`
- EnrichmentEvent oneof field number: 76

---

## SolarBatteryStrategy

### Behavior

- Category **Energy**, output kind **Condition**, default **off**
- Compares today's actual solar production (satellite) with tomorrow's forecast to recommend a battery strategy
- Headline sensor value: strategy enum (translated)
- Produces an hourly solar estimate for the next day

### Input

- Domains: Forecast + Satellite (required), Ensemble (optional for confidence)
- Required params: Forecast `ShortwaveRadiation`, Satellite `GlobalTilted`
- Input type: `ForecastSolarInput` (existing)
- PollDepth: 1

### Strategy enum

| Value | Condition | ha-njord EN | ha-njord DE |
|-------|-----------|-------------|-------------|
| FeedIn | Tomorrow >= 80% of today + surplus | Feed in | Einspeisen |
| ChargeBattery | Tomorrow < 50% of today + surplus | Charge battery | Batterie laden |
| Conserve | Tomorrow sunny + today underperforming | Conserve | Sparen |
| Normal | Balanced or uncertain | Normal | Normal |

"Surplus" means today's estimated total > (BatteryCapacity × UsablePercent × (1 - MinSocPercent/100)). "Underperforming" means today's actual < 50% of expected.

### Confidence

When Ensemble data is available, calculate spread of irradiance predictions:
- Narrow spread (CV < 0.2) → high confidence (80-100%)
- Moderate spread (CV 0.2-0.5) → medium confidence (40-80%)
- Wide spread (CV > 0.5) → low confidence (0-40%)

When no Ensemble: confidence = 0 (triggers `Normal` strategy unless the comparison is very clear).

### Config

Section: `Njord:Features:Energy:SolarBattery`

| Option | Type | Default | Validation |
|--------|------|---------|------------|
| Enabled | bool | false | — |
| BatteryCapacityKwh | double | 10.0 | > 0 |
| UsablePercent | int | 90 | 50-100 |
| MinSocPercent | int | 20 | 0-50 |
| SystemEfficiency | double | 0.85 | 0.5-1.0 |

Panel parameters (PeakKw, Azimuth, Tilt) come from `SolarYieldOptions` at `Njord:SolarYield`.

### Result record

```
SolarBatteryStrategyResult(Location, BatteryStrategy Strategy,
                           double TodayKwh, double TomorrowKwhEstimate,
                           int ConfidencePercent,
                           ImmutableArray<HourlySolarEntry> Hourly)
    : ConditionFeatureResult(Location)

HourlySolarEntry(DateTimeOffset Time, double EstimatedKwh)
```

### Proto

- Message `SolarBatteryStrategy` with `ConditionData condition`, `BatteryStrategy strategy`, `double today_kwh`, `double tomorrow_kwh_estimate`, `int32 confidence_percent`, `repeated HourlySolar hourly`
- Enum `BatteryStrategy` (UNSPECIFIED=0, FEED_IN=1, CHARGE_BATTERY=2, CONSERVE=3, NORMAL=4)
- Message `HourlySolar` with `Timestamp time`, `double estimated_kwh`
- EnrichmentEvent oneof field number: 77
