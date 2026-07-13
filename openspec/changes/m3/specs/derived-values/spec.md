## ADDED Requirements

### Requirement: Beaufort scale from wind speed
`DerivedComputer.Beaufort` SHALL accept a `double?` wind speed in m/s and return an `int?` Beaufort number (0–12). If the input is `null`, the result SHALL be `null`. The conversion SHALL follow the standard Beaufort scale boundaries.

#### Scenario: Calm wind
- **WHEN** wind speed is 0.2 m/s
- **THEN** the Beaufort number is 0

#### Scenario: Light breeze
- **WHEN** wind speed is 2.5 m/s
- **THEN** the Beaufort number is 2

#### Scenario: Strong gale
- **WHEN** wind speed is 22.0 m/s
- **THEN** the Beaufort number is 9

#### Scenario: Hurricane force
- **WHEN** wind speed is 35.0 m/s
- **THEN** the Beaufort number is 12

#### Scenario: Null input
- **WHEN** wind speed is null
- **THEN** the result is null

### Requirement: Wind chill from temperature and wind speed
`DerivedComputer.WindChill` SHALL accept `double?` temperature in °C and `double?` wind speed in m/s. It SHALL return the wind chill in °C using the North American formula (T in °C, V in km/h): `13.12 + 0.6215T − 11.37V^0.16 + 0.3965TV^0.16`. Wind speed SHALL be converted from m/s to km/h internally (×3.6). If temperature > 10 °C, wind speed ≤ 4.8 km/h (≤ ~1.33 m/s), or either input is `null`, the result SHALL be `null`.

#### Scenario: Cold and windy
- **WHEN** temperature is −5.0 °C and wind speed is 5.0 m/s (18.0 km/h)
- **THEN** the wind chill is approximately −11.0 °C (within ±0.5 °C)

#### Scenario: Mild temperature
- **WHEN** temperature is 15.0 °C and wind speed is 5.0 m/s
- **THEN** the result is null (T > 10 °C)

#### Scenario: Calm wind
- **WHEN** temperature is −5.0 °C and wind speed is 1.0 m/s (3.6 km/h)
- **THEN** the result is null (V ≤ 4.8 km/h)

#### Scenario: Null inputs
- **WHEN** temperature is null or wind speed is null
- **THEN** the result is null

### Requirement: Dew-point comfort category
`DerivedComputer.DewPointComfort` SHALL accept a `double?` dew-point temperature in °C and return a `string?` comfort category. Categories SHALL be: "dry" (< 10 °C), "comfortable" (10–15 °C), "sticky" (16–18 °C), "oppressive" (19–21 °C), "dangerous" (> 21 °C). If the input is `null`, the result SHALL be `null`.

#### Scenario: Dry air
- **WHEN** dew point is 5.0 °C
- **THEN** the comfort category is "dry"

#### Scenario: Comfortable
- **WHEN** dew point is 12.0 °C
- **THEN** the comfort category is "comfortable"

#### Scenario: Sticky
- **WHEN** dew point is 17.0 °C
- **THEN** the comfort category is "sticky"

#### Scenario: Oppressive
- **WHEN** dew point is 20.0 °C
- **THEN** the comfort category is "oppressive"

#### Scenario: Dangerous
- **WHEN** dew point is 23.0 °C
- **THEN** the comfort category is "dangerous"

#### Scenario: Boundary at 10
- **WHEN** dew point is 10.0 °C
- **THEN** the comfort category is "comfortable"

#### Scenario: Null input
- **WHEN** dew point is null
- **THEN** the result is null

### Requirement: Diurnal temperature amplitude
`DerivedComputer.DiurnalAmplitude` SHALL accept a `ForecastSeries` and a `DateTimeOffset` (now) and return `double?` — the difference between the maximum and minimum `temperature_2m` in the next 24 hours. If fewer than 2 non-null temperature points exist in the window, the result SHALL be `null`.

#### Scenario: Normal diurnal range
- **WHEN** the series has hourly points for the next 24h with min 8.0 °C and max 22.0 °C
- **THEN** the amplitude is 14.0

#### Scenario: Insufficient data
- **WHEN** the series has fewer than 2 non-null temperature values in the next 24h
- **THEN** the result is null

### Requirement: Sunshine percentage
`DerivedComputer.SunshinePercent` SHALL accept a `ForecastSeries` and a `DateTimeOffset` (now) and return `double?` (0.0–100.0). It SHALL compute the ratio of `sunshine_duration` sum to total daylight seconds in the next 24 h. Daylight hours SHALL be determined by counting points where `is_day` = 1. If `sunshine_duration` is not available, the result SHALL be `null`. If no daylight hours exist, the result SHALL be `null`.

#### Scenario: Full sunshine
- **WHEN** the series shows 14h of daylight and sunshine_duration sums to 50400s (14h)
- **THEN** the sunshine percentage is 100.0

#### Scenario: Partial sunshine
- **WHEN** the series shows 14h of daylight and sunshine_duration sums to 25200s (7h)
- **THEN** the sunshine percentage is 50.0

#### Scenario: No sunshine data
- **WHEN** sunshine_duration is null for all points
- **THEN** the result is null

#### Scenario: No daylight
- **WHEN** all points have is_day = 0 (polar night scenario)
- **THEN** the result is null

### Requirement: WMO weather description from weather code
`DerivedComputer.WmoDescription` SHALL accept an `int?` WMO 4677 weather code and return a `string?` English description. The mapping SHALL cover codes 0–99. Unknown codes SHALL return `null`. A `null` input SHALL return `null`.

#### Scenario: Clear sky
- **WHEN** weather code is 0
- **THEN** the description is "Clear sky"

#### Scenario: Mainly clear
- **WHEN** weather code is 1
- **THEN** the description is "Mainly clear"

#### Scenario: Rain slight
- **WHEN** weather code is 61
- **THEN** the description is "Rain: slight"

#### Scenario: Thunderstorm with heavy hail
- **WHEN** weather code is 99
- **THEN** the description is "Thunderstorm with heavy hail"

#### Scenario: Unknown code
- **WHEN** weather code is 150
- **THEN** the result is null

#### Scenario: Null input
- **WHEN** weather code is null
- **THEN** the result is null

### Requirement: Inversion detection heuristic
`DerivedComputer.InversionDetected` SHALL accept `double?` pressure_msl (hPa), `double?` surface_pressure (hPa), `double?` temperature_2m (°C), and `double?` dew_point_2m (°C). It SHALL return `bool?`. An inversion is detected when `pressure_msl − surface_pressure > 3` AND `temperature_2m − dew_point_2m < 3`. If any input is `null`, the result SHALL be `null`.

#### Scenario: Inversion conditions met
- **WHEN** pressure_msl is 1020, surface_pressure is 1015, temperature is 2.0, dew_point is 1.0
- **THEN** the result is true (gap 5 > 3, spread 1.0 < 3)

#### Scenario: No inversion — dry air
- **WHEN** pressure_msl is 1020, surface_pressure is 1015, temperature is 10.0, dew_point is 2.0
- **THEN** the result is false (temperature − dew_point = 8.0 ≥ 3)

#### Scenario: No inversion — low pressure gap
- **WHEN** pressure_msl is 1016, surface_pressure is 1015, temperature is 2.0, dew_point is 1.0
- **THEN** the result is false (gap 1 ≤ 3)

#### Scenario: Null inputs
- **WHEN** any of the four inputs is null
- **THEN** the result is null

### Requirement: DerivedResult aggregates all derived values and serializes to MQTT
`DerivedResult` SHALL be a record holding the location, per-horizon derived values (Beaufort, wind chill, dew-point comfort, WMO description), and scalar values (diurnal amplitude, sunshine percentage, inversion detected). It SHALL expose a static `Compute` method that takes a `ModelSnapshot`, location, horizons, parameter set, and `TimeProvider`, and computes all derived values using median across models at each horizon. It SHALL expose `ToMqttMessages(baseTopic)` that produces `MqttMessage` list: one per horizon (JSON with `beaufort`, `wind_chill`, `dewpoint_comfort`, `wmo_description`) plus one meta message (JSON with `diurnal_amplitude`, `sunshine_pct`, `inversion`).

#### Scenario: Horizon message content
- **WHEN** ToMqttMessages is called for location "lucerne" with baseTopic "njord" and horizon h3
- **THEN** one message has topic `njord/lucerne/derived/h3` with JSON keys `beaufort`, `wind_chill`, `dewpoint_comfort`, `wmo_description`

#### Scenario: Meta message content
- **WHEN** ToMqttMessages is called for location "lucerne" with baseTopic "njord"
- **THEN** one message has topic `njord/lucerne/derived/meta` with JSON keys `diurnal_amplitude`, `sunshine_pct`, `inversion`

#### Scenario: Null values serialize as JSON null
- **WHEN** wind chill is null for a horizon (T > 10 °C)
- **THEN** the JSON payload contains `"wind_chill": null`
