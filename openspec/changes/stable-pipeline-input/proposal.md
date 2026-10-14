## Why

Every message drop and dead stream fixed recently sat at a seam between separately materialized graphs, and
the volatile side owned the long-lived hub: `OpenMeteoGateway` built the forecast `BroadcastHub` and
`ForecastPipeline` attached via SourceRef. A gateway restart (BackoffOnFailure) completed that SourceRef, the
pipeline rebuilt its graph and output hub, every consumer's SourceRef completed, and the analysis state in the
stage closure (`ModelSnapshot`, previous consensus for trends) was lost. The gateway also carried a waiting
list, generation tags and in-flight rebuild logic only to keep its fire-and-forget `Tell` protocol from going
silent. This change (change A of the agreed stream concept) moves the long-lived hubs to the stable, stateful
side and turns the fetch protocol into request/response.

## What Changes

- **Gateway is a request/response gateway** (FunkArr `MediathekViewWebManager` pattern): a private stream
  `Source.Queue(DropNew)` → budget throttle → `SelectAsyncUnordered(2, try/catch → typed result)` →
  `Sink.ActorRef(Self)`. Every `FetchForecast` ends with exactly one reply: `FetchForecastCompleted(Location,
  ModelId, Hash)` (control only, no forecast data), `FetchForecastFailed(...)`, or the new
  `FetchForecastRejected(Location, ModelId, Reason)` with reason `QueueFull`, `Budget`, `PipelineUnavailable`
  or `Expired`. An unexpectedly ended private stream answers its open requests and restarts the gateway
  (BackoffOnFailure). **BREAKING (internal)**: the waiting list, generation tags, in-flight rebuild, the
  gateway's forecast `BroadcastHub` and the `RequestForecastStream` handshake are removed.
- **Budget never holds a request past its requester's patience**: the throttle still smooths short bursts,
  but a request that cannot be admitted within its admission deadline is answered `Rejected(Budget)` (or
  `Rejected(Expired)` when the deadline passed while it was queued) instead of waiting.
- **Forecast data flows into the pipeline through a SinkRef**: the gateway requests a `SinkRef<Forecast>` with
  the new `RequestForecastSink(RequestId)` handshake and feeds successful forecasts into a small separate
  branch (`Source.Queue` → `sinkRef.Sink`). It watches the pipeline and re-attaches only this branch with
  backoff when the pipeline terminates or the branch ends; while no SinkRef is attached it answers
  `Rejected(PipelineUnavailable)` without fetching.
- **ForecastPipeline owns both hubs**, materialized once per actor incarnation: `MergeHub<Forecast>` (one
  SinkRef per producer; a producer that completes or fails never ends the hub) → `ZipWith(sensor snapshot map,
  settings)` → `StatefulSelectMany(analysis)` → `BroadcastHub<PipelineEvent>`. The pipeline no longer resolves,
  watches or depends on the gateway; an ended analysis graph restarts the pipeline (now BackoffOnFailure).
- **SensorHub pushes `SensorSnapshots`** (an immutable map `LocationId` → `SensorSnapshot` of every location with
  fresh readings) to the pipeline on every accepted reading, on every expiry tick, and once when the pipeline
  is resolved. The per-forecast `SelectAsync` SensorHub ask and its timeout are removed.
- **PollScheduler (transitional until change C)** asks the gateway with a named reply timeout that covers
  queue + throttle + fetch and pipes the reply to itself; a timeout maps to a transport failure, a rejection to
  backoff (`Budget` with the 5-minute rate-limit delay). It no longer replies `Ack` to fetch replies.
- **Metrics**: `njord_pipeline_stream_rewires_total` is removed (the pipeline no longer rewires);
  `njord_openmeteo_pipeline_reattaches_total` counts re-attaches of the gateway's forecast branch.

## Non-goals

- Consumers (`ForecastReadModel`, `MqttStatePublisher`, `StreamConsumerActor`) and the output hub's buffering
  are unchanged (change B: permanent drain, private MQTT stream).
- No `PollManager`/`PollWorker` sharding and no persistence changes (change C).
- No change to budget accounting (`RecordApiCall`, free-tier soft limits), to what the analyzers compute, to
  the gRPC API or to MQTT payloads.
- `QuerySensorSnapshot` stays as a SensorHub query; it is no longer used by the pipeline.

## API-budget impact

No new requests and no change to how often targets are polled: polling cadence is still owned by the
scheduler's learned cycles. With the default free tier (300k/month, 600/min, 10k/day) and e.g. 3 locations ×
8 models at the 20-minute discovery interval, usage stays at 24 × 3 × 24 = 1 728 requests/day (≈ 52k/month,
17 % of 300k). Rejections cost nothing: a rejected request is never fetched, and `RecordApiCall` is still
sent only for admitted fetches. A `Budget` rejection backs the target off by at least 5 minutes, so an
exhausted budget lowers the request rate instead of queueing requests behind it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `poll-pipeline`: gateway becomes request/response with explicit rejections, attaches to the pipeline input
  via SinkRef, a stopped private stream restarts the gateway; waiting list and forecast hub removed.
- `pipeline-composition`: the pipeline owns a MergeHub input and the output hub once per incarnation, accepts
  producers via SinkRef, no longer depends on the gateway, and reads pushed sensor snapshot maps.
- `dynamic-budget-throttle`: the throttle stage rejects instead of waiting past the admission deadline.
- `stream-composition`: forecasts reach the pipeline input; gateway and pipeline restart on an ended graph;
  handshake-failure handling moves from the pipeline to the gateway.
- `stream-actor-conventions`: field, private-message, phase and materialization examples follow the new
  gateway and pipeline.
- `producer-broadcast-pattern`: hubs are owned by the stable side; the gateway is a producer into the
  pipeline's MergeHub, not a broadcaster.
- `failure-routing`: rejections and reply timeouts route to the scheduler's backoff.
- `poll-scheduler`: Ask with a named timeout instead of Tell; no `Ack` reply.
- `sensor-hub`: the SensorHub pushes snapshot maps to the pipeline.
- `message-conventions`: `FetchForecastRejected` with a typed reason; `SensorSnapshots` in `Sensors/`.
- `message-naming-convention`, `message-naming`: the `RequestForecastStream` examples become
  `RequestForecastSink`.
- `service-configuration`: the pipeline no longer resolves the gateway; the gateway and SensorHub resolve
  the pipeline.
- `domain-setup-containers`: `ForecastPipeline` runs inside a backoff-on-failure supervisor; the fetch
  timeouts are registered.
- `prometheus-metrics`: the pipeline rewire counter is replaced by the gateway re-attach counter.

## Impact

- Code: `src/Njord.OpenMeteo/OpenMeteoGateway.cs`, `GatewayBudgetGate.cs`, `Telemetry.cs`,
  `Configuration/OpenMeteoSetupContainer.cs`; `src/Njord.Pipeline/ForecastPipeline.cs`, `PollScheduler.cs`,
  `Telemetry.cs`, `Configuration/PipelineSetupContainer.cs`; `src/Njord.SensorHub/SensorHub.cs`,
  `SensorHubState.cs`; `src/Njord.Messages/Forecast/` (`FetchForecastRejected`, `FetchRejectionReason`),
  `src/Njord.Messages/Sensors/SensorSnapshots.cs`; `src/Njord.Core/Actors/StreamWiringMessages.cs`
  (`RequestForecastSink*` replaces `RequestForecastStream*`); `src/Njord.Core/Configuration/Polling/FetchTimeouts.cs`.
- Tests: `Njord.OpenMeteo.Tests`, `Njord.Pipeline.Tests`, `Njord.SensorHub.Tests`, shared helpers in
  `Njord.Tests.Shared`.
- Docs: `AGENTS.md` data-flow guardrail.
- Dependency direction: gateway → pipeline (via `IForecastPipeline`), SensorHub → pipeline; `Njord.OpenMeteo`
  and `Njord.Pipeline` still do not reference each other.
