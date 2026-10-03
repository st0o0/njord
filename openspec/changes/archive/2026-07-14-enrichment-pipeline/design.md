## Context

njord currently polls up to 8 weather models per location via the SchedulerActor, which learns individual poll intervals per model (ECMWF every ~6h, GFS ~1h, ICON ~3h). Raw data flows as `FetchOutcome` through a BroadcastHub to the MqttEgressActor (1:1 to MQTT) and the SchedulerActor (hash feedback). All multi-model intelligence — consensus, uncertainty, warnings — is missing.

The enrichment pipeline adds a third BroadcastHub consumer: the `EnrichmentActor`. It maintains a running `ModelSnapshot` (latest state of every model) and distributes via its own BroadcastHub to specialized consumer streams.

## Goals / Non-Goals

**Goals:**
- Make all 50 identified enrichment possibilities implementable across 8 milestones (M0–M7)
- Each milestone is a standalone OpenSpec change
- Pure Akka.Streams architecture using only built-in operators
- Pure, independently testable computation functions
- One consumer per area, independently toggleable

**Non-Goals:**
- Akka.Cluster, custom stages, separate MQTT connections per consumer
- UI/dashboard (separate change)
- Changes to the SchedulerActor or poll logic

## Decisions

### D1: Scan-based ModelSnapshot instead of GroupedWithin

**Decision:** `Scan` operator with a `ModelSnapshot` holding the latest `ModelForecast` per (location, model). Each incoming `FetchOutcome.Success` updates the snapshot and triggers recomputation.

**Alternatives rejected:**
- `GroupedWithin(modelCount, timeout)` — the SchedulerActor polls models individually on their own update cycle (GFS every hour, ECMWF every 6h). There is no synchronous "cycle" where all models arrive together. GroupedWithin would either cut too early or wait on a timeout that never makes sense.
- "Wait for 8/8 then compute" — same problem: with individual poll timing per model there is no natural "all are in" gate.

**Why Scan:** Each model update improves the snapshot. Consensus is always "best available" — all latest known data from all models. Delta check at the MQTT end prevents redundant publishes when the computed value doesn't change.

```
sourceRef.Source                         // FetchOutcome (Success + Failure)
  .Scan(ModelSnapshot.Empty, (snap, outcome) => outcome switch
  {
      Success s => snap.Update(s.Forecast),
      Failure _ => snap,                  // Failures don't change the snapshot
  })
  .Where(snap => snap.HasChanged)         // Only on actual data changes
```

### D2: Two-stage fan-out with second BroadcastHub

**Decision:** The EnrichmentActor materializes:
1. A Scan consumer on the pipeline BroadcastHub → produces `ModelSnapshot`
2. A second `BroadcastHub<ModelSnapshot>` as fan-out for enrichment consumers

**Why:** Every consumer (consensus, alerts, trends, ...) needs the same snapshot. Computing the Scan accumulation once and distributing via BroadcastHub is more efficient and cleaner than N redundant Scans.

```
BroadcastHub<FetchOutcome>  (pipeline, existing)
       │
       ├──▶ MqttEgressActor     (raw, unchanged)
       ├──▶ SchedulerActor      (hash feedback, unchanged)
       │
       └──▶ EnrichmentActor
              │
              Scan(ModelSnapshot) → Where(Changed)
              │
              BroadcastHub<ModelSnapshot>  (NEW, second stage)
              │
              ├──▶ Consensus stream   → MergeHub → MQTT
              ├──▶ Alert stream       → MergeHub → MQTT
              ├──▶ Derived stream     → MergeHub → MQTT
              ├──▶ Trends stream      → MergeHub → MQTT
              ├──▶ Index stream       → MergeHub → MQTT
              └──▶ Energy stream      → MergeHub → MQTT
```

### D3: Dedicated EnrichmentActor, not inside MqttEgressActor

**Decision:** Separate actor rather than extending MqttEgressActor.

**Why:**
- MqttEgressActor stays unchanged (single responsibility: raw model data → MQTT)
- Independent restart on enrichment failures
- Clear lifecycle separation: enrichment can start/stop without affecting the MQTT connection
- EnrichmentActor sends computed `MqttMessage`s via SinkRef into the existing MergeHub of MqttEgressActor

**Alternative rejected:** Everything in MqttEgressActor — would bloat the actor and blur lifecycle boundaries.

### D4: SinkRef binding to MqttEgressActor

**Decision:** MqttEgressActor provides a `SinkRef<MqttMessage>` on request that feeds into its existing MergeHub. The EnrichmentActor uses this SinkRef to publish computed messages.

**Why:** One MQTT connection, one transport graph, one availability topic. No duplicate connection logic. The MergeHub is already designed to accept multiple producers.

**New protocol:**
```
RequestMqttSink → MqttSinkResponse(SinkRef<MqttMessage>)
```

### D5: Pure computation functions per area

**Decision:** Each enrichment area is a static class with pure functions:

```
ConsensusComputer.Compute(ModelSnapshot, parameters, horizons) → ConsensusResult
AlertEvaluator.Evaluate(ModelSnapshot, thresholds) → AlertResult
DerivedValues.Compute(ModelSnapshot, parameters) → DerivedResult
TrendAnalyzer.Analyze(ModelSnapshot, previousSnapshot) → TrendResult
IndexScorer.Score(ModelSnapshot, parameters, weights) → IndexResult
EnergyForecaster.Forecast(ModelSnapshot, buildingConfig) → EnergyResult
```

**Why:** No Akka in the computation. Pure input → output. Unit-testable without TestKit, without materializer, without actor system. The stream integration is just the wrapper.

### D6: Delta publishing with lastPublished cache

**Decision:** Each consumer stream holds a `Dictionary<string, string>` (`topic → last payload`) as closure and only publishes when the serialized payload has changed.

**Why:** Proven existing pattern from MqttEgressActor. Prevents MQTT spam when a model update doesn't shift the consensus.

### D7: Topic scheme for enrichments

**Decision:** Consensus is treated as a pseudo-model. Other enrichments get their own topic segments:

```
njord/{location}/consensus/{horizon}     Consensus — identically structured to model topics
njord/{location}/alerts/{alert_type}     Threshold warnings
njord/{location}/derived/{horizon}       Derived values per horizon
njord/{location}/trends                  Trend analysis (single JSON)
njord/{location}/index/{index_type}      Daily-life indices
njord/{location}/energy/{metric}         Energy values
njord/{location}/meta/snapshot           Model availability + diagnostics
```

**Why consensus as pseudo-model:** Same schema as model devices → same HA discovery structure → consistent dashboard presentation. One device `njord_{location}_consensus` with the same horizons and parameters.

### D8: Configuration

**Decision:** Enrichment consumers are controlled via `NjordOptions.Enrichment`:

```json
{
  "Njord": {
    "Enrichment": {
      "Consensus": { "Enabled": true, "Method": "Median" },
      "Alerts": { "Enabled": true, "FrostThreshold": 0, "HeatThresholds": [30, 35, 40] },
      "Derived": { "Enabled": true },
      "Trends": { "Enabled": false },
      "Indices": { "Enabled": false },
      "Energy": { "Enabled": false, "PanelKwp": 0, "HeatingBaseTemp": 18 }
    }
  }
}
```

Disabled consumers are not materialized at all (no stream, no BroadcastHub subscriber).

## Risks / Trade-offs

**[ModelSnapshot grows unbounded]** → Snapshot holds only the latest forecast per (location, model). At 3 locations × 8 models = 24 entries. Negligible.

**[Enrichment errors affect MQTT transport]** → The EnrichmentActor uses the same MergeHub/transport as raw egress. A blocking enrichment consumer could theoretically exert backpressure on the transport. Mitigation: supervision strategy with Resume on every consumer stream, and buffer between consumer output and MergeHub sink.

**[Consensus from stale data]** → If ECMWF hasn't updated in 6h, "old" data flows into the consensus. This is correct: the latest ECMWF forecast is the best available information. A `last_updated` attribute per model makes freshness transparent. Optional: `max_age` config that excludes models with too-old data from the consensus.

**[Many MQTT topics]** → 50 enrichments × horizons × locations could yield hundreds of topics. Mitigation: consumers are individually toggleable. Default: only consensus active, rest opt-in.

**[M7 persistence dependency]** → Historical features need SQLite or Akka.Persistence. njord already has a `PersistencePath` in config and the SchedulerActor uses Akka.Persistence. M7 can use the same infrastructure.

## Milestone Dependencies

```
M0 (Infrastructure)
 │
 ├──▶ M1 (Consensus core)
 │     │
 │     ├──▶ M2 (Alerts)        ─ parallelizable ─┐
 │     ├──▶ M3 (Derived)       ─ parallelizable ─┤
 │     └──▶ M4 (Trends)        ─ parallelizable ─┤
 │                                                 │
 │                                                 ▼
 │                                          M5 (Indices)
 │                                                 │
 │                                                 ▼
 │                                          M6 (Energy)
 │                                                 │
 │                                                 ▼
 └──────────────────────────────────────▶  M7 (Historical)
```

M2, M3, M4 can be developed in parallel after M1. M5 uses results from M1–M4. M6 builds on M5. M7 is standalone but needs M1 for weighted consensus.
