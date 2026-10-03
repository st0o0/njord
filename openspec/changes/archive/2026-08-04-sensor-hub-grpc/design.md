## Context

Njord's enrichment pipeline operates exclusively on forecast data from Open-Meteo. Several enrichments use static configuration values for parameters that are actually measurable — most notably `IndoorTemp` (used by IndexScorer.Ventilation, default 22.0). There is no mechanism to feed live sensor readings into the system.

The enrichment pipeline flows: `Open-Meteo → PipelineActor → EnrichmentActor → EgressActor`. The EnrichmentActor scans `FetchOutcome` into `ModelSnapshot`, computes `ConsensusSnapshot` per location, then fans out to inline enrichments (`IStatelessEnrichment`, `IStatefulEnrichment`) and actor enrichments (`IActorEnrichment`). All enrichment `Compute` methods currently receive only consensus data.

The energy enrichment has been removed (change `remove-energy-enrichment`). The remaining enrichments are: consensus, alerts, derived, trends, indices, history. Of these, only **indices** (Ventilation score) currently uses `IndoorTemp`.

## Goals / Non-Goals

**Goals:**
- Introduce a `SensorHub` actor that stores the latest sensor readings received via gRPC
- Define `SensorKind` as a closed domain enum — njord declares what it understands
- Support multiple sources per (Location, SensorKind) with per-kind aggregation
- Extend `IStatelessEnrichment.Compute` and `IStatefulEnrichment.Compute` signatures to accept an optional `SensorSnapshot`
- Wire the index enrichment to use live `IndoorTemperature` with fallback to config
- Validate readings per SensorKind (plausibility ranges) and expire stale readings
- Expose `SensorService` gRPC API for pushing readings (unary + client-streaming)

**Non-Goals:**
- MQTT subscription for sensor data — gRPC only
- Reactive re-computation when sensor values change — latest-value pull at each poll cycle
- New enrichment features that consume sensor data — separate future changes
- HA integration/addon that pushes sensor data — client-side concern
- SensorHub persistence — readings are ephemeral, lost on restart

## Decisions

### 1. SensorHub as a standalone actor, not a stream stage

The SensorHub is a stateful key-value store receiving writes from gRPC and reads from the enrichment pipeline. An actor fits naturally: it serializes writes, handles the expiry timer, and is queryable via `Ask`.

**Alternative considered:** Making sensor data a second source in the Akka.Streams graph (merge with Open-Meteo). Rejected because sensor updates are high-frequency and should not trigger re-computation — the poll cycle pulls the latest value, keeping the pipeline tick-driven.

### 2. SensorKind as a domain enum, not free-form strings

Each `SensorKind` carries domain semantics: unit, plausibility range, aggregation strategy. A closed enum ensures njord only accepts values it can meaningfully consume. New kinds are added by extending the enum + adding a `SensorKindMetadata` entry.

**Alternative considered:** Free-form string keys (like HA entity IDs). Rejected because it pushes schema responsibility to the client and allows meaningless data to accumulate.

### 3. Signature change: add SensorSnapshot? to Compute

The `IStatelessEnrichment.Compute` and `IStatefulEnrichment.Compute` signatures gain a `SensorSnapshot?` parameter. The parameter is nullable — enrichments that don't use sensor data ignore it. This is a one-time interface change affecting 5 implementations (alerts, derived, trends, indices, history).

**Alternative considered:** Injecting the SensorHub into each enrichment via DI and having them pull directly. Rejected because it couples enrichments to the actor system and makes testing harder. Passing the snapshot as a pure data parameter keeps enrichments testable with plain records.

### 4. EnrichmentActor pulls SensorSnapshot before each consensus cycle

Before computing enrichments for a location, the EnrichmentActor sends `GetSnapshot(location)` to the SensorHub via `Ask`. The response is a `SensorSnapshot` record (or null if the hub has no data). This adds one message per location per poll cycle — negligible overhead.

The pull happens inside `BuildConsensusInlineFlow`, after `ConsensusSnapshot` is computed and before it's passed to enrichments. The snapshot is captured in the closure alongside the consensus.

### 5. Aggregation strategy per SensorKind

When multiple sources report the same (Location, SensorKind), the SensorHub aggregates them per the kind's defined strategy:

| Strategy | Behavior | Used by |
|---|---|---|
| Average | Arithmetic mean of all non-expired source values | IndoorTemperature, IndoorHumidity, BatteryStateOfCharge |
| Sum | Sum of all non-expired source values | SolarPanelPower |
| Latest | Most recent value only (ignores other sources) | HeatPumpFlowTemp, HeatPumpPower |

### 6. Staleness via configurable TTL with per-reading timestamps

Each reading stores its `MeasuredAt` timestamp. The SensorHub runs a periodic timer (every 60s) to evict expired entries. The TTL is configurable via `SensorOptions.StalenessSeconds` (default: 2 × poll interval). Expired readings are excluded from aggregation.

### 7. Fallback chain: Sensor → Config → Hardcoded default

The index enrichment's Ventilation score currently uses `IndexPreferences.IndoorTemp ?? 22.0`. With the SensorHub, the chain becomes:

```
sensorSnapshot?.Get(SensorKind.IndoorTemperature)   // live
  ?? resolvedPreferences.IndoorTemp                   // config cascade
  // (hardcoded 22.0 is the bottom of the config cascade already)
```

This is implemented in `IndexEnrichment.Compute`, not in the scorer itself — the scorer remains a pure function taking a `ResolvedPreferences` record.

### 8. SensorHub registration via Akka.Hosting

The SensorHub actor is registered as a singleton in the actor system via `AkkaConfigurationBuilder.WithActors`. The gRPC `SensorGrpcService` resolves it from the `ActorRegistry`. The EnrichmentActor resolves it alongside PipelineActor and EgressActor.

## Risks / Trade-offs

**Interface change touches all enrichments** — 5 implementations need a new parameter added. Low risk since the parameter is nullable and existing enrichments pass it through without using it.
→ Mitigation: Mechanical change, one line per implementation.

**Ask latency in the enrichment flow** — The `Ask` to SensorHub adds a message hop per location per cycle. With a 60-min poll interval and 1-3 locations, this is negligible.
→ Mitigation: Short timeout (1s). If SensorHub doesn't respond, use null (no sensor data).

**SensorHub is not persistent** — Readings are lost on restart. A client reconnecting after restart will repopulate quickly, but there's a brief window with no sensor data.
→ Mitigation: Acceptable — enrichments fall back to config defaults, which is the current behavior anyway.

**Proto enum evolution** — Adding new `SensorKind` values requires a proto change. Clients with old protos sending new kinds get `SENSOR_KIND_UNSPECIFIED`.
→ Mitigation: Server rejects `UNSPECIFIED` with `INVALID_ARGUMENT`. Clients must update protos to use new kinds.
