## ADDED Requirements

### Requirement: Seasonal client fetches 7-month outlook
The system SHALL provide an `ISeasonalClient` that fetches the seasonal forecast from `seasonal-api.open-meteo.com/v1/seasonal` using the `OpenMeteoRequestBuilder`. The response SHALL be a typed `SeasonalResponse` with 6-hourly data for temperature and precipitation over 7 months.

#### Scenario: Successful seasonal fetch
- **WHEN** `FetchAsync` is called with valid coordinates
- **THEN** the response SHALL contain 6-hourly temperature and precipitation data spanning at least 3 months

### Requirement: Seasonal service fetches daily and computes monthly anomalies
The `SeasonalOutlookService` SHALL be a hosted service that fetches seasonal data once every 24 hours per location. It SHALL aggregate 6-hourly data into monthly means and compare each month with the climate baseline from `IClimateBaselineStore`. The result SHALL be cached in `ISeasonalOutlookStore`.

#### Scenario: Monthly anomaly computed
- **WHEN** the seasonal forecast shows a January mean of 5°C and the climate baseline January mean is 2°C
- **THEN** the cached outlook SHALL contain a January entry with TempAnomaly = +3.0

#### Scenario: Service runs only when enabled
- **WHEN** `EnrichmentOptions.SeasonalOutlook` is false
- **THEN** the service SHALL NOT fetch seasonal data and the store SHALL remain empty

#### Scenario: Service retries on failure
- **WHEN** a seasonal fetch fails
- **THEN** the service SHALL retry after 15 minutes and keep the previous outlook

### Requirement: Confidence from ensemble spread
The monthly confidence level SHALL be derived from the ensemble temperature spread (standard deviation across the month). StdDev below 1.5°C → High, below 3.0°C → Medium, otherwise Low.

#### Scenario: Low spread means high confidence
- **WHEN** the ensemble temperature spread for February is 1.0°C
- **THEN** the February outlook SHALL have Confidence = "High"

### Requirement: SeasonalOutlookHandler emits 3-month outlook
The handler SHALL trigger on Weather domain updates, read the cached outlook, and emit a `SeasonalOutlookComputed` event with the next 3 months' outlooks. It SHALL return no events when the store has no data.

#### Scenario: 3-month outlook emitted
- **WHEN** weather data arrives and the seasonal store has data for November, December, January
- **THEN** a `SeasonalOutlookComputed` event SHALL be emitted with 3 `MonthlyOutlook` entries

#### Scenario: No data returns no event
- **WHEN** weather data arrives but the seasonal store is empty
- **THEN** no event SHALL be emitted
