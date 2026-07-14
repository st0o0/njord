## Why

The project's folder structure and namespaces have grown organically and no longer reflect the actual architecture. `Egress/` mixes MQTT transport, HA discovery, payload building, and topic schemes into one namespace. `Enrichment/` conflates pure domain computation (consensus, trends, alerts) with actor lifecycle code. `MqttEgressActor` carries three distinct responsibilities (connection management, discovery publishing, state egress) in 350 lines. The `*Result` records in `Enrichment/` contain `ToMqttMessages()` methods, breaking the architectural guardrail that Ingest and Egress never reference each other — domain types should not know about MQTT serialization. This restructuring establishes clean boundaries, splits the monolithic MQTT actor into focused actors, and introduces a publisher-agnostic egress layer that makes it possible to add non-MQTT publishers in the future.

## What Changes

- **Namespace restructure**: `Domain/` gains `Weather/` and `Analysis/` subfolders. Pure computation (ConsensusComputer, TrendAnalyzer, AlertEvaluator, DerivedComputer, EnergyForecaster, IndexScorer, HistoryAnalyzer, ForecastHistory) moves from `Enrichment/` to `Domain/Analysis/`. New `Mqtt/` namespace for all MQTT-specific code. `Egress/` becomes publisher-agnostic (no MQTT types). `Enrichment/` retains only actors.
- **`MqttEgressActor` split** into four actors:
  - `MqttConnectionActor` — connection lifecycle, MergeHub owner, LWT, reconnect
  - `MqttPublisherActor` — transforms domain results to MqttMessages, publishes via MergeHub sink
  - `DiscoveryActor` — HA birth subscription, discovery config publishing
  - `EgressActor` — publisher-agnostic hub that receives enriched data and routes to registered publisher actors
- **Publisher registration protocol**: publishers register with `EgressActor` via `RegisterPublisher` message. EgressActor broadcasts to all registered publishers. No concrete publisher types in `Egress/` namespace.
- **Remove `ToMqttMessages()` from `*Result` records**: `ConsensusResult`, `AlertResult`, `DerivedResult`, `TrendResult`, `IndexResult`, `EnergyResult`, `HistoryResult` become pure data records. MQTT serialization moves to `StatePayloadBuilder` / `MqttPublisherActor` in `Mqtt/`.
- **Test namespace restructure**: test folders mirror the new source structure.

## Non-goals

- Changing any external behavior (MQTT topics, payloads, discovery format, polling).
- Adding new publishers (InfluxDB, webhook) — this change only establishes the extensibility seam.
- Modifying the Akka.Streams pipeline graph topology in `PipelineActor`.
- Altering the enrichment computation logic itself.
- No API-budget impact — this is a pure structural refactoring with no polling changes.

## Capabilities

### New Capabilities

- `publisher-protocol`: Publisher registration protocol for the egress layer — EgressActor accepts RegisterPublisher/UnregisterPublisher messages and broadcasts enrichment results to all registered publishers.
- `mqtt-actor-topology`: Actor topology for MQTT concerns — MqttConnectionActor (connection + MergeHub), MqttPublisherActor (state serialization + publishing), DiscoveryActor (HA discovery configs + birth handling).

### Modified Capabilities

- `mqtt-egress`: MqttEgressActor is replaced by the new actor topology; connection management, discovery, and state publishing become separate actors. The MergeHub moves to MqttConnectionActor.
- `egress-stream-graph`: The egress stream graph is restructured — MergeHub ownership moves to MqttConnectionActor, consumer graph splits across MqttPublisherActor (state) and DiscoveryActor (configs).
- `enrichment-actor`: EnrichmentActor no longer produces MqttMessages directly; it produces domain result records and sends them to EgressActor. The `ToMqttMessages()` calls are removed from the enrichment streams.
- `consensus-computation`: ConsensusResult loses `ToMqttMessages()` — becomes a pure data record. MQTT serialization responsibility moves to `Mqtt/` namespace.

## Impact

- **Every `.cs` file** gets a namespace change (files physically move between folders).
- **All test files** mirror the structural changes.
- **`MqttEgressActor`** is deleted and replaced by 4 new actors.
- **7 `*Result` records** lose their `ToMqttMessages()` methods; serialization logic moves to `Mqtt/StatePayloadBuilder`.
- **`EnrichmentActor`** stream consumers change from producing `MqttMessage` to producing domain results routed through `EgressActor`.
- **Actor registration** in `NjordActorSystemSetup` changes to register the new actor types.
- **DI registrations** in service setup change to match new namespaces.
- **No package changes**, no external behavior changes.
