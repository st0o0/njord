# energy-management Specification

## Purpose

Energy-related forecast enrichments: heating demand scoring, heat-pump COP estimation and optimal-hour ranking, shading recommendations, battery charge/discharge strategy, night cooling potential assessment, and a composite EnergyResult that aggregates all values for MQTT publishing.

## Requirements

### Requirement: Heating demand score from outdoor conditions
`EnergyForecaster.HeatingDemand` SHALL accept mean outdoor temperature (°C), mean wind speed (m/s), and mean cloud cover (%). It SHALL return an `int` score 0–100 where higher = more heating needed. The formula SHALL combine: temperature deficit below heating base (weight 0.5), wind chill effect (weight 0.3), and cloud cover radiative cooling (weight 0.2). Null inputs SHALL use neutral sub-score 50.

#### Scenario: Cold windy overcast
- **WHEN** temp is 0 °C, wind 8 m/s, cloud cover 100%
- **THEN** the score is ≥ 80

#### Scenario: Mild calm clear
- **WHEN** temp is 18 °C, wind 1 m/s, cloud cover 10%
- **THEN** the score is ≤ 15

### Requirement: Heat-pump COP estimate from outdoor temperature
`EnergyForecaster.CopEstimate` SHALL accept outdoor temperature (°C), flow temperature (default 35 °C), and Carnot efficiency factor (default 0.45). It SHALL compute COP as `η × T_hot_K / (T_hot_K − T_cold_K)` where temperatures are in Kelvin. If outdoor temp ≥ flow temp, the result SHALL be `null` (physically meaningless). If outdoor temp is null, the result SHALL be `null`.

#### Scenario: Mild outdoor
- **WHEN** outdoor is 10 °C, flow is 35 °C, η is 0.45
- **THEN** COP is approximately 5.56 (0.45 × 308.15 / 25)

#### Scenario: Cold outdoor
- **WHEN** outdoor is -10 °C, flow is 35 °C, η is 0.45
- **THEN** COP is approximately 3.08 (0.45 × 308.15 / 45)

#### Scenario: Outdoor above flow temp
- **WHEN** outdoor is 40 °C, flow is 35 °C
- **THEN** the result is null

#### Scenario: Null outdoor
- **WHEN** outdoor is null
- **THEN** the result is null

### Requirement: COP-optimal hours ranks best hours to run heat pump
`EnergyForecaster.CopOptimalHours` SHALL accept a `ForecastSeries`, temperature parameter, flow temp, Carnot efficiency, count N (default 3), and `DateTimeOffset` (now). It SHALL compute COP at each hour in the next 24h and return the top N hours sorted by COP descending, as `IReadOnlyList<(int HoursFromNow, double Cop)>`. If fewer than N hours have valid COP, return what's available.

#### Scenario: Warmest hours ranked first
- **WHEN** the series has temperatures varying from -5 to 15 °C over 24h
- **THEN** the top 3 hours are the warmest hours with the highest COP values

#### Scenario: Fewer valid hours than N
- **WHEN** only 2 hours have valid temperature data
- **THEN** 2 entries are returned

### Requirement: Shading recommendation from radiation and temperature
`EnergyForecaster.ShadingScore` SHALL accept direct radiation (W/m²), is_day flag, and outdoor temperature (°C). It SHALL return an `int` score 0–100 where higher = deploy shading. Weights: radiation 0.5, daytime 0.1, overheating risk (temp > 25 °C) 0.4. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Peak summer afternoon
- **WHEN** radiation 800 W/m², is_day 1.0, temp 32 °C
- **THEN** the score is ≥ 80

#### Scenario: Overcast cool day
- **WHEN** radiation 100 W/m², is_day 1.0, temp 15 °C
- **THEN** the score is ≤ 20

#### Scenario: Night
- **WHEN** is_day is 0.0
- **THEN** the score is ≤ 15

### Requirement: Battery strategy from solar yield and time of day
`EnergyForecaster.BatteryStrategy` SHALL accept a solar yield score (0–100) and an is_day flag. It SHALL return a `string`: "charge" when solar yield > 60 AND is_day = 1, "discharge" when is_day = 0 OR solar yield < 20, "hold" otherwise.

#### Scenario: High solar daytime
- **WHEN** solar yield 85, is_day 1.0
- **THEN** strategy is "charge"

#### Scenario: Night
- **WHEN** solar yield 0, is_day 0.0
- **THEN** strategy is "discharge"

#### Scenario: Cloudy daytime
- **WHEN** solar yield 40, is_day 1.0
- **THEN** strategy is "hold"

#### Scenario: Low solar daytime
- **WHEN** solar yield 15, is_day 1.0
- **THEN** strategy is "discharge"

### Requirement: Night cooling potential from overnight forecast
`EnergyForecaster.NightCoolingPotential` SHALL accept a `ForecastSeries`, temperature parameter, humidity parameter, wind parameter, rain probability parameter, indoor temp (default 22 °C), and `DateTimeOffset` (now). It SHALL evaluate hours 22:00–06:00 in the next forecast window, compute a ventilation-like score for each overnight hour, and return the best score (0–100). If no overnight hours are in the series, the result SHALL be 0.

#### Scenario: Cool dry night
- **WHEN** overnight hours show temp 16 °C, humidity 40%, wind 3 m/s, rain 0%
- **THEN** the score is ≥ 75

#### Scenario: Warm humid night
- **WHEN** overnight hours show temp 25 °C, humidity 80%, wind 0.5 m/s, rain 30%
- **THEN** the score is ≤ 15

### Requirement: EnergyResult includes pessimistic envelope fields
`EnergyResult` SHALL include additional fields: `HeatingDemandMax` (int), `CopEstimateMin` (double?), and `CopOptimalConservative` (IReadOnlyList of hours). These represent the worst-case scenario across all models for use in conservative automation decisions.

#### Scenario: Heating demand worst case
- **WHEN** 4 models produce per-model heating demand values [40, 55, 45, 62]
- **THEN** HeatingDemandMax=62 and HeatingDemand remains the median/mean-based value

#### Scenario: COP minimum
- **WHEN** 3 models produce COP estimates [3.2, 2.8, 3.5]
- **THEN** CopEstimateMin=2.8

#### Scenario: Conservative optimal hours (intersection)
- **WHEN** model A COP optimal = [2,3,4,5], model B = [3,4,5,6], model C = [4,5]
- **THEN** CopOptimalConservative = [4,5]

#### Scenario: No models provide COP data
- **WHEN** temperature parameter is not available
- **THEN** CopEstimateMin is null and CopOptimalConservative is empty

### Requirement: Energy computation evaluates consensus values instead of per-model aggregation

`EnergyResult.Compute` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. Heating demand, COP estimate, COP-optimal hours, shading, battery strategy, and night cooling SHALL be computed from consensus median values. Pessimistic envelope fields SHALL be derived from consensus spread/confidence interval instead of per-model evaluation.

#### Scenario: Computed from consensus medians
- **WHEN** `EnergyResult.Compute` is called with a `ConsensusSnapshot`
- **THEN** all energy values use consensus median temperature, radiation, wind, and cloud cover

#### Scenario: Pessimistic envelope from consensus spread
- **WHEN** consensus spread is available
- **THEN** `HeatingDemandMax` and `CopEstimateMin` SHALL be derived from the pessimistic end of the consensus confidence interval

#### Scenario: COP-optimal hours from consensus
- **WHEN** COP-optimal hours are computed
- **THEN** the ranking uses consensus median temperatures per hour

### Requirement: EnergyResult aggregates all energy values and serializes to MQTT

`EnergyResult` SHALL derive its location from `ConsensusSnapshot.Location`.

#### Scenario: Energy message content
- **WHEN** energy values are serialized to MQTT
- **THEN** one retained message is published with all fields

#### Scenario: Retained message
- **WHEN** an energy message is published
- **THEN** it is retained

### Requirement: EnergyResult serialization with pinned wire names
`EnergyResult` SHALL have `[property: JsonProperty("...")]` on all positional parameters. The value tuple `(int HoursFromNow, double Cop)` in the `CopOptimal` list SHALL be replaced with a named record (`CopOptimalEntry`) carrying `[JsonProperty]` attributes.

#### Scenario: EnergyResult with CopOptimal entries round-trips through JSON
- **WHEN** an `EnergyResult` with CopOptimal entries, CopEstimate, and CopOptimalConservative is serialized and deserialized
- **THEN** all properties round-trip correctly, CopOptimal entries use named record fields with camelCase wire names
