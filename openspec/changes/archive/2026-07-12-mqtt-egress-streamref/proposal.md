## Why

The MQTT egress layer has a split-brain problem: `MqttConnectionActor` owns the broker connection and publishes discovery/availability, while `PublishStage` in the pipeline calls `IMqttPublisher.PublishAsync` directly for state payloads. Two independent callers share one connection with no coordination. The actor interface (`IMqttPublisher`) mixes connection-management callbacks with fire-and-forget publishing. Additionally, the pipeline is hosted via `IHostedService` with manual `KillSwitch` management rather than actor-materialized lifecycle. This change unifies all MQTT publishing through a single stream graph (MergeHub), gives each concern its own actor with actor-bound materialization, and connects them via StreamRef for clean failure-domain separation.

## What Changes

- **Introduce `MqttEgressActor`** (replaces current `MqttConnectionActor`): owns the broker connection lifecycle, materializes the egress graph (`MergeHub<MqttMessage>` → Publish Sink), feeds Discovery/Availability/Tombstone messages into the hub via `Source.Queue`, and exposes a `SinkRef<MqttMessage>` for external producers.
- **Introduce `PipelineActor`** (replaces `PipelineHostedService`): materializes the pipeline graph with `Context.Materializer()`, obtains the `SinkRef<MqttMessage>` from the egress actor, maps `FetchOutcome` to `MqttMessage`, and sinks into the StreamRef. Actor-bound materialization provides lifecycle without `KillSwitch`.
- **Introduce `MqttMessage` record**: unified type for all broker publishes (topic, payload, retain). All sources (state, discovery, availability, tombstone) produce `MqttMessage` — the sink is content-agnostic.
- **Split `IMqttPublisher` interface**: `IMqttConnection` for connection-management (connect, subscribe, disconnect callbacks) used only by the egress actor. The publish sink calls `MqttClient.PublishAsync` directly — no interface needed for the data path.
- **Remove `PipelineHostedService`**: replaced by `PipelineActor` with actor-managed lifecycle.
- **Remove `PublishStage`**: its responsibility (map outcome → publish) moves into the pipeline actor's graph as a simple `Select` + StreamRef sink.

## Non-goals

- Changing the MQTT topic scheme, discovery payload format, or state payload format.
- Adding MQTT command sources (Phase 2 — the egress MergeHub enables this later).
- Changing the pipeline's fetch logic (expand, throttle, fetch stages stay as-is).
- Clustering or remote StreamRefs — this is local-only StreamRef for typed in-process bridging.
- API budget changes (no change to polling volume or frequency).

## Capabilities

### New Capabilities
- `egress-stream-graph`: The egress MergeHub graph — accepts `MqttMessage` from multiple sources (pipeline via StreamRef, internal discovery/availability/tombstone via Source.Queue), publishes to broker via a single sink with supervision. Defines the `MqttMessage` protocol and `SinkRef` exposure.
- `pipeline-actor`: Actor-owned pipeline materialization replacing `IHostedService`. Obtains egress `SinkRef`, materializes the full pipeline graph (commands → expand → throttle → fetch → map → SinkRef.Sink). Actor-bound lifecycle eliminates KillSwitch management.

### Modified Capabilities
- `mqtt-egress`: The connection actor's responsibilities narrow — it still owns connection lifecycle, LWT, discovery, HA birth, and tombstoning, but publishing now flows through the egress stream graph rather than direct `PublishAsync` calls. The telemetry publishing requirement changes from "actor receives telemetry messages" to "state payloads arrive via StreamRef into the egress hub".
- `stream-composition`: The pipeline graph's terminal stage changes from a `PublishStage` sink (which called `IMqttPublisher` directly) to a `Select(ToMqttMessage)` feeding into a `SinkRef<MqttMessage>`. The pipeline is now materialized by an actor rather than an `IHostedService`, and lifecycle is actor-bound rather than KillSwitch-based.

## Impact

- **`src/Njord/Egress/`**: `MqttConnectionActor.cs` refactored into new `MqttEgressActor`; `IMqttPublisher.cs` split; `EgressMessages.cs` extended with `MqttMessage`.
- **`src/Njord/Pipeline/`**: `PipelineHostedService.cs` replaced by `PipelineActor.cs`; `PublishStage.cs` removed; `PollPipeline.cs` adapted to produce `MqttMessage` output.
- **`src/Njord/Program.cs`**: Actor registration replaces `AddHostedService<PipelineHostedService>`.
- **`src/Njord.Tests/`**: Egress and pipeline tests updated for new actor structure.
- **Dependencies**: No new NuGet packages (StreamRefs are in Akka.Streams core).
