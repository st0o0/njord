## Why

The pipeline currently leaks a raw `ISourceQueueWithComplete` handle across actor
boundaries (PipelineActor → SchedulerActor) — shared mutable state that bypasses
Akka's message-passing guarantees. The egress publish path uses a side-effecting
`.Select()` with fire-and-forget `OfferAsync` into a second queue, breaking
backpressure and mixing I/O into a transformation stage. The startup requires two
sequential handshakes (Egress→Pipeline, Pipeline→Scheduler) with stashing, creating
a fragile ordering chain. This refactoring replaces the ad-hoc wiring with
first-class Akka.Streams primitives (StreamRefs, MergeHub, BroadcastHub) to
establish clean, backpressure-aware boundaries between actors.

## What Changes

- **PipelineActor input**: Replace `Source.Queue` with a `MergeHub.Source<WeightedTarget>` and vend a `SinkRef<WeightedTarget>` to the SchedulerActor (instead of a raw queue handle).
- **SchedulerActor queue**: The SchedulerActor materializes its own local `Source.Queue<WeightedTarget>` connected to the PipelineActor's `SinkRef.Sink` — it no longer holds a foreign queue handle.
- **Pipeline output**: Replace the side-effecting `.Select()` + fire-and-forget egress publish with a `BroadcastHub.Sink` at the end of the pipeline flow.
- **Egress consumer**: The MqttEgressActor pulls from the BroadcastHub via a `SourceRef` (or direct hub subscription), builds `MqttMessage` payloads (including delta logic), and feeds them into its existing MergeHub for transport.
- **Feedback consumer**: The PipelineActor materializes a second BroadcastHub consumer locally: `Select(ComputeHash) → Ask<Ack>(scheduler) → Sink.Ignore`. The Ask stays in the PipelineActor's graph, preserving backpressure between hash feedback and the pipeline.
- **Delta-publish state**: The `ConcurrentDictionary<(string,string,string), string> lastPublished` cache moves from the PipelineActor's closure to the EgressActor's consumer graph, where it belongs (egress owns what was last published).
- **Handshakes**: The two current handshakes (`RequestEgressSink`/`EgressSinkResponse` + `RequestPipelineQueue`/`PipelineQueueResponse`) are replaced by a single pattern: PipelineActor vends `SinkRef` to Scheduler and `SourceRef` (or hub source) to Egress.
- **Messages**: `RequestPipelineQueue`/`PipelineQueueResponse` are removed; replaced by `RequestPipelineSink`/`PipelineSinkResponse` (SinkRef) and `RequestPipelineSource`/`PipelineSourceResponse` (SourceRef from BroadcastHub).

## Non-goals

- Changing the poll-scheduling logic, adaptive timing, or persistence model.
- Changing the MQTT topic scheme, payload format, or discovery/availability flows.
- Adding new consumers to the BroadcastHub (metrics, logging) — the hub enables this but we don't add any beyond egress + feedback.
- Changing the Open-Meteo client, fetch parallelism, or throttle configuration.
- Modifying the EgressActor's internal MergeHub for discovery/availability/tombstone queues — that stays as-is.

## Capabilities

### New Capabilities

_(none — this is a structural refactoring of existing capabilities)_

### Modified Capabilities

- `stream-composition`: The pipeline graph structure changes from Source.Queue → linear flow → Sink.Ignore to MergeHub.Source → linear flow → BroadcastHub.Sink with two materialized consumers.
- `pipeline-actor`: The actor no longer exposes a raw queue handle; it vends SinkRef (input) and SourceRef (output) via StreamRefs. The egress SinkRef request is removed; replaced by outbound SourceRef vending.
- `poll-pipeline`: The publish path changes from side-effecting Select to a BroadcastHub consumer. The hash/Ask feedback is a separate BroadcastHub consumer materialized locally.
- `poll-scheduler`: The actor no longer receives a foreign queue handle; it obtains a SinkRef and connects its own local Source.Queue to it.
- `egress-stream-graph`: The egress actor's pipeline integration changes from receiving a SinkRef (push model) to pulling a SourceRef from the BroadcastHub (pull model). Delta-publishing state moves here.
- `delta-publishing`: The cache ownership moves from PipelineActor closure to the EgressActor's consumer graph.

## Impact

- **Code**: `PipelineActor.cs`, `SchedulerActor.cs`, `MqttEgressActor.cs`, and their message types (`SchedulerMessages.cs`, `EgressMessages.cs`, `PipelineCommand.cs`).
- **Tests**: All tests that wire up the pipeline handshake or mock the queue handle need updating. Tests for the egress actor need to account for SourceRef subscription instead of SinkRef vending to the pipeline.
- **Dependencies**: No new NuGet packages — `MergeHub`, `BroadcastHub`, and `StreamRefs` are all in `Akka.Streams` which is already referenced.
- **API budget**: No change — the throttle, fetch parallelism, and polling logic are untouched.
