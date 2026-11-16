## MODIFIED Requirements

### Requirement: Weather feature requires Forecast domain
The WeatherRule's `FeatureDescriptor` SHALL declare `Requirements` including the Forecast domain with at minimum Temperature, WeatherCode, Humidity, WindSpeed, WindDirection, Pressure, ApparentTemperature, CloudCover, DewPoint, Visibility, WindGusts, Precipitation, and **IsDay** parameters.

#### Scenario: Weather feature requests IsDay parameter
- **WHEN** njord starts with WeatherRule enabled
- **THEN** the resolved DomainRequirements SHALL include `ForecastParams.IsDay`

#### Scenario: IsDay has zero API budget impact
- **WHEN** the IsDay parameter is added to the Forecast requirements
- **THEN** the call weight SHALL NOT increase because IsDay is a free parameter on the Open-Meteo API (not counted in the variables-per-10 formula)

### Requirement: WeatherResult contains current conditions and forecast
`WeatherResult` SHALL contain the current weather conditions (temperature, humidity, pressure, wind speed, wind bearing, weather code/condition, cloud cover, dew point, visibility, precipitation, wind gusts) and an hourly forecast array. Each forecast entry SHALL contain at minimum: timestamp, temperature, humidity, precipitation, wind speed, wind bearing, condition, precipitation probability, cloud cover, **weather_code (int)**, and **is_day (bool)**. `WeatherResult` SHALL include `WeatherCode` (int) and `IsDay` (bool) fields alongside the existing `Condition` string. `WeatherForecastPoint` SHALL include `WeatherCode` (int) and `IsDay` (bool) fields alongside its existing `Condition` string.

#### Scenario: WeatherResult carries WMO code and is_day
- **WHEN** the current ForecastPoint has WeatherCode = 3 and IsDay = 1.0
- **THEN** the WeatherResult SHALL have WeatherCode = 3, IsDay = true, and Condition = "overcast"

#### Scenario: WeatherForecastPoint carries WMO code and is_day
- **WHEN** a future ForecastPoint has WeatherCode = 61 and IsDay = 0.0
- **THEN** the WeatherForecastPoint SHALL have WeatherCode = 61, IsDay = false, and Condition = "rain"

#### Scenario: Condition string remains as human-readable label
- **WHEN** a WeatherResult is produced
- **THEN** the Condition field SHALL still contain a human-readable label derived from the WMO code (e.g. "clear", "rain", "thunderstorm")

#### Scenario: Missing IsDay defaults to daytime
- **WHEN** the ForecastPoint has IsDay = null
- **THEN** the WeatherResult SHALL default IsDay to true

## ADDED Requirements

### Requirement: ha-njord uses WMO codes for condition mapping
The ha-njord weather entity SHALL derive its HA condition string from the `weather_code` and `is_day` fields using `condition_mapper.map_condition()`, not from the `condition` string field. The `_CONDITION_MAP` dictionary in `weather.py` SHALL be removed.

#### Scenario: Daytime clear sky shows sunny icon
- **WHEN** the gRPC WeatherValue has weather_code = 0 and is_day = true
- **THEN** the HA weather entity condition SHALL be "sunny"

#### Scenario: Nighttime clear sky shows clear-night icon
- **WHEN** the gRPC WeatherValue has weather_code = 0 and is_day = false
- **THEN** the HA weather entity condition SHALL be "clear-night"

#### Scenario: Hourly forecast points use weather_code for condition
- **WHEN** an hourly forecast point has weather_code = 65 and is_day = true
- **THEN** the forecast entry condition SHALL be "pouring" (mapped via condition_mapper)

#### Scenario: Every WMO code maps to a valid HA condition
- **WHEN** any valid WMO weather code (0-99) is received
- **THEN** condition_mapper SHALL return a valid HA condition string that HA recognizes with an icon
