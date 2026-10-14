## Why

`StreamConsumerActor` (the base of `MqttStatePublisher` and `ForecastReadModel`) only rebuilds its
graph when a watched dependency actor terminates. When the graph itself fails or completes - a stage
stopped by the logging decider, the remote side of the pipeline SourceRef failing, the pipeline hub
completing - the materialization's completion is discarded and the actor stays in `Ready` with a dead
graph: MQTT stops publishing state and the read model stops updating until njord restarts, without a
log line or metric. `ForecastPipeline` has the same blind spot towards the gateway's forecast stream,
and since it rebuilds its analysis graph (and with it the output hub) when the gateway restarts,
every pipeline consumer currently goes silently dead after a gateway restart, too.

## What Changes

- `StreamConsumerActor.MaterializeGraph` returns the completion `Task` of the materialized graph
  (**BREAKING** for subclasses; both production subclasses and the test subclasses are updated).
- The base watches that task (`PipeTo` with a generation-tagged success and failure record). When the
  current graph completes or fails while the actor is `Ready`, it logs a Warning, increments
  `njord_stream_consumer_rewires_total{consumer}`, shuts down the kill switch, lets the subclass drop
  its SourceRefs through a new `OnStreamLost()` hook and re-requests them after a capped, jittered
  exponential backoff that grows across consecutive losses and resets after the graph stayed up for a
  stability window.
- Completions of replaced graphs (dependency loss, earlier rewires) and of graphs stopped by the
  actor's own stop are ignored.
- `ForecastReadModel` keeps its latest-value state across a rewire, and its secondary hub for gRPC
  streaming clients now outlives the pipeline subscription, so attached streaming clients keep their
  SourceRefs across a rewire.
- `MqttStatePublisher` starts each materialization with an empty dedup cache, so the first cycle after
  a rewire republishes every state topic.
- `ForecastPipeline` watches its analysis graph the same way: when the forecast stream from the gateway
  or the analysis graph ends, it logs a Warning, increments `njord_pipeline_stream_rewires_total` and
  requests a new forecast stream with the same kind of backoff. It also correlates the forecast stream
  handshake by request id.
- New `Njord.Core` meter (registered in `CoreSetupContainer.NjordMeters`).

## Non-goals

- No change to the gateway's forecast hub: it is materialized once per gateway incarnation and fed by
  an actor-owned queue; the fetch-stream rebuild from `harden-gateway-requests` already covers the
  gateway's own failure mode.
- No change to `StreamSupervision.LoggingDecider` classification: a non-transient exception still
  stops the stage; the rewire restores the flow afterwards.
- No restart of the consumer actor itself, no new supervisor strategy, no health-check change.
- No replay of events missed while a graph was down: the pipeline output is a live stream; the next
  poll cycle refills MQTT state and the read model.
- The analysis state of `ForecastPipeline` (`ModelSnapshot`, previous consensus) still resets when
  its graph is rebuilt, as it does today after a gateway restart.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `stream-consumer-actor`: the `MaterializeGraph` contract returns the completion task; the base
  rewires a failed or completed graph with backoff, ignores stale and self-inflicted completions, and
  exposes `OnStreamLost()`.
- `stream-composition`: stream lifecycle includes rewiring on graph termination; an unexpected error
  stops the stage and the owning actor rebuilds the graph (instead of "triggering actor supervision").
- `pipeline-composition`: `ForecastPipeline` requests a new forecast stream when its analysis graph
  ends, correlates the handshake by request id.
- `consumer-model`: `ForecastReadModel` keeps state and streaming clients across a rewire;
  `MqttStatePublisher` republishes after a rewire.
- `prometheus-metrics`: new rewire counters.
- `per-domain-telemetry`: `Njord.Core` meter registered in the host.

## Impact

- Code: `src/Njord.Core/Actors/StreamConsumerActor.cs`, new `src/Njord.Core/Telemetry.cs`,
  `src/Njord.Mqtt/MqttStatePublisher.cs`, `src/Njord.Pipeline/ForecastReadModel.cs`,
  `src/Njord.Pipeline/ForecastPipeline.cs`, `src/Njord.Pipeline/Telemetry.cs`,
  `src/Njord/Configuration/CoreSetupContainer.cs`.
- Tests: Core, Mqtt and Pipeline test projects (new rewire specs, updated test subclasses).
- Metrics: two new counters. MQTT wire format, gRPC contracts and golden masters unchanged.
- API budget: none - polling is untouched (0 additional Open-Meteo requests/month).
