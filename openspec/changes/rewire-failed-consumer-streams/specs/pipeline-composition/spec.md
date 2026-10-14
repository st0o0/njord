## MODIFIED Requirements

### Requirement: ForecastPipeline owns the analysis stream
`ForecastPipeline` (src/Njord.Pipeline/ForecastPipeline.cs, marker `IForecastPipeline`) SHALL request a `SourceRef<Forecast>` from the gateway (`IForecastGateway`) with `RequestForecastStream` and, on the `RequestForecastStreamCompleted` that answers its current request id, SHALL materialize exactly one analysis graph with `Context.Materializer()`: `sourceRef.Source → ZipWith(settings side channel) → SelectAsync(1, sensor lookup) → StatefulSelectMany(compute all) → BroadcastHub<PipelineEvent>`. Handshake replies carrying an older request id SHALL be ignored. The graph SHALL contain no fetch, throttle or hashing logic, no intermediate relay actor and no second hub. Until the forecast stream is materialized the actor SHALL stash every message except the gateway handshake, `AnalysisSettingsChanged` and `Terminated`. When the gateway terminates or the handshake fails, the actor SHALL re-resolve the gateway with jittered exponential backoff (1 s up to 30 s) and request a new stream. When the analysis graph completes or fails while the gateway is still alive (the forecast SourceRef ends or fails, or a stage stops), the actor SHALL log a Warning, increment `njord_pipeline_stream_rewires_total`, return to waiting for the gateway and request a new forecast stream after a jittered exponential backoff delay that grows with consecutive graph losses (1 s up to 30 s) and resets once a graph has stayed up for a stability window. The completion of a graph replaced after a gateway loss SHALL be ignored. Consumers that held a SourceRef to the old output hub see it end and rewire on their own.

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

#### Scenario: A failed forecast stream is requested again
- **WHEN** the forecast SourceRef from a gateway that is still alive fails
- **THEN** `ForecastPipeline` logs a Warning, requests a new forecast stream after the backoff delay, and a consumer that requests the pipeline output afterwards receives the events of forecasts offered to the new stream

#### Scenario: A completed forecast stream is requested again
- **WHEN** the forecast SourceRef from a gateway that is still alive completes
- **THEN** `ForecastPipeline` requests a new forecast stream after the backoff delay

#### Scenario: Stale handshake reply is ignored
- **WHEN** a `RequestForecastStreamCompleted` arrives whose request id is not the pipeline's current one
- **THEN** no analysis graph is materialized from it
