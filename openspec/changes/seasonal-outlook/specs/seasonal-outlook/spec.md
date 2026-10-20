## ADDED Requirements

### Requirement: Seasonal client fetches a 7-month outlook
The system SHALL provide an `ISeasonalClient` in `Njord.OpenMeteo` that fetches the seasonal forecast from `seasonal-api.open-meteo.com/v1/seasonal` using `OpenMeteoRequestBuilder`. The client SHALL receive its variables from the caller and return a typed `SeasonalResponse` with 6-hourly data for temperature and precipitation.

#### Scenario: Successful seasonal fetch
- **WHEN** `FetchAsync` is called with valid coordinates and variables
- **THEN** the request URL SHALL contain the coordinates (invariant culture), the supplied variables, `timeformat=unixtime` and `timezone=UTC`
- **AND** the response SHALL contain 6-hourly data spanning at least 3 months

### Requirement: Seasonal service fetches daily and computes monthly anomalies
The `SeasonalOutlookService` SHALL be a hosted service that fetches seasonal data once every 24 hours per location with coordinates. It SHALL aggregate 6-hourly data into monthly temperature anomalies (°C) and precipitation anomalies (%) against a baseline owned by the service itself (anomalies returned by the Seasonal API, or a private monthly-normal cache; no shared climate store). The result SHALL be written to `ISeasonalOutlookStore`.

#### Scenario: Monthly anomaly computed
- **WHEN** the seasonal forecast shows a January mean of 5°C and the service baseline for January is 2°C
- **THEN** the stored outlook SHALL contain a January entry with TempAnomaly = +3.0

#### Scenario: Precipitation anomaly in percent
- **WHEN** the forecast monthly precipitation is 60 mm and the baseline is 80 mm
- **THEN** the stored entry SHALL have PrecipAnomaly = -25.0

#### Scenario: Service runs only when enabled
- **WHEN** `EnrichmentOptions.SeasonalOutlook` is false
- **THEN** the service SHALL NOT call the seasonal API and the store SHALL remain empty

#### Scenario: Service retries on failure
- **WHEN** a seasonal fetch fails
- **THEN** the service SHALL retry after 15 minutes (per `TimeProvider`) and keep the previous outlook in the store

#### Scenario: Location without coordinates is skipped
- **WHEN** a configured location has no coordinates
- **THEN** the service SHALL NOT fetch for it and SHALL log a warning

#### Scenario: Request budget
- **WHEN** the service runs for one location over 24 hours without failures
- **THEN** it SHALL issue exactly one seasonal API request

### Requirement: Confidence from spread
The monthly confidence level SHALL be derived from the temperature spread (standard deviation across the month). Below 1.5°C SHALL be High, below 3.0°C Medium, otherwise Low. Wire text SHALL come from an explicit mapping covering every member.

#### Scenario: Low spread means high confidence
- **WHEN** the temperature spread for February is 1.0°C
- **THEN** the February outlook SHALL have Confidence High

#### Scenario: Boundary values
- **WHEN** the spread is exactly 1.5°C
- **THEN** Confidence SHALL be Medium
- **AND** when the spread is exactly 3.0°C, Confidence SHALL be Low

### Requirement: SeasonalOutlookHandler declares its requirements
`SeasonalOutlookHandler` SHALL be an `IDomainHandler` with `ConfigKey` "SeasonalOutlook" matching the `EnrichmentOptions.SeasonalOutlook` toggle and `Requirements` of `new(Weather: WeatherParam.Temperature)`. It SHALL be registered in `AddWeatherDomain`.

#### Scenario: Requirements pinned
- **WHEN** the handler's `Requirements` are read
- **THEN** only `Weather` SHALL be non-default and it SHALL equal `WeatherParam.Temperature`

#### Scenario: Toggle default
- **WHEN** no configuration is supplied
- **THEN** `EnrichmentOptions.SeasonalOutlook` SHALL be false and `EnrichmentResolution.EnabledHandlers` SHALL exclude the handler

### Requirement: SeasonalOutlookHandler emits 3-month outlook
On each weather update the handler SHALL read the store for the forecast's location and emit one `SeasonalOutlookComputed` event with the next 3 calendar months. It SHALL return no events when the store has no data for the location.

#### Scenario: 3-month outlook emitted
- **WHEN** weather data arrives, the clock is in October, and the store has data for November, December and January
- **THEN** a `SeasonalOutlookComputed` event SHALL be emitted with 3 `MonthlyOutlook` entries for those months

#### Scenario: No data returns no event
- **WHEN** weather data arrives but the store is empty for the location
- **THEN** no event SHALL be emitted

#### Scenario: Months beyond three are dropped
- **WHEN** the store holds 7 months
- **THEN** the event SHALL contain only the first 3 months after the current month

### Requirement: Outlook is delivered as a lifestyle update
`SeasonalOutlookComputed` SHALL flatten through `ToLifestyleUpdate()` into a `LifestyleUpdate` with keys `month_<n>`, `month_<n>_temp_anomaly`, `month_<n>_precip_anomaly` and `month_<n>_confidence` for n = 1..3, delivered over gRPC and MQTT through the existing lifestyle path.

#### Scenario: Wire shape
- **WHEN** an outlook with 3 months is mapped
- **THEN** the update SHALL contain 12 entries, with `month_1` equal to the first month ("2026-11")
