## MODIFIED Requirements

### Requirement: Response hierarchies with Completed and Failed
Actor query and command responses SHALL use abstract base records with typed
subtypes. Query response types SHALL drop the `Query` prefix: base is `<Noun>Response`, success is
`<Noun>Result`, and the non-success case is `<Noun>Failed(Exception Cause)` when the handler can fail, or `<Noun>NotAvailable` when the answer is that no data exists yet (read-model and sensor lookups). Command response types match the
command name: `<VerbNoun>Response`, `<VerbNoun>Completed`, `<VerbNoun>Failed`.
A command that can be refused for a validation reason SHALL also have a
`<VerbNoun>Rejected(string Reason)` subtype.

#### Scenario: Budget response hierarchy
- **WHEN** BudgetTracker responds to `QueryBudgetStatus`
- **THEN** the response is `BudgetStatusResult(...)` (usage and effective budget) or `BudgetStatusFailed(Exception Cause)` under abstract `BudgetStatusResponse`

#### Scenario: Poll states response hierarchy
- **WHEN** PollScheduler responds to `QueryPollStates`
- **THEN** the response is `PollStatesResult(...)` or `PollStatesFailed(Exception Cause)` under abstract `PollStatesResponse`

#### Scenario: Sensor snapshot response hierarchy
- **WHEN** SensorHub responds to `QuerySensorSnapshot`
- **THEN** the response is `SensorSnapshotResult(...)` or `SensorSnapshotNotAvailable` under abstract `SensorSnapshotResponse`

#### Scenario: Forecast query response hierarchy
- **WHEN** ForecastReadModel responds to `QueryForecast`
- **THEN** the response is `ForecastResult(...)` or `ForecastNotAvailable(...)` under abstract `ForecastResponse`

#### Scenario: Config update command hierarchy
- **WHEN** ConfigManager responds to `UpdateLocations`
- **THEN** the response is `UpdateLocationsCompleted(...)`, `UpdateLocationsRejected(string Reason)` or `UpdateLocationsFailed(Exception Cause)` under abstract `UpdateLocationsResponse`

#### Scenario: FetchForecast command hierarchy
- **WHEN** OpenMeteoGateway responds to `FetchForecast`
- **THEN** the response is `FetchForecastCompleted(...)` or `FetchForecastFailed(...)` under abstract `FetchForecastResponse`

#### Scenario: RequestPipelineOutput command hierarchy
- **WHEN** ForecastPipeline responds to `RequestPipelineOutput`
- **THEN** the response is `RequestPipelineOutputCompleted(...)` or `RequestPipelineOutputFailed(...)` under abstract `RequestPipelineOutputResponse`

#### Scenario: TriggerImmediatePoll has Failed variant
- **WHEN** PollScheduler fails to handle `TriggerImmediatePoll`
- **THEN** a `TriggerImmediatePollFailed(Exception Cause)` response is available in the hierarchy

#### Scenario: Failed carries exception
- **WHEN** an actor encounters an exception while handling a query or command
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`
