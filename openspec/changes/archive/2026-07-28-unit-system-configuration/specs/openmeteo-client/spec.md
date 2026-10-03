## MODIFIED Requirements

### Requirement: Forecast fetch per location and model
The client SHALL fetch `GET {OpenMeteoBaseUrl}/v1/forecast?latitude={lat}&longitude={lon}&models={id}` where `OpenMeteoBaseUrl` is read from `NjordOptions.OpenMeteoBaseUrl` (default `https://api.open-meteo.com`). The request SHALL include the hourly variables from the active parameter set and, when daily parameters are active, the daily variables. The request SHALL include unit query parameters derived from the configured `UnitSystem`: for `Metric`, `temperature_unit=celsius&wind_speed_unit=ms&precipitation_unit=mm`; for `Imperial`, `temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch`. The request SHALL always include `timeformat=unixtime` and `forecast_days` from configuration. No authentication header or API key SHALL be sent. One successful call SHALL yield one `ModelForecast` with hourly points covering at least +72 h and daily points spanning `forecast_days`. The `FetchAsync` method SHALL NOT accept a `CycleId` parameter — the `CycleId` SHALL be provided via the `WeightedTarget` that carries the fetch context. After deserialization, the client SHALL apply `UnitConverter.Convert` to each value of each parameter that requires server-side conversion (pressure, snowfall, snow_depth, visibility, freezing_level_height) when the unit system is `Imperial`.

#### Scenario: Successful fetch maps hourly and daily to domain
- **WHEN** the API returns a valid single-model payload for `icon_eu` with the configured hourly and daily variables
- **THEN** the client returns `Success` with a `ModelForecast` whose hourly series contains points including the +72 h horizon and whose daily series contains `forecast_days` entries

#### Scenario: Request variables match active configuration
- **WHEN** the active hourly set contains 25 variables and the active daily set contains 12 variables
- **THEN** the HTTP request's `hourly` parameter lists exactly those 25 variable names and the `daily` parameter lists exactly those 12 variable names

#### Scenario: API call weight is determined by hourly variable count
- **WHEN** the active hourly set contains 25 variables
- **THEN** the effective API call weight is `ceil(25/10) = 3`

#### Scenario: Custom base URL is used when configured
- **WHEN** `NjordOptions.OpenMeteoBaseUrl` is set to `http://localhost:8080`
- **THEN** the client SHALL send requests to `http://localhost:8080/v1/forecast` instead of the default

#### Scenario: Metric unit params in request
- **WHEN** the unit system is `Metric`
- **THEN** the request URL contains `temperature_unit=celsius&wind_speed_unit=ms&precipitation_unit=mm`

#### Scenario: Imperial unit params in request
- **WHEN** the unit system is `Imperial`
- **THEN** the request URL contains `temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch`

#### Scenario: Server-side conversion applied for pressure in Imperial
- **WHEN** the unit system is `Imperial` and the API returns `pressure_msl: 1013.25` (always in hPa)
- **THEN** the deserialized value in the `ModelForecast` is approximately `29.92` (inHg)

#### Scenario: Server-side conversion applied for snowfall in Imperial
- **WHEN** the unit system is `Imperial` and the API returns `snowfall: 5.0` (always in cm)
- **THEN** the deserialized value in the `ModelForecast` is approximately `1.969` (in)

### Requirement: Response units are verified
The client SHALL verify that the returned `hourly_units` report the expected units for every active parameter that has a verifiable unit expectation. The expected units SHALL be derived from the configured `UnitSystem` and the `UnitResolver`: temperature parameters SHALL match the unit system's temperature unit, wind parameters SHALL match the wind speed unit, precipitation parameters SHALL match the precipitation unit, pressure parameters SHALL always be `hPa` (no API-side conversion), time SHALL be `unixtime`. A mismatch SHALL return `Failure(MalformedPayload)`.

#### Scenario: Metric unit verification
- **WHEN** unit system is `Metric` and the API returns `hourly_units` with `temperature_2m: "°C"` and `wind_speed_10m: "m/s"`
- **THEN** verification passes

#### Scenario: Imperial unit verification
- **WHEN** unit system is `Imperial` and the API returns `hourly_units` with `temperature_2m: "°F"` and `wind_speed_10m: "mph"`
- **THEN** verification passes

#### Scenario: Pressure unit is always hPa regardless of unit system
- **WHEN** unit system is `Imperial` and the API returns `hourly_units` with `pressure_msl: "hPa"`
- **THEN** verification passes (pressure has no API-side unit option)

#### Scenario: Dynamic unit check catches drift
- **WHEN** `surface_pressure` is in the active set and the response reports `surface_pressure` unit as `Pa` instead of `hPa`
- **THEN** the client returns `Failure(MalformedPayload)`
