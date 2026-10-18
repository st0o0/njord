## Purpose

Defines the FunkArr naming convention for actor responses: query responses drop the Query prefix, and command response variants match the command name.
## Requirements
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

#### Scenario: Sensor readings query
- **WHEN** `QuerySensorReadings` is sent
- **THEN** the response base SHALL be `SensorReadingsResponse`, success `SensorReadingsResult`, unavailable `SensorReadingsNotAvailable`

#### Scenario: Forecast query
- **WHEN** `QueryForecast` is sent
- **THEN** the response base SHALL be `ForecastResponse`, success `ForecastResult`, unavailable `ForecastNotAvailable`

### Requirement: Command response variants match the command name
Command response variants SHALL be named `<VerbNoun>Completed`, `<VerbNoun>Failed` and, when the command can be refused, `<VerbNoun>Rejected`, matching the command record name.

#### Scenario: FetchForecast responses
- **WHEN** `FetchForecast` command is answered
- **THEN** the success type SHALL be `FetchForecastCompleted`, the failure type `FetchForecastFailed` and the refusal type `FetchForecastRejected`

#### Scenario: RequestForecastSink responses
- **WHEN** `RequestForecastSink` command completes
- **THEN** the success type SHALL be `RequestForecastSinkCompleted` and failure `RequestForecastSinkFailed`

#### Scenario: RequestPipelineOutput responses
- **WHEN** `RequestPipelineOutput` command completes
- **THEN** the success type SHALL be `RequestPipelineOutputCompleted` and failure `RequestPipelineOutputFailed`

