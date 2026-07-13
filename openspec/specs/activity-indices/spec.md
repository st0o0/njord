# activity-indices Specification

## Purpose

Activity and environmental index scores computed from forecast data: lifestyle scores (laundry drying, outdoor, running, cycling, BBQ, irrigation, ventilation, solar yield), degree days (heating/cooling), frost protection, VPD plant stress, and a unified IndexResult that serializes all indices to MQTT.

## Requirements

### Requirement: Laundry drying score from temperature, humidity, wind, rain, sunshine
`IndexScorer.LaundryDrying` SHALL accept mean temperature (°C), mean relative humidity (%), mean wind speed (m/s), mean precipitation probability (%), and sunshine percentage (%) over the next 24h. It SHALL return an `int` score 0–100. The formula SHALL weight: temperature 0.3, humidity 0.25, wind 0.2, rain probability 0.15, sunshine 0.1. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Perfect drying day
- **WHEN** temp is 28 °C, humidity 35%, wind 5 m/s, rain prob 0%, sunshine 100%
- **THEN** the score is ≥ 90

#### Scenario: Cold rainy day
- **WHEN** temp is 5 °C, humidity 90%, wind 1 m/s, rain prob 80%, sunshine 0%
- **THEN** the score is ≤ 15

### Requirement: Outdoor score from temperature comfort, rain, wind, cloud cover
`IndexScorer.OutdoorScore` SHALL accept mean temperature (°C), mean precipitation probability (%), mean wind speed (m/s), and mean cloud cover (%) over the next 24h. It SHALL return an `int` score 0–100. Temperature comfort SHALL use a bell curve peaking at 22 °C. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Pleasant spring day
- **WHEN** temp is 22 °C, rain prob 5%, wind 2 m/s, cloud cover 20%
- **THEN** the score is ≥ 85

#### Scenario: Stormy winter day
- **WHEN** temp is 2 °C, rain prob 90%, wind 12 m/s, cloud cover 100%
- **THEN** the score is ≤ 10

### Requirement: Running comfort with optimal temperature range
`IndexScorer.RunningComfort` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), and mean precipitation probability (%). It SHALL return an `int` score 0–100. Optimal temperature range SHALL be 5–20 °C. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Ideal running weather
- **WHEN** temp is 12 °C, humidity 45%, wind 2 m/s, rain prob 0%
- **THEN** the score is ≥ 85

#### Scenario: Hot and humid
- **WHEN** temp is 35 °C, humidity 80%, wind 0.5 m/s, rain prob 10%
- **THEN** the score is ≤ 20

### Requirement: Cycling comfort penalizes wind more heavily
`IndexScorer.CyclingComfort` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), and mean precipitation probability (%). It SHALL return an `int` score 0–100. Wind SHALL be weighted 0.3 (vs 0.2 for running). Null inputs SHALL use neutral sub-score 50.

#### Scenario: Calm warm day
- **WHEN** temp is 18 °C, humidity 50%, wind 1.5 m/s, rain prob 0%
- **THEN** the score is ≥ 85

#### Scenario: Very windy
- **WHEN** temp is 18 °C, humidity 50%, wind 12 m/s, rain prob 0%
- **THEN** the score is ≤ 40

### Requirement: BBQ weather from warmth, dryness, light wind
`IndexScorer.BbqWeather` SHALL accept mean temperature (°C), mean humidity (%), mean wind speed (m/s), and mean precipitation probability (%). It SHALL return an `int` score 0–100. Rain probability SHALL be weighted 0.35 (critical). Null inputs SHALL use neutral sub-score 50.

#### Scenario: Perfect BBQ
- **WHEN** temp is 26 °C, humidity 40%, wind 2 m/s, rain prob 0%
- **THEN** the score is ≥ 90

#### Scenario: Rain kills the BBQ
- **WHEN** temp is 26 °C, humidity 40%, wind 2 m/s, rain prob 80%
- **THEN** the score is ≤ 30

### Requirement: Irrigation need from rain absence, heat, dryness, evapotranspiration
`IndexScorer.IrrigationNeed` SHALL accept mean precipitation probability (%), mean temperature (°C), mean humidity (%), and mean evapotranspiration (mm). It SHALL return an `int` score 0–100. High score = water your garden. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Hot dry day
- **WHEN** rain prob 0%, temp 32 °C, humidity 30%, ET 6.0 mm
- **THEN** the score is ≥ 85

#### Scenario: Rainy day
- **WHEN** rain prob 90%, temp 15 °C, humidity 80%, ET 1.0 mm
- **THEN** the score is ≤ 15

### Requirement: Heating and cooling degree days
`IndexScorer.HeatingDegreeDays` SHALL accept mean daily temperature (°C) and base temperature (default 18 °C). It SHALL return `double` = max(0, base − meanTemp). `IndexScorer.CoolingDegreeDays` SHALL accept mean daily temperature and base (default 24 °C). It SHALL return `double` = max(0, meanTemp − base).

#### Scenario: Cold day heating
- **WHEN** mean temp is 5 °C and base is 18
- **THEN** HDD is 13.0

#### Scenario: Hot day cooling
- **WHEN** mean temp is 30 °C and base is 24
- **THEN** CDD is 6.0

#### Scenario: Mild day
- **WHEN** mean temp is 20 °C
- **THEN** HDD (base 18) is 0.0 and CDD (base 24) is 0.0

### Requirement: Solar yield score from radiation, cloud cover, temperature
`IndexScorer.SolarYield` SHALL accept mean shortwave radiation (W/m²), mean cloud cover (%), and mean temperature (°C). It SHALL return an `int` score 0–100. Temperature efficiency SHALL decrease ~0.4%/°C above 25 °C. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Clear cool day
- **WHEN** radiation 800 W/m², cloud cover 10%, temp 18 °C
- **THEN** the score is ≥ 85

#### Scenario: Overcast hot day
- **WHEN** radiation 150 W/m², cloud cover 90%, temp 38 °C
- **THEN** the score is ≤ 20

### Requirement: Ventilation score from outdoor-indoor delta, humidity, wind, rain
`IndexScorer.Ventilation` SHALL accept mean outdoor temperature (°C), indoor temperature (default 22 °C), mean humidity (%), mean wind speed (m/s), and mean precipitation probability (%). It SHALL return an `int` score 0–100. High score = open the windows. Null inputs SHALL use neutral sub-score 50.

#### Scenario: Cool evening breeze
- **WHEN** outdoor 17 °C, indoor 22 °C, humidity 45%, wind 3 m/s, rain prob 0%
- **THEN** the score is ≥ 85

#### Scenario: Hot humid outside
- **WHEN** outdoor 30 °C, indoor 22 °C, humidity 80%, wind 1 m/s, rain prob 0%
- **THEN** the score is ≤ 15

### Requirement: Frost protection hours and confidence
`IndexScorer.FrostProtection` SHALL accept a `ForecastSeries`, temperature parameter, and `DateTimeOffset` (now). It SHALL scan the next 48h for the first point where temperature ≤ 0 °C. It SHALL return `(int? HoursUntilFrost, double? Confidence)?`. Confidence is from multi-model agreement (fraction predicting frost). If no frost found, the result SHALL be `null`.

#### Scenario: Frost in 8 hours
- **WHEN** the series shows temp ≤ 0 at T0+8h
- **THEN** HoursUntilFrost is 8

#### Scenario: No frost risk
- **WHEN** all temperatures in the next 48h are > 0
- **THEN** the result is null

### Requirement: VPD plant stress category
`IndexScorer.VpdCategory` SHALL accept temperature (°C) and relative humidity (%). It SHALL compute VPD using the Magnus formula: `SVP = 0.6108 × exp(17.27 × T / (T + 237.3))`, `VPD = SVP × (1 − RH/100)`. Category SHALL be: "low" (< 0.4 kPa), "optimal" (0.4–1.2), "high" (1.2–2.0), "critical" (> 2.0). If either input is null, the result SHALL be `null`.

#### Scenario: Optimal greenhouse
- **WHEN** temp is 25 °C and humidity is 60%
- **THEN** VPD is approximately 1.27 kPa and category is "high"

#### Scenario: Very humid
- **WHEN** temp is 20 °C and humidity is 90%
- **THEN** VPD is approximately 0.23 kPa and category is "low"

#### Scenario: Null inputs
- **WHEN** temperature or humidity is null
- **THEN** the result is null

### Requirement: IndexResult aggregates all indices and serializes to MQTT
`IndexResult` SHALL be a record holding the location and all index values. It SHALL expose a static `Compute` method taking a `ModelSnapshot`, location, parameter set, `TimeProvider`, and `IndexOptions`. It SHALL expose `ToMqttMessages(baseTopic)` producing a single `MqttMessage` on topic `{baseTopic}/{location}/indices` with a flat JSON payload containing all index values. Null values SHALL serialize as JSON null.

#### Scenario: Index message content
- **WHEN** ToMqttMessages is called for location "lucerne" with baseTopic "njord"
- **THEN** one message has topic `njord/lucerne/indices` with JSON keys for all indices

#### Scenario: Retained message
- **WHEN** ToMqttMessages produces a message
- **THEN** the message has Retain = true
