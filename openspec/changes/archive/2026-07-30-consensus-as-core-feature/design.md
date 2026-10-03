## Context

Consensus is njord's core value — synthesizing multi-model forecasts into a consolidated view. It is currently implemented as `ConsensusEnrichment : IStatelessEnrichment`, architecturally equal to activity indices or BBQ scores. All enrichments receive raw `ModelSnapshot` and independently navigate multi-model data. Daily consensus exists in three redundant forms: direct daily parameters (`DailyParameters`), hourly→daily rollup (`DailyConsensusSummary`), and ha-njord's own client-side aggregation.

The downstream HA integration (ha-njord) confirms this redundancy: it ignores `DailyConsensusSummary` and re-aggregates daily values from hourly consensus data itself (weather.py:556-603).

Current pipeline topology in `EnrichmentActor`:

```
FetchOutcome → Scan(ModelSnapshot) → BuildInlineFlow → [all enrichments in parallel] → EgressActor
                                   → [actor enrichments in parallel] → EgressActor
```

All enrichments (consensus, alerts, derived, trends, indices, energy) run as peers in `BuildInlineFlow`. History runs as a separate actor enrichment branch.

## Goals / Non-Goals

**Goals:**

- Elevate consensus from enrichment to pipeline-stage stream transformation
- Clean separation: `HourlyConsensus` (from hourly model data) and `DailyConsensus` (from daily model data) — no hybrid rollup
- Enrichments consume `ConsensusSnapshot` instead of `ModelSnapshot` — single source of truth
- History retains raw `ModelSnapshot` access via broadcast before consensus stage
- Consensus egress originates directly from consensus stage, not enrichment pathway

**Non-Goals:**

- Changing consensus statistics (`ConsensusComputer`) — pure math stays untouched
- Changing MQTT topic structure (`h0..hN`, `d0..dN`) or HA entity naming
- Changes to ha-njord
- Changing poll pipeline, fetch logic, or Open-Meteo client
- Weighted consensus (using History's model accuracy weights) — future work

## Decisions

### 1. ConsensusSnapshot as a pure Select transformation in Akka.Streams

**Decision:** Consensus computation is a `Select` (map) stage in the stream graph, not an actor or enrichment.

**Rationale:** Consensus is a pure function `ModelSnapshot → ConsensusSnapshot`. It has no state, no side effects, no lifecycle. A `Select` is the simplest, most testable, and most composable form. An actor would add unnecessary complexity (mailbox, scheduling, restart semantics) for what is fundamentally a map operation.

**Alternative considered:** Dedicated `ConsensusActor` — rejected because there is no state to manage, no messages to receive, no supervision benefit.

### 2. Broadcast before consensus, not after

**Decision:** The `ModelSnapshot` is broadcast to two branches: (1) History (raw), (2) Consensus→Enrichments.

```
                    ModelSnapshot
                         │
                    Broadcast(2)
                    ╱           ╲
                   ╱             ╲
          History branch    Consensus Select
          (raw snapshot)         │
                           ConsensusSnapshot
                                 │
                            Broadcast(2)
                           ╱          ╲
                    Consensus      Enrichments
                    Egress         (inline flow)
```

**Rationale:** History needs per-model accuracy tracking (MAE, drift, weights) — it fundamentally operates on individual model forecasts, not consensus. All other enrichments operate on the consolidated view.

**Alternative considered:** Passing both `ModelSnapshot` and `ConsensusSnapshot` to all enrichments — rejected because it blurs the boundary and tempts enrichments to bypass consensus.

### 3. Eliminate DailyConsensusSummary entirely

**Decision:** Remove the hourly→daily rollup. Daily consensus comes exclusively from model daily parameters (`temperature_2m_max`, `precipitation_sum`, `weather_code`, etc.).

**Rationale:** The rollup is redundant — Open-Meteo provides daily aggregates directly, and they are more accurate than re-deriving daily values from hourly medians (e.g., the API's `temperature_2m_max` is the true daily max, not `max(hourly medians)` which underestimates extremes). ha-njord already ignores the server-side rollup and computes its own, confirming the feature has no consumer.

**Risk:** If a model has hourly data but no daily data, that model contributes nothing to daily consensus. Mitigated: all configured models (ICON, ECMWF, GFS, UKMO, MeteoSwiss) provide daily parameters.

### 4. Per-location ConsensusSnapshot

**Decision:** `ConsensusSnapshot` includes the location string. The consensus `Select` stage emits one `ConsensusSnapshot` per location by iterating `locations` inside the map. Downstream enrichments receive `(ConsensusSnapshot, location)` pairs — one call per location, no more `IReadOnlyList<string> locations` parameter.

**Rationale:** Simplifies enrichment interfaces. Every enrichment today has the same `foreach (var location in locations)` boilerplate. Moving iteration upstream removes this repetition and makes each enrichment call focused on one location.

### 5. Enrichment interface signature change

**Decision:**

```csharp
// Stateless (alerts, derived, indices, energy):
IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus);

// Stateful (trends):
IEnumerable<EgressEvent> Compute(ConsensusSnapshot consensus, ConsensusSnapshot? previous);

// Actor (history) — unchanged, stays on ModelSnapshot:
Flow<ModelSnapshot, EgressEvent, NotUsed> CreateFlow(ActorMaterializerContext context);
```

Location comes from `ConsensusSnapshot.Location` — no separate parameter needed.

**Alternative considered:** Keeping `ModelSnapshot` available alongside `ConsensusSnapshot` — rejected per Decision 2.

### 6. ConsensusSnapshot type hierarchy

**Decision:**

```csharp
public sealed record ConsensusSnapshot(
    string Location,
    HourlyConsensus Hourly,
    DailyConsensus Daily);

public sealed record HourlyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffHour);

public sealed record DailyConsensus(
    IReadOnlyList<ParameterConsensus> Parameters,
    int CutoffDay);
```

`HorizonConsensus` and `ParameterConsensus` are unchanged — the statistics layer is already clean.

**Rationale:** Wrapping hourly and daily in their own records (vs. two flat lists on one record) makes the intent explicit and provides a place for granularity-specific metadata (cutoff).

### 7. Consensus egress stays in MqttEgressActor but sourced differently

**Decision:** `MqttEgressActor` continues to handle consensus state messages, but receives them as `EgressEvent.ConsensusUpdate` (new event type) from the consensus stage, not from the enrichment pathway.

**Alternative considered:** Dedicated consensus MQTT publisher — rejected because `MqttEgressActor` already handles dedup, formatting, and connection lifecycle; duplicating that is waste.

### 8. FilterByModelCount moves into ConsensusSnapshot.Compute

**Decision:** The minimum-2-models filter is part of `ConsensusSnapshot.Compute`, not a post-processing step in the enrichment. Horizons with fewer than 2 contributing models are excluded from the snapshot.

**Rationale:** Filtering is an invariant of consensus, not a consumer concern. Every downstream consumer (egress, enrichments) should receive only valid consensus data.

## Risks / Trade-offs

- **[Breaking enrichment interfaces]** → All five enrichments + their tests change signatures. Mitigated by Big Bang approach — no compatibility shim needed since this is an internal interface.
- **[Persistence DTOs reference ConsensusResult]** → Check if `ConsensusResult` appears in persisted snapshots. If so, the persistence DTO layer keeps the old name with `[JsonProperty]` wire names unchanged; only the domain type renames. → Mitigated: persistence DTOs are extend-only with pinned `[JsonProperty]` names; the domain rename does not affect wire format.
- **[Daily consensus coverage]** → If a model provides hourly but not daily data, it won't contribute to daily consensus. → Mitigated: all currently configured models provide daily parameters; document this assumption.
- **[gRPC proto changes]** → `ConsensusUpdate` proto message loses `DailyConsensus` (the summary type). → Mitigated: ha-njord already computes its own daily summaries; the proto field can be deprecated without breaking the client.

## Open Questions

None — all decisions are resolved based on the exploration conversation.
