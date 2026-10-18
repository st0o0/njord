## Purpose

Establishes field ordering, lifecycle-phase naming, and stream materialization conventions for non-persistent stream-oriented actors.
## Requirements
### Requirement: Consistent field ordering in actors

All actor classes SHALL order their fields in a consistent sequence: (1) constants, (2) injected dependencies (readonly), (3) runtime/mutable refs (materializer, queue refs, actor refs), (4) logger (`_log`), (5) state record (if any).

#### Scenario: ForecastPipeline field order
- **WHEN** inspecting `ForecastPipeline`'s fields
- **THEN** injected dependencies (TimeProvider, analyzers) SHALL appear before runtime refs (`_mat`, `_mergeSink`, `_pipelineOutputSource`, `_sensorRef`, `_settingsRef`), followed by `_log`

#### Scenario: OpenMeteoGateway field order
- **WHEN** inspecting `OpenMeteoGateway`'s fields
- **THEN** injected dependencies SHALL appear before runtime stream refs (`_mat`, `_fetchQueue`, `_forecastQueue`), followed by `_log`

### Requirement: Private message records are grouped after the class declaration

Private message records used within an actor SHALL be declared as nested sealed records grouped together at the top of the class body, before the constructor.

#### Scenario: MqttConnection private messages
- **WHEN** inspecting `MqttConnection`'s private message records (`Connected`, `ConnectFailed`, `Disconnected`, `Reconnect`, `Inbound`, `StatusSubscribed`, `StatusSubscribeFailed`, `PublishOnline`, `OnlinePublished`, `OnlinePublishFailed`)
- **THEN** they SHALL be grouped together at the top of the class body

#### Scenario: ForecastPipeline private messages
- **WHEN** inspecting `ForecastPipeline`'s private message records (`AnalysisGraphEnded`)
- **THEN** they SHALL be grouped together at the top of the class body

### Requirement: Become-phase methods are named for the phase

Actor methods that define a Become phase SHALL be named after the actor's lifecycle phase they represent (e.g., `WaitingForGateway`, `Ready`). Each phase method SHALL register all message handlers for that phase in one place. An actor with a single phase registers its handlers in one method or in the constructor; the stream consumers (`ForecastReadModel`, `MqttStatePublisher`) and their `StreamConsumerActor` base have a single phase.

#### Scenario: ForecastPipeline phases
- **WHEN** inspecting `ForecastPipeline`'s message handling
- **THEN** it has a single operational phase whose handlers (`RequestForecastSink`, `RequestPipelineOutput`, `AnalysisSettingsChanged`, `AnalysisSettingsResult`, `LocationSensorReadings`, graph end) are registered in one place, and no waiting phase

#### Scenario: MqttConnection phases
- **WHEN** inspecting `MqttConnection`'s message handling
- **THEN** `Connecting`, `WaitingToReconnect` and `ConnectedState` SHALL each register all handlers of their phase (handlers shared by every phase come from one common method)

### Requirement: Stream materialization in a dedicated method

Actors that materialize Akka.Streams graphs SHALL do so in a dedicated method (e.g., `MaterializeStreams`, `MaterializeAnalysisPipeline`) called from `PreStart` or the phase transition that triggers it. Stream setup logic SHALL NOT be spread across the constructor and multiple handlers.

#### Scenario: OpenMeteoGateway stream setup
- **WHEN** inspecting how `OpenMeteoGateway` sets up its streams
- **THEN** the fetch stream SHALL be materialized in `MaterializeFetchStream`, called from `PreStart`, and the forecast branch in `AttachForecastBranch`, called when a SinkRef arrives

#### Scenario: ForecastPipeline stream setup
- **WHEN** inspecting how `ForecastPipeline` sets up its analysis pipeline
- **THEN** all stream materialization SHALL happen in `MaterializeAnalysisPipeline`, called from `PreStart`

