## MODIFIED Requirements

### Requirement: Query responses drop the Query prefix
Query response types SHALL drop the `Query` prefix per FunkArr convention.
The response base is `<Noun>Response`, the success variant is `<Noun>Result`,
and the non-success variant is `<Noun>Failed` or, for lookups that can find no data, `<Noun>NotAvailable`.

#### Scenario: Budget usage query
- **WHEN** `QueryBudgetUsage` is sent
- **THEN** the response base SHALL be `BudgetUsageResponse`, success `BudgetUsageResult`, failure `BudgetUsageFailed`

#### Scenario: Poll states query
- **WHEN** `QueryPollStates` is sent
- **THEN** the response base SHALL be `PollStatesResponse`, success `PollStatesResult`, failure `PollStatesFailed`

#### Scenario: Sensor snapshot query
- **WHEN** `QuerySensorSnapshot` is sent
- **THEN** the response base SHALL be `SensorSnapshotResponse`, success `SensorSnapshotResult`, unavailable `SensorSnapshotNotAvailable`

#### Scenario: Forecast query
- **WHEN** `QueryForecast` is sent
- **THEN** the response base SHALL be `ForecastResponse`, success `ForecastResult`, unavailable `ForecastNotAvailable`
