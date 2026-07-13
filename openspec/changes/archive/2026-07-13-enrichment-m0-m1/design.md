## Context

njord polls up to 8 weather models per location via the SchedulerActor. Each model has its own update cycle (ECMWF ~6h, GFS ~1h, ICON ~3h). Raw data flows as `FetchOutcome` through a BroadcastHub to the MqttEgressActor (1:1 to MQTT) and the SchedulerActor (hash feedback). No multi-model aggregation exists yet.

The enrichment pipeline roadmap (M0–M7) adds a third BroadcastHub consumer — the `EnrichmentActor` — which accumulates model data into a running snapshot and distributes to specialized consumer streams. This change implements M0 (infrastructure) and M1 (consensus consumer).

## Goals / Non-Goals

**Goals:**
- EnrichmentActor infrastructure that supports arbitrary consumer streams added in future milestones
- Scan-based ModelSnapshot that provides "best available" data after each model update
- ConsensusComputer as a pure, independently testable static class
- Consensus published as pseudo-model `consensus` — identical topic and discovery structure to raw model devices
- Delta publishing to prevent MQTT spam when consensus doesn't change

**Non-Goals:**
- Later enrichment consumers (alerts M2, derived M3, trends M4, indices M5, energy M6, history M7)
- Akka.Cluster or custom Akka.Streams stages
- Changes to the SchedulerActor, poll logic, or API budget

## Decisions

### D1: Scan-based ModelSnapshot instead of GroupedWithin

**Decision:** `Scan` operator with a `ModelSnapshot` holding the latest `ModelForecast` per (location, model). Each incoming `FetchOutcome.Success` updates the snapshot.

**Alternatives rejected:**
- `GroupedWithin(modelCount, timeout)` — models poll on individual schedules (GFS hourly, ECMWF every 6h). There is no synchronous cycle where all arrive together. GroupedWithin would cut too early or wait on a meaningless timeout.
- "Wait for N/N then compute" — same problem with individual poll timing.

**Why Scan:** Each model update improves the snapshot. Consensus is always "best available." Delta check at the MQTT end prevents redundant publishes.

```
sourceRef.Source
  .Scan(ModelSnapshot.Empty, (snap, outcome) => outcome switch
  {
      FetchOutcome.Success s => snap.Update(s.Forecast),
      _ => snap,
  })
  .Where(snap => snap.HasChanged)
```

### D2: Second BroadcastHub for consumer fan-out

**Decision:** The EnrichmentActor materializes:
1. A Scan consumer on the pipeline BroadcastHub → produces `ModelSnapshot`
2. A `BroadcastHub<ModelSnapshot>` for enrichment consumers

**Why:** Every consumer (consensus now, alerts/trends/indices later) needs the same snapshot. Computing Scan once and distributing via BroadcastHub avoids N redundant Scans and keeps the architecture uniform for M2–M7.

```
BroadcastHub<FetchOutcome>  (pipeline, existing)
       │
       ├──▶ MqttEgressActor     (raw data, unchanged)
       ├──▶ SchedulerActor      (hash feedback, unchanged)
       │
       └──▶ EnrichmentActor
              │
              Scan(ModelSnapshot) → Where(HasChanged)
              │
              BroadcastHub<ModelSnapshot>
              │
              └──▶ Consensus stream → MergeHub → MQTT
```

### D3: SinkRef binding to MqttEgressActor

**Decision:** MqttEgressActor gains a `RequestMqttSink` / `MqttSinkResponse` protocol. The EnrichmentActor uses the returned `SinkRef<MqttMessage>` to push computed messages into the egress MergeHub.

**Why:** One MQTT connection, one transport graph, one availability topic. No duplicate connection logic. The MergeHub already supports multiple producers.

**Alternative rejected:** Separate MQTT connection for enrichment — would duplicate connection management, LWT handling, and reconnect logic.

**Implementation:** MqttEgressActor materializes a new `SinkRef<MqttMessage>` connected to the existing MergeHub on each `RequestMqttSink`:

```csharp
Receive<RequestMqttSink>(_ =>
{
    var sinkRef = StreamRefs.SinkRef<MqttMessage>()
        .To(_mergeHubSink!)
        .Run(_mat!);
    sinkRef.PipeTo(Self, Sender,
        sr => new MqttSinkResponse(sr),
        ex => /* log and ignore */);
});
```

### D4: Pure computation functions

**Decision:** `ConsensusComputer` is a static class with pure functions:

```csharp
ConsensusComputer.ComputeMedian(IReadOnlyList<double?> values) → double?
ConsensusComputer.ComputeTrimmedMean(values, trimPercent) → double?
ConsensusComputer.ComputeSpread(values) → double?
ConsensusComputer.ComputeIqr(values) → double?
ConsensusComputer.ComputeAgreement(values, reference, tolerance) → double?
ConsensusComputer.IdentifyOutlier(models, values, reference) → (WeatherModel, double)?
ConsensusComputer.ComputeConfidenceInterval(values, lowerPct, upperPct) → (double, double)?
ConsensusComputer.BuildAvailabilityMatrix(snapshot, targetTime) → Dictionary<WeatherModel, bool>
```

**Why:** No Akka in the computation. Pure input → output. Unit-testable without TestKit, without materializer, without actor system.

### D5: Delta publishing with lastPublished cache

**Decision:** The consensus consumer stream holds a `Dictionary<string, string>` (`topic → last payload`) as closure state. Only publishes when the serialized JSON differs.

**Why:** Proven existing pattern from MqttEgressActor's consumer graph. Prevents MQTT spam when a model update doesn't shift the consensus.

### D6: Consensus as pseudo-model in the topic scheme

**Decision:** `njord/{location}/consensus/{horizon}` — same structure as model topics. Device id `njord_{location}_consensus`. Discovery payload identical to model devices plus diagnostic attributes (spread, agreement, models_used) as additional JSON keys in the state payload.

**Why:** Same schema → same HA discovery structure → consistent dashboard presentation. HA automations and templates work identically on consensus and model devices.

### D7: EnrichmentOptions configuration

**Decision:** `NjordOptions.Enrichment` with per-consumer sections:

```json
{
  "Njord": {
    "Enrichment": {
      "Consensus": { "Enabled": true, "Method": "Median", "TrimPercent": 0.1 }
    }
  }
}
```

**Why:** Disabled consumers are not materialized (no stream, no subscriber). M2–M7 add their own sections under `Enrichment`. Consensus enabled by default since it's the primary value-add.

## Risks / Trade-offs

**[Consensus from stale data]** → If ECMWF hasn't updated in 6h, its "old" data flows into the consensus. This is correct: the latest forecast is the best available. The state payload includes `models_used` and per-model `last_updated` so freshness is transparent.

**[BroadcastHub backpressure from enrichment]** → A slow consensus computation could backpressure through the BroadcastHub. Mitigation: supervision with Resume on the consumer stream, and the BroadcastHub's internal buffer (256 elements). Consensus computation is pure math on <30 values — microsecond territory.

**[SinkRef lifecycle coupling]** → If MqttEgressActor restarts, the SinkRef becomes invalid. Mitigation: EnrichmentActor watches MqttEgressActor and re-requests on `Terminated`.

**[Discovery payload size]** → Consensus device has the same component count as model devices (~240). This is within HA's tested limits and consistent with existing devices.
