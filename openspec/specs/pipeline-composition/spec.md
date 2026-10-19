# pipeline-composition Specification

## Purpose

The `ForecastPipeline` analysis stream: it owns a MergeHub input that producers attach to via SinkRef, materialized once per incarnation, maintains per-location LocationForecast state, evaluates registered ProjectionGroups for domain-specific enrichment, supervises the stream, and vends typed `PipelineEvent` output via SourceRef.
## Requirements
### Requirement: Pipeline input is DomainDataReceived
The pipeline's MergeHub SHALL accept DomainDataReceived(Location, DomainKind, IDomainForecast) messages instead of Forecast messages. Each successful domain fetch from any PollWorker produces one DomainDataReceived.

#### Scenario: Multiple domains arrive independently
- **WHEN** Weather data arrives at t=0 and AirQuality data arrives at t=200ms for the same location
- **THEN** the pipeline SHALL process each as a separate DomainDataReceived, updating the LocationForecast incrementally

### Requirement: One stateful stage evaluates ProjectionGroups per domain update
For every DomainDataReceived the `StatefulSelectMany` stage SHALL update the LocationForecast for that location (shifting previous, updating the domain slot), emit a domain-received event, then iterate registered ProjectionGroups. Only groups whose RequiredDomains include the changed domain SHALL be evaluated. For each affected group, the Projector projects the LocationForecast; if non-null, all enrichments in the group evaluate the projection. Analysis settings SHALL be read from `IOptions<T>` at pipeline construction and SHALL remain fixed for the pipeline incarnation. All state (LocationForecast per location, previous snapshots) SHALL live in the stage closure, not in actor fields.

#### Scenario: Weather update triggers weather alert group
- **WHEN** Weather data arrives for a location
- **THEN** the stage emits WeatherDataReceived and evaluates the WeatherAlertProjectionGroup

#### Scenario: Unaffected groups are skipped
- **WHEN** AirQuality data arrives and a group requires only Weather
- **THEN** that group SHALL NOT be evaluated

#### Scenario: Previous state preserved across updates
- **WHEN** Weather data arrives twice for the same location
- **THEN** the second evaluation has access to the first Weather data as Previous

### Requirement: No settings or sensor side-channels
The pipeline SHALL have no ZipWith side-channels. Analysis settings are constructor-injected from IOptions<T>. No sensor readings input exists.

#### Scenario: Direct flow from MergeHub to StatefulSelectMany
- **WHEN** the pipeline is materialized
- **THEN** the graph SHALL be MergeHub → StatefulSelectMany → BroadcastHub with no ZipWith stages

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

### Requirement: ForecastPipeline owns a stable analysis stream
`ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs, marker `IForecastPipeline`) SHALL materialize, once per actor incarnation when it starts and with `Context.Materializer()`, exactly one analysis graph: `MergeHub<DomainDataReceived>` (per-producer buffer 16) → `StatefulSelectMany(ProjectionEngine)` → `BroadcastHub<PipelineEvent>`. Analysis settings SHALL be read from `IOptions<T>` at pipeline construction and remain fixed for the pipeline incarnation. The graph SHALL contain no fetch, throttle or hashing logic, no intermediate relay actor and no other hub. The pipeline SHALL NOT resolve, watch or message the gateway, and SHALL NOT stash messages: its hubs exist from the start, so `RequestForecastSink` and `RequestPipelineOutput` are answered at any time. Producers and consumers coming and going SHALL NOT rebuild the graph. When the analysis graph completes or fails, the pipeline SHALL log an Error and fail, so its backoff-on-failure supervisor restarts it with fresh hubs; producers and consumers re-attach on their own.

#### Scenario: Pipeline graph structure
- **WHEN** `ForecastPipeline` starts
- **THEN** it materializes `MergeHub<DomainDataReceived> → StatefulSelectMany → BroadcastHub<PipelineEvent>` before any producer or consumer attaches

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

