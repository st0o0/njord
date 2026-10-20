# seasonal-outlook Specification

## Purpose
TBD - created by archiving change seasonal-outlook. Update Purpose after archive.
## Requirements
### Requirement: Seasonal is a fetched domain
The system SHALL provide `DomainKind.Seasonal` with monthly `SeasonalPoint`s (`TimeResolution.Monthly`) and an `ISeasonalClient` in `Njord.OpenMeteo` that fetches `seasonal-api.open-meteo.com/v1/seasonal` using `OpenMeteoRequestBuilder`. The gateway SHALL dispatch `FetchDomain` for `Seasonal` to the client with the variables of the resolved requirements and answer with exactly one `FetchDomainCompleted`, `FetchDomainFailed` or `FetchDomainRejected`.

#### Scenario: Request URL
- **WHEN** `FetchAsync` is called with coordinates and the variables `temperature_2m_anomaly` and `precipitation_anomaly`
- **THEN** the URL SHALL contain the coordinates (invariant culture), `monthly=temperature_2m_anomaly,precipitation_anomaly`, `timeformat=unixtime` and `timezone=UTC`

#### Scenario: Months are limited
- **WHEN** the API returns six months and `Njord:Seasonal:ForecastMonths` is 3
- **THEN** the forecast SHALL contain the current month and the next 3 months

#### Scenario: Gateway delivers monthly points
- **WHEN** the gateway receives `FetchDomain` for `Seasonal`
- **THEN** it SHALL call only the seasonal client and deliver a `DomainForecast<SeasonalPoint>` with `TimeResolution.Monthly`

### Requirement: Seasonal is polled only when needed
The Seasonal domain SHALL be required only when the `SeasonalOutlook` feature is enabled, and the PollWorker SHALL learn its update cycle like every other domain. `BudgetCalculator` SHALL count it as 30 calls per month and location instead of once per poll cycle.

#### Scenario: Budget projection
- **WHEN** two locations require only the Seasonal domain
- **THEN** the projected monthly calls SHALL be 60 and no domain call per poll cycle SHALL be counted

### Requirement: Confidence from lead time
The monthly confidence SHALL be derived from the lead time in months between the current month and the outlook month. Up to 1 month SHALL be High, 2 months Medium, otherwise Low. Wire text SHALL come from an explicit mapping covering every member.

#### Scenario: Lead times
- **WHEN** the lead time is 1, 2 or 3 months
- **THEN** Confidence SHALL be High, Medium or Low respectively

### Requirement: SeasonalOutlookHandler emits the outlook
`SeasonalOutlookHandler` SHALL be an `IDomainHandler` with a `Feature` descriptor of kind `SeasonalOutlook` (Insight, default off) requiring only the Seasonal domain. On each seasonal update it SHALL emit one `SeasonalOutlookComputed` with a `MonthlyOutlook` per month after the current month, temperature anomaly rounded to one decimal and precipitation anomaly in percent of the normal month (`mean - anomaly`).

#### Scenario: Anomalies converted
- **WHEN** the November point has a temperature anomaly of 1.74, a precipitation anomaly of 2.4 mm and a mean of 99.5 mm
- **THEN** the November entry SHALL have TempAnomaly 1.7 and PrecipAnomaly 2.5

#### Scenario: Current and past months dropped
- **WHEN** the forecast contains the current month and later months
- **THEN** only the later months SHALL be emitted

#### Scenario: No data
- **WHEN** the location has no seasonal forecast
- **THEN** no event SHALL be emitted

#### Scenario: Toggle default
- **WHEN** no configuration is supplied
- **THEN** the feature SHALL be off and `FeatureResolution.EnabledHandlers` SHALL exclude the handler

### Requirement: Outlook is delivered as a lifestyle update
`SeasonalOutlookComputed` SHALL flatten through `ToLifestyleUpdate()` into a `LifestyleUpdate` of kind `SeasonalOutlook` (wire name `seasonal_outlook`) with keys `month_<n>`, `month_<n>_temp_anomaly`, `month_<n>_precip_anomaly` and `month_<n>_confidence`, delivered over gRPC and MQTT through the existing lifestyle path.

#### Scenario: Wire shape
- **WHEN** an outlook with 2 months is mapped
- **THEN** the update SHALL contain 8 entries, with `month_1` equal to the first month ("2026-11")

