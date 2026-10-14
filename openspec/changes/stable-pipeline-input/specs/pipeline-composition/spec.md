## REMOVED Requirements

### Requirement: ForecastPipeline owns the analysis stream
**Reason**: The pipeline no longer requests a forecast stream from the gateway and no longer rebuilds its graph per received SourceRef; the dependency direction is now gateway → pipeline.
**Migration**: See "ForecastPipeline owns a stable analysis stream" and "Producers attach to the pipeline input via SinkRef".

## ADDED Requirements

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

## MODIFIED Requirements

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
