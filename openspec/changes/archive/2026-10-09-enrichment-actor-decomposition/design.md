## Context

`EnrichmentActor` (384 lines) orchestrates the entire enrichment pipeline: resolves
dependencies, classifies features by interface type, builds the Akka.Streams graph (with
broadcast/merge), constructs the consensus inline flow (sensor polling + error handling +
metrics in 54 lines), and records metrics. `ModelStateActor` (219 lines) embeds mutable
capability-learning inside a stream transformation. `HorizonProjection.BuildPerHorizon()`
conflates coordinate calculations, point lookup, and JSON building.

## Goals / Non-Goals

**Goals:**
- Each component has one job: EnrichmentActor owns graph topology + lifecycle;
  consensus-flow logic moves to a named builder; capability tracking moves out of the
  stream transformation; JSON building separates from point lookup.
- Zero behavior change: same enrichments computed, same egress events, same MQTT payloads.

**Non-Goals:**
- Not redesigning the IStatelessEnrichment/IStatefulEnrichment/IActorEnrichment hierarchy.
- Not changing MqttStateActor's dedup or stream-merge.

## Decisions

1. **Extract `ConsensusFlowFactory`** (new static class in `Njord.Enrichment`): takes
   sensor-hub actor ref, metrics, timeout config; returns the `Flow<>` that
   `EnrichmentActor` currently builds inline in `BuildConsensusInlineFlow`. The actor calls
   `ConsensusFlowFactory.Create(sensorHub, timeout)` and plugs the result into its graph.
   Alternative: make it a child actor. Rejected — it's a pure stream-flow factory, not a
   stateful lifecycle concern.

2. **Extract `CapabilityTracker`** (new class in `Njord.Egress`): owns the
   `knownCapabilities` dictionary, `IsSubsetOf`/`UnionWith` logic, and
   `CapabilityLearned` event emission. `ModelStateActor`'s stream transformation calls
   `_tracker.Track(observedParams)` and receives a bool/event back, keeping the stream
   stage itself side-effect-free.

3. **Split `HorizonProjection.BuildPerHorizon()`** into `ToHourlyPayload(point, params)`
   and `ToDailyPayload(point, params)` plus a coordinator that handles horizon filtering.

## Risks / Trade-offs

- [Risk] ConsensusFlowFactory needs the sensor-hub IActorRef, which is resolved at
  runtime. → Pass it as parameter to the factory; the actor resolves it and hands it over.
- [Risk] CapabilityTracker mutation must be thread-safe if the stream runs on multiple
  dispatchers. → Verify ModelStateActor's stream runs on the actor's dispatcher (default
  for ActorMaterializer); if so, no concurrent access.

## Migration Plan

Pure refactor, single deploy, no persistence/wire changes.

## Open Questions

None.
