## Purpose

Defines the conventions for actor messages: naming, response hierarchies with typed success and failure subtypes, handshake failure naming, and the organization of Njord.Messages into domain namespaces with one file per command or query.

## Requirements

### Requirement: Query responses drop the Query prefix
Query response types SHALL drop the `Query` prefix. The response base is `<Noun>Response`, the success variant is `<Noun>Result`, and the non-success variant is `<Noun>Failed(Exception Cause)` or, for lookups that can find no data, `<Noun>NotAvailable`.

#### Scenario: Budget status query
- **WHEN** `QueryBudgetStatus` is sent
- **THEN** the response base SHALL be `BudgetStatusResponse`, success `BudgetStatusResult`, failure `BudgetStatusFailed(Exception Cause)`

#### Scenario: Poll states query
- **WHEN** `QueryPollStates` is sent
- **THEN** the response base SHALL be `PollStatesResponse`, success `PollStatesResult`, failure `PollStatesFailed(Exception Cause)`

#### Scenario: Sensor readings query with NotAvailable
- **WHEN** `QuerySensorReadings` is sent and no data exists yet
- **THEN** the response is `SensorReadingsNotAvailable` under abstract `SensorReadingsResponse`

#### Scenario: Forecast query with NotAvailable
- **WHEN** `QueryForecast` is sent and no forecast is available
- **THEN** the response is `ForecastNotAvailable(...)` under abstract `ForecastResponse`

### Requirement: Command response variants match the command name
Command response variants SHALL be named `<VerbNoun>Completed`, `<VerbNoun>Failed` and, when the command can be refused, `<VerbNoun>Rejected`, matching the command record name. A command whose requester reacts differently per refusal reason SHALL carry a typed reason enum instead of a string.

#### Scenario: Config update command hierarchy
- **WHEN** ConfigManager responds to `UpdateLocations`
- **THEN** the response is `UpdateLocationsCompleted(...)`, `UpdateLocationsRejected(string Reason)` or `UpdateLocationsFailed(Exception Cause)` under abstract `UpdateLocationsResponse`

#### Scenario: FetchForecast command with typed rejection
- **WHEN** OpenMeteoGateway responds to `FetchForecast`
- **THEN** the response is `FetchForecastCompleted(...)`, `FetchForecastFailed(...)` or `FetchForecastRejected(Location, ModelId, FetchRejectionReason Reason)` under abstract `FetchForecastResponse`, where the reason is one of `QueueFull`, `Budget`, `PipelineUnavailable` and `Expired`

#### Scenario: RequestPipelineOutput command hierarchy
- **WHEN** ForecastPipeline responds to `RequestPipelineOutput`
- **THEN** the response is `RequestPipelineOutputCompleted(...)` or `RequestPipelineOutputFailed(...)` under abstract `RequestPipelineOutputResponse`

#### Scenario: TriggerImmediatePoll has proper response hierarchy
- **WHEN** the system defines `TriggerImmediatePoll`
- **THEN** its response base is `TriggerImmediatePollResponse` with a `TriggerImmediatePollFailed(Exception Cause)` variant

### Requirement: Handshake failures follow Request*Failed naming
Failure response records SHALL be named `Request<Noun>Failed` when they respond to a `Request<Noun>` command, matching the established `Request<Noun>Response` / `Request<Noun>Completed` / `Request<Noun>Failed` convention.

#### Scenario: ForecastSink handshake failure
- **WHEN** a `RequestForecastSink` command fails
- **THEN** the failure record is `RequestForecastSinkFailed` (not `ForecastSinkFailed`)

### Requirement: Failed carries exception
Every `*Failed` response record SHALL carry the caught `Exception` as a `Cause` property.

#### Scenario: Actor encounters exception during handling
- **WHEN** an actor encounters an exception while handling a query or command
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`

### Requirement: Njord.Messages organizes messages by domain namespace
The `Njord.Messages` project SHALL hold every public actor command, query, result and event, with one file per command or query (including its response hierarchy), organized into domain namespaces: `Budget/`, `Config/`, `Forecast/`, `Mqtt/`, `Pipeline/`, and `Sensors/`. There SHALL be no generic acknowledgement message: a requester that needs an answer gets a typed reply from the domain's response hierarchy. No production library other than `Njord.Messages` SHALL declare a public message record; actor-private records stay `private` inside the actor.

#### Scenario: No public messages nested in actors
- **WHEN** any actor class in a feature library is inspected
- **THEN** it declares no public nested record

#### Scenario: No acknowledgement message
- **WHEN** inspecting `Njord.Messages`
- **THEN** it declares no `Ack` record and no `Common/` namespace
