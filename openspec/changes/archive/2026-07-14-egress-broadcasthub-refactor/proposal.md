## Why

The egress layer has no clean protocol boundary. `EnrichmentActor` builds `MqttMessage` objects directly (violating its own spec), `MqttPublisherActor` has two unrelated input paths (Pipeline BroadcastHub for per-model data + EgressActor Tell for enrichment results), and the `EgressActor` is a trivial Tell-forwarder with no streaming capability. Adding a second output protocol (SignalR for a live UI) would require duplicating enrichment and per-model formatting code. A protocol-neutral egress boundary is needed now, before that work begins.

## What Changes

- **Define `EgressEvent`** — a discriminated union in `Njord.Egress` covering all output data categories (per-model state, consensus, alerts, derived, trends, indices, energy, history). This is the single contract between data producers and output protocols.
- **Rebuild `EgressActor`** with a MergeHub → BroadcastHub graph (mirroring `PipelineActor`'s ingest pattern). Producers attach via `ISinkRef<EgressEvent>`, consumers via `ISourceRef<EgressEvent>`.
- **Decouple `EnrichmentActor` from `Njord.Mqtt`** — its 7 sub-graphs produce `EgressEvent` variants instead of `MqttMessage`, and feed into the EgressActor's MergeHub instead of directly into `MqttConnectionActor`.
- **Rename `MqttPublisherActor` → `ModelStateActor`** — moves to `Njord.Egress`, produces `EgressEvent.PerModelUpdate`, feeds into EgressActor's MergeHub. No longer references MQTT types.
- **Create `MqttEgressActor`** — a new MQTT-specific consumer that subscribes to EgressActor's BroadcastHub and maps `EgressEvent → TopicScheme + StatePayloadBuilder → MqttMessage → MqttConnectionActor`. All MQTT formatting logic concentrates here.
- **Relocate `StatePayloadBuilder`** mapping into `MqttEgressActor` — it is a protocol-specific concern, not a domain concern.
- **Remove the current `EgressActor` Tell-forwarding protocol** (`RegisterPublisher`, `PublishStateResult`) — replaced by the streams-based MergeHub/BroadcastHub.

## Non-goals

- Implementing SignalR or any second output protocol — this change prepares the boundary, a future change adds the consumer.
- Changing the MQTT topic scheme, discovery payloads, or wire format — HA output must remain identical.
- Modifying the ingest pipeline or scheduler — only the egress side of the data flow changes.
- Changing enrichment computation logic — only the output type of the sub-graphs changes (domain result → `EgressEvent` wrapper).

## Capabilities

### New Capabilities

- `egress-event`: Protocol-neutral egress event discriminated union and the EgressActor's MergeHub/BroadcastHub streaming graph.

### Modified Capabilities

- `publisher-protocol`: Replace Tell-based fan-out with streams-based MergeHub/BroadcastHub. `RegisterPublisher`/`PublishStateResult` removed; producers and consumers attach via StreamRefs.
- `enrichment-actor`: Sub-graphs produce `EgressEvent` instead of `MqttMessage`; no longer requests `MqttSinkRef` from `MqttConnectionActor`; sends to EgressActor's MergeHub instead.
- `mqtt-actor-topology`: `MqttPublisherActor` removed from `Njord.Mqtt`; replaced by `ModelStateActor` in `Njord.Egress` (produces `EgressEvent`) and `MqttEgressActor` in `Njord.Mqtt` (consumes `EgressEvent`, maps to `MqttMessage`).
- `egress-stream-graph`: MqttConnectionActor's MergeHub now receives from `MqttEgressActor` and `DiscoveryActor` only (no longer from the old `MqttPublisherActor` directly).

## Impact

- **Code**: `Njord.Egress/` — new `EgressEvent`, rebuilt `EgressActor`, new `ModelStateActor` (moved from `MqttPublisherActor`). `Njord.Mqtt/` — new `MqttEgressActor`, delete `MqttPublisherActor`. `Njord.Enrichment/` — remove `Njord.Mqtt` dependency.
- **Actor registration**: `NjordActorSystemSetup` — replace `MqttPublisherActor` registration with `ModelStateActor` + `MqttEgressActor`.
- **Tests**: All tests referencing `MqttPublisherActor`, `PublishStateResult`, `RegisterPublisher` need updating. Enrichment tests should verify `EgressEvent` output instead of `MqttMessage`.
- **No API budget impact**: No polling changes — only the output path is restructured.
- **No wire-format change**: MQTT topics, payloads, and discovery configs remain identical.
