## MODIFIED Requirements

### Requirement: A model forecast carries both hourly and daily series
A `ModelForecast` SHALL carry the weather model, the location, the poll cycle id, an hourly `ForecastSeries`, and a daily `DailyForecastSeries`. Either series MAY be empty (but not both). The record SHALL NOT carry a separate `RetrievedAt` timestamp — the `CycleId` is the authoritative time reference for when the data was collected.

#### Scenario: Forecast with hourly only
- **WHEN** no daily parameters are active
- **THEN** the `ModelForecast` carries an hourly series and an empty daily series

#### Scenario: Forecast with both
- **WHEN** hourly and daily parameters are active and the model returns both
- **THEN** the `ModelForecast` carries both series populated

#### Scenario: No RetrievedAt field exists
- **WHEN** a `ModelForecast` is constructed
- **THEN** it carries `Model`, `Location`, `Cycle`, `Hourly`, and `Daily` — no `RetrievedAt`
