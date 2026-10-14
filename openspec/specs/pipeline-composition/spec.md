# pipeline-composition Specification

## Purpose

The `ForecastPipeline` analysis stream: it owns a MergeHub input that producers attach to via SinkRef, materialized once per incarnation, combines each forecast with the settings side channel and the pushed sensor snapshot of its location, computes consensus and all enabled analysis features in one stateful stage (including trends against the previous consensus), supervises the stream, and vends typed `PipelineEvent` output via SourceRef.
## Requirements
### Requirement: One stateful stage computes all analysis per forecast
For every forecast the `StatefulSelectMany` stage SHALL update a `ModelSnapshot` held in the stage closure, build a `ConsensusSnapshot` with `ConsensusBuilder.Create(snapshot, location, settings.Consensus)`, and emit, in this order: `PipelineEvent.ForecastReceived`, `PipelineEvent.ConsensusComputed` (only while `ConsensusOptions.Enabled` is true; the snapshot is built either way), then `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` for each analyzer that returns a result. Each analyzer SHALL take the `ConsensusSnapshot` directly plus its own extras and the current settings record: `AlertAnalyzer.Compute(consensus, settings.Alerts)`, `DerivedAnalyzer.Compute(consensus, settings.Derived)`, `TrendAnalyzer.Compute(consensus, previous, settings.Trends)`, `IndexAnalyzer.Compute(consensus, sensors, settings.Indices)`. There SHALL be no `PollContext` record and no per-feature input interfaces. An analyzer whose feature is disabled SHALL return `null`, and no event SHALL be emitted for it. All analysis state SHALL live in the stage closure, not in actor fields.

#### Scenario: Events per forecast
- **WHEN** a forecast arrives and consensus, alerts, derived, trends and indices are enabled and a previous consensus exists
- **THEN** the stage emits `ForecastReceived`, `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed`

#### Scenario: Disabled feature emits nothing
- **WHEN** the alerts feature is disabled
- **THEN** `AlertAnalyzer.Compute` returns `null` and no `AlertComputed` event is emitted

#### Scenario: Analyzer signatures
- **WHEN** an analyzer is invoked by the pipeline
- **THEN** it receives a `ConsensusSnapshot` (plus `previous` for trends and an optional `SensorSnapshot` for indices) and its settings record, never a context object

### Requirement: Trends compare against the previous consensus held in the stage
The previous `ConsensusSnapshot` for trend analysis SHALL be kept in the `StatefulSelectMany` closure and replaced by the current consensus after each forecast. `TrendAnalyzer` itself SHALL be stateless and SHALL return `null` when no previous consensus exists. Because the analysis graph lives as long as the pipeline incarnation, the previous consensus SHALL survive producers detaching and re-attaching.

#### Scenario: First poll cycle
- **WHEN** the first forecast after the pipeline started is processed
- **THEN** the previous consensus is `null`, `TrendAnalyzer.Compute` returns `null` and no `TrendComputed` event is emitted

#### Scenario: Subsequent poll cycles
- **WHEN** a later forecast is processed
- **THEN** `TrendAnalyzer.Compute` receives the consensus computed for the preceding forecast as `previous`

#### Scenario: Previous consensus survives a producer change
- **WHEN** a forecast arrives through one producer, that producer ends, and the next forecast arrives through a new producer
- **THEN** `TrendAnalyzer.Compute` receives the consensus of the first forecast as `previous` and a `TrendComputed` event is emitted

### Requirement: ForecastPipeline vends its output via SourceRef
The analysis graph SHALL end in a `BroadcastHub.Sink<PipelineEvent>` with `bufferSize: 2`, materialized once per pipeline incarnation. On `RequestPipelineOutput(RequestId)` the actor SHALL run the hub source into `StreamRefs.SourceRef<PipelineEvent>()` and reply `RequestPipelineOutputCompleted(RequestId, SourceRef)`, or `RequestPipelineOutputFailed(RequestId, Cause)` when materializing the SourceRef fails. These handshake records SHALL live in `Njord.Core/Actors/StreamWiringMessages.cs`. The hub SHALL NOT use KeepAlive, Buffer or IdleTimeout stages. A consumer's SourceRef SHALL stay alive while producers detach and re-attach.

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
The analysis stage SHALL use `StreamSupervision.LoggingDecider` with the actor's logging adapter as its supervision strategy. An exception the decider stops on ends the analysis graph, which restarts the pipeline (see "ForecastPipeline owns a stable analysis stream").

#### Scenario: Analyzer exception is logged
- **WHEN** an analyzer throws while processing one forecast
- **THEN** the exception is logged through the actor's logger and handled by the shared decider

### Requirement: Settings side channel
`ForecastPipeline` SHALL feed analysis settings into the graph through a `Source.ActorRef` side channel (buffer 10, `DropHead`), started with the startup settings and repeated with `RepeatPrevious`, so every forecast is analyzed with the latest `AnalysisSettings`. On `AnalysisSettingsChanged` the actor SHALL store the settings and forward them to the side channel, so a change applies from the next forecast on and never in the middle of one computation.

#### Scenario: Settings change applies to the next forecast
- **WHEN** `ConfigManager` sends `AnalysisSettingsChanged` with new alert thresholds
- **THEN** the next forecast is analyzed with the new thresholds and a computation already running is not affected

### Requirement: Live sensor snapshot per forecast
`ForecastPipeline` SHALL accept `SensorSnapshots` messages (an immutable map from `LocationId` to `SensorSnapshot`, pushed by the SensorHub) and feed each into a sensor side channel (`Source.ActorRef`, buffer 10, `DropHead`, started with an empty map and repeated with `RepeatPrevious`) that is zipped onto every forecast, so every forecast is analyzed with the latest pushed map and a new map never applies in the middle of one computation. For each forecast the stage SHALL pass the map's entry for the forecast's location (matched case-insensitively) to `IndexAnalyzer`, or `null` when the map has no entry for it. The pipeline SHALL NOT ask the SensorHub per forecast, and a missing or late push SHALL NOT drop, delay or fail the analysis of a forecast.

#### Scenario: Live indoor temperature reaches the indices
- **WHEN** the latest pushed map holds `IndoorTemperature = 24.5` for location "Luzern" and a forecast for "Luzern" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives a snapshot with `IndoorTemperature = 24.5` and the index result reflects it instead of the configured indoor temperature

#### Scenario: Readings are matched to the forecast's location
- **WHEN** the latest pushed map holds readings for "Bern" only and a forecast for "Lucerne" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives `null` for "Lucerne"

#### Scenario: No sensor data yet
- **WHEN** no map has been pushed yet, or the map has no entry for the forecast's location
- **THEN** `IndexAnalyzer.Compute` receives `null` for the sensors and uses configured or default indoor values

#### Scenario: SensorHub unavailable
- **WHEN** no SensorHub is registered and nothing is pushed
- **THEN** the forecast is analyzed with `null` sensors and all its pipeline events are emitted

#### Scenario: Stale readings are not used
- **WHEN** the only reading for the location is older than the configured staleness window when the SensorHub pushes its map
- **THEN** the pushed map has no entry for that location and `IndexAnalyzer.Compute` receives `null` for it

#### Scenario: Location keys match case-insensitively
- **WHEN** the latest pushed map holds a snapshot keyed "LUCERNE" and a forecast for "lucerne" is analyzed
- **THEN** `IndexAnalyzer.Compute` receives that snapshot

### Requirement: ForecastPipeline owns a stable analysis stream
`ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs, marker `IForecastPipeline`) SHALL materialize, once per actor incarnation when it starts and with `Context.Materializer()`, exactly one analysis graph: `MergeHub<Forecast>` (per-producer buffer 16) → `ZipWith(sensor snapshot side channel, settings side channel)` → `StatefulSelectMany(compute all)` → `BroadcastHub<PipelineEvent>`. The graph SHALL contain no fetch, throttle or hashing logic, no intermediate relay actor and no other hub. The pipeline SHALL NOT resolve, watch or message the gateway, and SHALL NOT stash messages: its hubs exist from the start, so `RequestForecastSink` and `RequestPipelineOutput` are answered at any time. Producers and consumers coming and going SHALL NOT rebuild the graph. When the analysis graph completes or fails, the pipeline SHALL log an Error and fail, so its backoff-on-failure supervisor restarts it with fresh hubs; producers and consumers re-attach on their own.

#### Scenario: Pipeline graph structure
- **WHEN** `ForecastPipeline` starts
- **THEN** it materializes `MergeHub<Forecast> → ZipWith(sensors, settings) → StatefulSelectMany → BroadcastHub<PipelineEvent>` before any producer or consumer attaches

#### Scenario: No fetch logic in the pipeline
- **WHEN** `ForecastPipeline` is inspected
- **THEN** it has no dependency on `IOpenMeteoClient`, the gateway throttle, `ForecastDataHash` or `IForecastGateway`

#### Scenario: Output is available before any producer
- **WHEN** a `RequestPipelineOutput` arrives before any producer has attached
- **THEN** it is answered immediately with `RequestPipelineOutputCompleted`

#### Scenario: An ended analysis graph restarts the pipeline
- **WHEN** a stage of the analysis graph stops the graph
- **THEN** the pipeline fails and is restarted by its supervisor with fresh hubs

### Requirement: Producers attach to the pipeline input via SinkRef
On `RequestForecastSink(RequestId)` the pipeline SHALL materialize a `StreamRefs.SinkRef<Forecast>()` source into its MergeHub and reply `RequestForecastSinkCompleted(RequestId, SinkRef)` to the sender, or `RequestForecastSinkFailed(RequestId, Cause)` when the SinkRef cannot be materialized. These handshake records SHALL live in `Njord.Core/Actors/StreamWiringMessages.cs`. Any number of producers SHALL be able to hold a SinkRef at the same time. A producer's stream that completes or fails SHALL end only that producer's input: the MergeHub, the analysis state and every consumer SourceRef SHALL stay alive.

#### Scenario: Producer receives a SinkRef
- **WHEN** a producer sends `RequestForecastSink(5)`
- **THEN** `ForecastPipeline` replies `RequestForecastSinkCompleted(5, sinkRef)` and forecasts sent into the SinkRef are analyzed

#### Scenario: A second producer can attach
- **WHEN** two producers each hold a SinkRef and each sends a forecast
- **THEN** both forecasts are analyzed and their events reach the same consumer

#### Scenario: A producer that ends does not end the hub
- **WHEN** a producer's stream completes or fails and another producer sends a forecast afterwards
- **THEN** that forecast is analyzed and a consumer attached before the first producer ended receives its events on the same SourceRef

