## MODIFIED Requirements

### Requirement: Response hierarchies with Completed and Failed
Actor query and command responses SHALL use abstract base records with typed
subtypes. Each response hierarchy SHALL have a `Failed` subtype. Query response
types SHALL drop the `Query` prefix: base is `<Noun>Response`, success is
`<Noun>Result`, failure is `<Noun>Failed`. Command response types match the
command name: `<VerbNoun>Response`, `<VerbNoun>Completed`, `<VerbNoun>Failed`.

#### Scenario: Budget response hierarchy
- **WHEN** BudgetTracker responds to `QueryBudgetUsage`
- **THEN** the response is `BudgetUsageResult(long Monthly, long Daily)` or `BudgetUsageFailed(Exception Cause)` under abstract `BudgetUsageResponse`

#### Scenario: Poll states response hierarchy
- **WHEN** PollScheduler responds to `QueryPollStates`
- **THEN** the response is `PollStatesResult(...)` or `PollStatesFailed(Exception Cause)` under abstract `PollStatesResponse`

#### Scenario: Sensor snapshot response hierarchy
- **WHEN** SensorHub responds to `QuerySensorSnapshot`
- **THEN** the response is `SensorSnapshotResult(...)` or `SensorSnapshotNotAvailable` under abstract `SensorSnapshotResponse`

#### Scenario: Forecast query response hierarchy
- **WHEN** GrpcConsumer responds to `QueryForecast`
- **THEN** the response is `ForecastResult(...)` or `ForecastNotAvailable(...)` under abstract `ForecastResponse`

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

### Requirement: Njord.Messages organizes messages by domain namespace
The `Njord.Messages` project SHALL organize messages into domain-specific
namespaces with one file per command or query: `Pipeline/` (scheduling, budget,
poll state), `Forecast/` (fetch, stream requests), `Mqtt/` (connection events,
inbound messages, subscribe — each in its own file), `Sensors/` (readings in
own file, snapshot query in own file), `Snapshots/` (forecast queries), and
`Common/` (shared types like Ack).

#### Scenario: Mqtt messages in separate files
- **WHEN** inspecting `Njord.Messages/Mqtt/`
- **THEN** `MqttConnected`, `SubscribeInbound`, and `MqttInboundMessage` are each in their own `.cs` file

#### Scenario: SensorHub messages in separate files
- **WHEN** inspecting `Njord.Messages/Sensors/`
- **THEN** `UpdateReading` and `QuerySensorSnapshot` (with its response hierarchy) are each in their own file

## REMOVED Requirements

### Requirement: Messages targeting ShardRegion entities implement routing marker interfaces
**Reason**: ShardRegion entities were removed during the pipeline refactor.
**Migration**: No routing marker interfaces needed — actors are resolved via ActorKeys.
