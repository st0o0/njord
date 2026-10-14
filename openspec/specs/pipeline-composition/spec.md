# pipeline-composition Specification

## Purpose

The `ForecastPipeline` analysis stream: it consumes the gateway's `SourceRef<Forecast>`, combines each forecast with the settings side channel and the live sensor snapshot of its location, computes consensus and all enabled analysis features in one stateful stage (including trends against the previous consensus), supervises the stream, and vends typed `PipelineEvent` output via SourceRef.
## Requirements
### Requirement: ForecastPipeline owns the analysis stream
`ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs, marker `IForecastPipeline`) SHALL request a `SourceRef<Forecast>` from the gateway (`IForecastGateway`) with `RequestForecastStream` and, on `RequestForecastStreamCompleted`, SHALL materialize exactly one analysis graph with `Context.Materializer()`: `sourceRef.Source → ZipWith(settings side channel) → SelectAsync(1, sensor lookup) → StatefulSelectMany(compute all) → BroadcastHub<PipelineEvent>`. The graph SHALL contain no fetch, throttle or hashing logic, no intermediate relay actor and no second hub. Until the forecast stream is materialized the actor SHALL stash every message except the gateway handshake, `AnalysisSettingsChanged` and `Terminated`. When the gateway terminates or the handshake fails, the actor SHALL re-resolve the gateway with jittered exponential backoff (1 s up to 30 s) and request a new stream.

#### Scenario: Pipeline graph structure
- **WHEN** `ForecastPipeline` receives `RequestForecastStreamCompleted`
- **THEN** it materializes `sourceRef.Source → ZipWith(settings) → SelectAsync(1, sensor lookup) → StatefulSelectMany → BroadcastHub<PipelineEvent>` and enters its ready behavior

#### Scenario: No fetch logic in the pipeline
- **WHEN** `ForecastPipeline` is inspected
- **THEN** it has no dependency on `IOpenMeteoClient`, the gateway throttle or `ForecastDataHash`

#### Scenario: Messages before the stream exists are stashed
- **WHEN** a `RequestPipelineOutput` arrives before the forecast stream is materialized
- **THEN** it is stashed and answered after the graph is materialized

#### Scenario: Gateway loss triggers re-resolve
- **WHEN** the watched gateway actor terminates
- **THEN** `ForecastPipeline` re-resolves `IForecastGateway` with backoff and requests a new forecast stream

### Requirement: One stateful stage computes all analysis per forecast
For every forecast the `StatefulSelectMany` stage SHALL update a `ModelSnapshot` held in the stage closure, build a `ConsensusSnapshot` with `ConsensusBuilder.Create(snapshot, location, settings.Consensus)`, and emit, in this order: `PipelineEvent.ForecastReceived`, `PipelineEvent.ConsensusComputed`, then `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` for each analyzer that returns a result. Each analyzer SHALL take the `ConsensusSnapshot` directly plus its own extras and the current settings record: `AlertAnalyzer.Compute(consensus, settings.Alerts)`, `DerivedAnalyzer.Compute(consensus, settings.Derived)`, `TrendAnalyzer.Compute(consensus, previous, settings.Trends)`, `IndexAnalyzer.Compute(consensus, sensors, settings.Indices)`. There SHALL be no `PollContext` record and no per-feature input interfaces. An analyzer whose feature is disabled SHALL return `null`, and no event SHALL be emitted for it. All analysis state SHALL live in the stage closure, not in actor fields.

#### Scenario: Events per forecast
- **WHEN** a forecast arrives and alerts, derived, trends and indices are enabled and a previous consensus exists
- **THEN** the stage emits `ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed`

#### Scenario: Disabled feature emits nothing
- **WHEN** the alerts feature is disabled
- **THEN** `AlertAnalyzer.Compute` returns `null` and no `AlertComputed` event is emitted

#### Scenario: Analyzer signatures
- **WHEN** an analyzer is invoked by the pipeline
- **THEN** it receives a `ConsensusSnapshot` (plus `previous` for trends and an optional `SensorSnapshot` for indices) and its settings record, never a context object

### Requirement: Trends compare against the previous consensus held in the stage
The previous `ConsensusSnapshot` for trend analysis SHALL be kept in the `StatefulSelectMany` closure and replaced by the current consensus after each forecast. `TrendAnalyzer` itself SHALL be stateless and SHALL return `null` when no previous consensus exists.

#### Scenario: First poll cycle
- **WHEN** the first forecast after materialization is processed
- **THEN** the previous consensus is `null`, `TrendAnalyzer.Compute` returns `null` and no `TrendComputed` event is emitted

#### Scenario: Subsequent poll cycles
- **WHEN** a later forecast is processed
- **THEN** `TrendAnalyzer.Compute` receives the consensus computed for the preceding forecast as `previous`

### Requirement: ForecastPipeline vends its output via SourceRef
The analysis graph SHALL end in a `BroadcastHub.Sink<PipelineEvent>` with `bufferSize: 2`. On `RequestPipelineOutput(RequestId)` the actor SHALL run the hub source into `StreamRefs.SourceRef<PipelineEvent>()` and reply `RequestPipelineOutputCompleted(RequestId, SourceRef)`, or `RequestPipelineOutputFailed(RequestId, Cause)` when materializing the SourceRef fails. These handshake records SHALL live in `Njord.Core/Actors/StreamWiringMessages.cs`. The hub SHALL NOT use KeepAlive, Buffer or IdleTimeout stages.

#### Scenario: Consumer receives a SourceRef
- **WHEN** a consumer sends `RequestPipelineOutput(7)`
- **THEN** `ForecastPipeline` replies `RequestPipelineOutputCompleted(7, sourceRef)`

#### Scenario: SourceRef failure is reported
- **WHEN** materializing the SourceRef fails
- **THEN** the requester receives `RequestPipelineOutputFailed` carrying the request id and the exception

#### Scenario: Several consumers share the hub
- **WHEN** the MQTT state publisher and `ForecastReadModel` both hold a SourceRef
- **THEN** each receives every `PipelineEvent` emitted after it attached

### Requirement: Analysis stream supervision
The analysis stage SHALL use `StreamSupervision.LoggingDecider` with the actor's logging adapter as its supervision strategy.

#### Scenario: Analyzer exception is logged
- **WHEN** an analyzer throws while processing one forecast
- **THEN** the exception is logged through the actor's logger and handled by the shared decider

### Requirement: Settings side channel
`ForecastPipeline` SHALL feed analysis settings into the graph through a `Source.ActorRef` side channel (buffer 10, `DropHead`), started with the startup settings and repeated with `RepeatPrevious`, so every forecast is analyzed with the latest `AnalysisSettings`. On `AnalysisSettingsChanged` the actor SHALL store the settings and forward them to the side channel, so a change applies from the next forecast on and never in the middle of one computation.

#### Scenario: Settings change applies to the next forecast
- **WHEN** `ConfigManager` sends `AnalysisSettingsChanged` with new alert thresholds
- **THEN** the next forecast is analyzed with the new thresholds and a computation already running is not affected

### Requirement: Live sensor snapshot per forecast
For every forecast, `ForecastPipeline` SHALL ask the SensorHub (`ISensorHub`) with `QuerySensorSnapshot` for the forecast's location before analyzing it, one forecast at a time and in arrival order, and SHALL pass the snapshot of a `SensorSnapshotResult` to `IndexAnalyzer`. It SHALL pass `null` when the reply is `SensorSnapshotNotAvailable`, when the SensorHub is not registered, or when no reply arrives within a bounded timeout. A failed or missing sensor lookup SHALL NOT drop, delay beyond that timeout or fail the analysis of the forecast. The pipeline SHALL NOT accept pushed `SensorSnapshot` messages.

#### Scenario: Live indoor temperature reaches the indices
- **WHEN** the SensorHub holds a fresh `IndoorTemperature` reading of 24.5 for location "Luzern" and a forecast for "Luzern" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives a snapshot with `IndoorTemperature = 24.5` and the index result reflects it instead of the configured indoor temperature

#### Scenario: Readings are matched to the forecast's location
- **WHEN** the SensorHub holds readings for "Luzern" only and a forecast for "Borken" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives `null` for "Borken"

#### Scenario: No sensor data yet
- **WHEN** the SensorHub has no fresh reading for the forecast's location
- **THEN** `IndexAnalyzer.Compute` receives `null` for the sensors and uses configured or default indoor values

#### Scenario: SensorHub unavailable
- **WHEN** the SensorHub is not registered or does not reply within the timeout
- **THEN** the forecast is analyzed with `null` sensors and all its pipeline events are emitted

#### Scenario: Stale readings are not used
- **WHEN** the only reading for the location is older than the configured staleness window
- **THEN** `IndexAnalyzer.Compute` receives `null` for that location

