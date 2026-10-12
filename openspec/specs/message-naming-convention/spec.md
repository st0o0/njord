## ADDED Requirements

### Requirement: Query responses drop the Query prefix

Query response types SHALL drop the `Query` prefix per FunkArr convention.
The response base is `<Noun>Response`, the success variant is `<Noun>Result`,
the failure variant is `<Noun>Failed`.

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

### Requirement: Completed variants match command name

Command response variants SHALL be named `<VerbNoun>Completed` and
`<VerbNoun>Failed`, matching the command record name.

#### Scenario: FetchForecast responses
- **WHEN** `FetchForecast` command completes
- **THEN** the success type SHALL be `FetchForecastCompleted` and the failure type `FetchForecastFailed`

#### Scenario: RequestForecastStream responses
- **WHEN** `RequestForecastStream` command completes
- **THEN** the success type SHALL be `RequestForecastStreamCompleted`

#### Scenario: RequestPipelineOutput responses
- **WHEN** `RequestPipelineOutput` command completes
- **THEN** the success type SHALL be `RequestPipelineOutputCompleted` and failure `RequestPipelineOutputFailed`
