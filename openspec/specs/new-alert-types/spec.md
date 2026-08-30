# new-alert-types Specification

## Purpose

Five additional alert evaluators (Ice, WindChill, Visibility, TropicalNight, Humidity) that extend the alert system beyond the original nine threshold-based alerts, along with the enum values, topic segment mappings, and configuration entries they require.

## Requirements

### Requirement: Ice alert evaluates freezing rain risk from rain at near-freezing temperatures

`AlertEvaluator.EvaluateIce` SHALL scan `rain` and `temperature_2m` consensus medians over the next 24 hours. Ice conditions exist when `rain` median > 0 mm AND `temperature_2m` median <= ice threshold (default 2.0 C). Soil temperature (`soil_temperature_0cm`) SHALL escalate severity when available and <= 0 C.

Severity:
- Yellow: temp 0-2 C AND rain > 0 (glaze risk)
- Orange: temp <= 0 C AND rain > 0 (freezing rain likely)
- Red: temp <= 0 C AND rain > 0 AND soil_temp_0cm <= 0 C (immediate freeze on contact)

Confidence SHALL be the average agreement across horizons where ice conditions are met. Attributes SHALL include `expected_low` (min temperature median), `rain_hours` (count of hours with rain > 0 at ice temperatures), and `soil_frozen` (boolean, true when soil_temp_0cm <= 0 C).

If `rain` parameter is not in the resolved parameter set, the evaluator SHALL return `Alert.None`.

#### Scenario: Rain at near-freezing temperature
- **WHEN** consensus median rain > 0 mm and temperature median is 1.5 C in at least one horizon
- **THEN** an Ice alert with severity Yellow is produced

#### Scenario: Freezing rain
- **WHEN** consensus median rain > 0 mm and temperature median is -1.0 C
- **THEN** an Ice alert with severity Orange is produced

#### Scenario: Freezing rain on frozen ground
- **WHEN** consensus median rain > 0 mm, temperature median is -1.0 C, and soil_temperature_0cm median is -2.0 C
- **THEN** an Ice alert with severity Red is produced

#### Scenario: Snow but no rain
- **WHEN** precipitation > 0 but rain == 0 and temperature is -3.0 C
- **THEN** no Ice alert is produced (severity None) because the precipitation is snow, not rain

#### Scenario: Rain parameter not available
- **WHEN** the `rain` parameter is not in the resolved parameter set
- **THEN** the evaluator returns `Alert.None(AlertType.Ice)`

#### Scenario: Soil temperature not available
- **WHEN** rain > 0 and temp <= 0 C but soil_temperature_0cm is not available
- **THEN** severity is Orange (cannot escalate to Red without soil data)

### Requirement: WindChill alert evaluates extreme apparent temperature driven by wind

`AlertEvaluator.EvaluateWindChill` SHALL scan `apparent_temperature` consensus medians over the next 24 hours. WindChill conditions exist when the minimum apparent temperature median falls below configured thresholds (default [-10, -20, -30] C).

Severity SHALL be determined by the lowest threshold crossed (highest index in the array = Red).

Confidence SHALL be the average agreement at horizons where apparent temperature is below the Yellow threshold. Attributes SHALL include `felt_temperature` (minimum apparent temperature median), `wind_factor` (difference between temperature_2m and apparent_temperature at the coldest horizon), and `exposure_risk` ("frostbite_30min" for Orange, "frostbite_10min" for Red, null otherwise).

#### Scenario: Moderate wind chill
- **WHEN** consensus median apparent temperature is -12 C with agreement 0.8
- **THEN** a WindChill alert with severity Yellow and confidence 0.8 is produced

#### Scenario: Severe wind chill
- **WHEN** consensus median apparent temperature is -22 C
- **THEN** a WindChill alert with severity Orange is produced with exposure_risk "frostbite_30min"

#### Scenario: Extreme wind chill
- **WHEN** consensus median apparent temperature is -35 C
- **THEN** a WindChill alert with severity Red is produced with exposure_risk "frostbite_10min"

#### Scenario: Cold but no extreme wind chill
- **WHEN** consensus median apparent temperature is -5 C
- **THEN** no WindChill alert is produced (severity None)

#### Scenario: Wind factor attribute
- **WHEN** temperature_2m median is -5 C and apparent_temperature median is -15 C at the coldest horizon
- **THEN** the wind_factor attribute is 10.0

### Requirement: Visibility alert evaluates direct visibility parameter against safety thresholds

`AlertEvaluator.EvaluateVisibility` SHALL scan `visibility` consensus medians over the next 24 hours. Severity SHALL be determined by the minimum visibility median against configured thresholds (default [1000, 200, 50] meters, where values below the first = Yellow, below the second = Orange, below the third = Red).

Confidence SHALL be the average agreement at horizons where visibility is below the Yellow threshold. Attributes SHALL include `min_visibility` (minimum visibility median in meters) and `hours_below_1000m` (count of horizons with visibility median < 1000m).

#### Scenario: Reduced visibility
- **WHEN** consensus median visibility is 800m in at least one horizon
- **THEN** a Visibility alert with severity Yellow is produced

#### Scenario: Dense fog visibility
- **WHEN** consensus median visibility is 150m
- **THEN** a Visibility alert with severity Orange is produced

#### Scenario: Near-zero visibility
- **WHEN** consensus median visibility is 30m
- **THEN** a Visibility alert with severity Red is produced

#### Scenario: Good visibility
- **WHEN** consensus median visibility is above 1000m at all horizons
- **THEN** no Visibility alert is produced (severity None)

#### Scenario: Visibility parameter not available
- **WHEN** the `visibility` parameter is not in the resolved parameter set
- **THEN** the evaluator returns `Alert.None(AlertType.Visibility)`

### Requirement: TropicalNight alert evaluates overnight minimum temperature

`AlertEvaluator.EvaluateTropicalNight` SHALL scan `temperature_2m` consensus medians at night hours (determined by `is_day == 0`) over the next 24 hours. The minimum night temperature median SHALL be compared against configured thresholds (default [20, 23, 25] C).

Severity SHALL be determined by the lowest threshold exceeded (highest index = Red). Confidence SHALL be the average agreement at night horizons where temperature exceeds the Yellow threshold. Attributes SHALL include `night_minimum` (minimum night temperature median) and `hours_above_20` (count of night hours with median > 20 C).

If no night hours exist in the 24h window (polar day), the evaluator SHALL return `Alert.None`.

#### Scenario: Tropical night
- **WHEN** minimum consensus median temperature during night hours is 21 C
- **THEN** a TropicalNight alert with severity Yellow is produced

#### Scenario: Severe tropical night
- **WHEN** minimum consensus median temperature during night hours is 24 C
- **THEN** a TropicalNight alert with severity Orange is produced

#### Scenario: Extreme tropical night
- **WHEN** minimum consensus median temperature during night hours is 26 C
- **THEN** a TropicalNight alert with severity Red is produced

#### Scenario: Cool night
- **WHEN** minimum consensus median temperature during night hours is 15 C
- **THEN** no TropicalNight alert is produced (severity None)

#### Scenario: No night hours in window
- **WHEN** `is_day` is 1 for all 24 horizons (polar day)
- **THEN** the evaluator returns `Alert.None(AlertType.TropicalNight)`

### Requirement: Humidity alert evaluates dewpoint for oppressive mugginess

`AlertEvaluator.EvaluateHumidity` SHALL scan `dew_point_2m` consensus medians at daytime hours (determined by `is_day > 0`) over the next 24 hours. The maximum daytime dewpoint median SHALL be compared against configured thresholds (default [16, 21, 24] C).

Severity SHALL be determined by the highest threshold exceeded. Confidence SHALL be the average agreement at daytime horizons where dewpoint exceeds the Yellow threshold. Attributes SHALL include `max_dewpoint` (maximum daytime dewpoint median) and `comfort_level` ("muggy" for Yellow, "oppressive" for Orange, "tropical" for Red).

#### Scenario: Muggy conditions
- **WHEN** maximum consensus median dewpoint during daytime is 18 C
- **THEN** a Humidity alert with severity Yellow and comfort_level "muggy" is produced

#### Scenario: Oppressive humidity
- **WHEN** maximum consensus median dewpoint during daytime is 22 C
- **THEN** a Humidity alert with severity Orange and comfort_level "oppressive" is produced

#### Scenario: Tropical humidity
- **WHEN** maximum consensus median dewpoint during daytime is 25 C
- **THEN** a Humidity alert with severity Red and comfort_level "tropical" is produced

#### Scenario: Comfortable humidity
- **WHEN** maximum consensus median dewpoint during daytime is 12 C
- **THEN** no Humidity alert is produced (severity None)

#### Scenario: No daytime hours
- **WHEN** `is_day` is 0 for all 24 horizons (polar night)
- **THEN** the evaluator returns `Alert.None(AlertType.Humidity)`

### Requirement: AlertType enum includes new values

`AlertType` SHALL include the values: Ice, WindChill, Visibility, TropicalNight, Humidity (in addition to existing values).

#### Scenario: New enum values exist
- **WHEN** `AlertType` is inspected
- **THEN** it contains Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity

### Requirement: AlertTypeExtensions maps new types to topic segments

`AlertTypeExtensions.ToTopicSegment` SHALL map new alert types to kebab-case topic segments: Ice -> "ice", WindChill -> "wind-chill", Visibility -> "visibility", TropicalNight -> "tropical-night", Humidity -> "humidity".

#### Scenario: New topic segments
- **WHEN** `AlertType.WindChill.ToTopicSegment()` is called
- **THEN** it returns "wind-chill"

#### Scenario: TropicalNight topic segment
- **WHEN** `AlertType.TropicalNight.ToTopicSegment()` is called
- **THEN** it returns "tropical-night"

### Requirement: New alert types have configurable thresholds in AlertOptions

`AlertOptions` SHALL include: `IceThreshold` (double, default 2.0), `WindChillThresholds` (double[], default [-10, -20, -30]), `VisibilityThresholds` (double[], default [1000, 200, 50]), `TropicalNightThresholds` (double[], default [20, 23, 25]), `HumidityThresholds` (double[], default [16, 21, 24]).

#### Scenario: Custom ice threshold
- **WHEN** `AlertOptions.IceThreshold` is set to 3.0
- **THEN** the Ice evaluator uses 3.0 C as the temperature threshold

#### Scenario: Default wind chill thresholds
- **WHEN** no override is configured for WindChillThresholds
- **THEN** the evaluator uses [-10, -20, -30] C
