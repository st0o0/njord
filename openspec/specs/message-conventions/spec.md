## Purpose

Defines the conventions for actor messages: response hierarchies with typed success and Failed subtypes, and the organization of Njord.Messages into domain namespaces with one file per command or query.
## Requirements
### Requirement: Response hierarchies with Completed and Failed
Actor query and command responses SHALL use abstract base records with typed
subtypes. Query response types SHALL drop the `Query` prefix: base is `<Noun>Response`, success is
`<Noun>Result`, and the non-success case is `<Noun>Failed(Exception Cause)` when the handler can fail, or `<Noun>NotAvailable` when the answer is that no data exists yet (read-model and sensor lookups). Command response types match the
command name: `<VerbNoun>Response`, `<VerbNoun>Completed`, `<VerbNoun>Failed`.
A command that can be refused for a validation reason SHALL also have a
`<VerbNoun>Rejected(string Reason)` subtype; a command whose requester reacts differently per refusal reason SHALL carry a typed reason enum instead of a string.

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
- **THEN** the response is `FetchForecastCompleted(...)`, `FetchForecastFailed(...)` or `FetchForecastRejected(Location, ModelId, FetchRejectionReason Reason)` under abstract `FetchForecastResponse`, where the reason is one of `QueueFull`, `Budget`, `PipelineUnavailable` and `Expired`

#### Scenario: RequestPipelineOutput command hierarchy
- **WHEN** ForecastPipeline responds to `RequestPipelineOutput`
- **THEN** the response is `RequestPipelineOutputCompleted(...)` or `RequestPipelineOutputFailed(...)` under abstract `RequestPipelineOutputResponse`

#### Scenario: TriggerImmediatePoll has Failed variant
- **WHEN** PollScheduler fails to handle `TriggerImmediatePoll`
- **THEN** a `TriggerImmediatePollFailed(Exception Cause)` response is available in the hierarchy

#### Scenario: Failed carries exception
- **WHEN** an actor encounters an exception while handling a query or command
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`

### Requirement: Njord.Messages organizes messages by domain namespace
The `Njord.Messages` project SHALL hold every public actor command, query, result and event, with one file per command or query (including its response hierarchy), organized into domain namespaces: `Budget/` (usage recording and status), `Config/` (config snapshot contracts, config, catalog and analysis-settings queries, update commands), `Forecast/` (fetch, fetch replies and rejection reasons, forecast queries), `Mqtt/` (connection events, inbound messages, subscribe), `Pipeline/` (`PipelineEvent`, scheduling, poll state), and `Sensors/` (readings, snapshot query, pushed snapshot maps and the request to push one). There SHALL be no generic acknowledgement message: a requester that needs an answer gets a typed reply from the domain's response hierarchy. No production library other than `Njord.Messages` SHALL declare a public message record; actor-private records stay `private` inside the actor.

#### Scenario: Mqtt messages in separate files
- **WHEN** inspecting `Njord.Messages/Mqtt/`
- **THEN** `MqttConnected`, `SubscribeInbound`, and `MqttInboundMessage` are each in their own `.cs` file

#### Scenario: SensorHub messages in separate files
- **WHEN** inspecting `Njord.Messages/Sensors/`
- **THEN** `UpdateReading`, `QuerySensorSnapshot` (with its response hierarchy), `SensorSnapshots` and `RequestSensorSnapshots` are each in their own file

#### Scenario: No public messages nested in actors
- **WHEN** any actor class in a feature library is inspected
- **THEN** it declares no public nested record

#### Scenario: Analysis settings query
- **WHEN** inspecting `Njord.Messages/Config/`
- **THEN** `QueryAnalysisSettings` and its response hierarchy (`AnalysisSettingsResponse`, `AnalysisSettingsResult`) are in their own file

#### Scenario: No acknowledgement message
- **WHEN** inspecting `Njord.Messages`
- **THEN** it declares no `Ack` record and no `Common/` namespace

