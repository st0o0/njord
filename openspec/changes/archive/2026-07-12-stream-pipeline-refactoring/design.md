## Context

The current pipeline (`PollPipeline.cs`) uses a two-level materialization pattern: an outer `RestartSource` wraps a `Source.Tick` that calls `SelectAsync(1, RunCycleAsync)`. Inside `RunCycleAsync`, a second stream is materialized (`Source.From(targets).RunWith(Sink.Seq)`), collects all outcomes, then returns a `CycleResult` with received/failed/unanswered lists. The `PipelineGuardianActor` materializes the outer source and forwards each `CycleResult` to the MQTT egress.

This works but has structural limitations: new trigger sources (MQTT commands, REST) would require bypassing the tick-driven outer source; the inner materialization per cycle prevents a single backpressure-connected graph; and the `CycleResult` aggregation exists to batch outcomes for a consensus step that has since been deferred.

With consensus deferred (per-model data goes 1:1 to HA), each fetch outcome maps directly to one device state publish. There is nothing to aggregate.

## Goals / Non-Goals

**Goals:**
- Single flat, materialized graph from trigger to publish — no nested `RunWith`
- Typed command protocol decoupling triggers from processing
- Weighted throttle using pre-calculated API cost for safe parallelism
- Architecture that trivially accepts additional sources (MQTT cmd, REST) later
- Each pipeline stage independently testable as a `Flow<TIn, TOut>`

**Non-Goals:**
- Adding MQTT command or REST trigger sources (Phase 2/3)
- Consensus computation
- Changing the MQTT discovery mechanism
- Changing the HTTP client interface

## Decisions

### 1. MergeHub as the pipeline entry point

**Choice:** `MergeHub.Source<PipelineCommand>(perProducerBufferSize: 4)` with a `Sink` materialized once at startup. Sources attach dynamically.

**Why over alternatives:**
- vs. `Source.Combine/Merge` at build time: can't add sources after materialization (REST endpoint doesn't exist at graph-build time)
- vs. Actor mailbox as entry: loses backpressure; actor mailbox is unbounded
- MergeHub is exactly "many-to-one with backpressure, dynamic attach"

Phase 1 materializes a single `Source.Tick → PollAll` and attaches it to the hub. Phase 2+ attaches MQTT/REST sources to the same hub.

### 2. Typed command protocol

```
PipelineCommand (abstract)
├── PollAll
├── RefreshLocation(LocationId)
└── RefreshModel(LocationId, ModelId)
```

Each command carries enough info for expansion to concrete `FetchTarget[]`. The `Expand` stage maps commands to targets with pre-calculated weight.

**Why typed over stringly-typed:** pattern matching in Expand, exhaustiveness checks, no parsing errors at runtime.

### 3. Weighted throttle instead of element-count throttle

**Choice:** `Throttle(maxElements: 600, per: 1 minute, costCalculation: t => t.Weight, ThrottleMode.Shaping)`

**Weight formula:** `ceil(hourlyVars / 10) × ceil(forecastDays / 14)` — derived from Open-Meteo documentation on call weighting. Currently all njord requests are weight 1 (9 hourly vars, 4 days), but the formula is correct for future config changes.

**Why over sequential processing:** With known cost, parallelism is safe. A manual `RefreshModel` (weight 1) no longer waits behind a 24-target `PollAll` completing sequentially — both flow through the throttle concurrently.

### 4. No aggregation — direct publish per outcome

**Choice:** Each `FetchOutcome.Success` maps immediately to a `DeviceStatePayload` and publishes. Failed outcomes are logged and discarded (the device keeps its last retained state on the broker).

**Why:** Consensus is deferred; the MQTT spec already defines "one retained state JSON per device per cycle"; a successful fetch for (lucerne, icon_d2) IS that device's state. No need to wait for other models.

**What replaces CycleResult for logging:** A lightweight counter/metric per command execution. The pipeline emits a `CommandSummary` side-effect (via `WireTap` or `DivertTo`) for observability without blocking the main flow.

### 5. Stream supervision replaces RestartSource + Actor lifecycle

**Choice:** `RestartSource.WithBackoff` wraps only the `Source.Tick` (or future sources). The processing stages use a `Decider` supervision strategy (resume on transient failures, restart on persistent ones). The `PipelineGuardianActor` is replaced by a simpler hosting setup that materializes the graph and holds the `KillSwitch`.

**Why:** The actor added indirection (Tell → Receive → forward to egress) without adding value. The stream can publish directly to the `IMqttPublisher` seam. If needed, a lightweight actor can still own the `KillSwitch` for clean shutdown.

### 6. Pipeline graph composition as named stages

```csharp
// TriggerStage: Source<PipelineCommand, IDisposable>
// ExpandStage:  Flow<PipelineCommand, WeightedTarget, NotUsed>
// FetchStage:   Flow<WeightedTarget, FetchOutcome, NotUsed>
// PublishStage: Sink<FetchOutcome, NotUsed>

var (mergeHubSink, source) = MergeHub.Source<PipelineCommand>(4)
    .PreMaterialize(materializer);

source
    .Via(ExpandStage.Create(options))
    .Via(FetchStage.Create(options, client))
    .To(PublishStage.Create(publisher, logger))
    .Run(materializer);
```

Each stage is a static factory returning a `Flow` or `Sink` — independently testable by wiring `TestSource → Stage → TestSink`.

## Risks / Trade-offs

**[GroupBy substream leak]** → If MergeHub is not drained fast enough, producers back-pressure. Mitigation: buffer after MergeHub (`Buffer(32, OverflowStrategy.DropHead)` for command deduplication if needed later).

**[Weighted throttle accuracy]** → Open-Meteo's weight formula is documented but not enforced with HTTP 429 at exactly the boundary. Mitigation: use 80% of the stated limit (480/min instead of 600/min) as the throttle ceiling, consistent with the existing startup budget validation.

**[Lost observability without CycleResult]** → No single "cycle summary" log line. Mitigation: `WireTap` after fetch that increments counters per command; a periodic metric log (or structured log per outcome) replaces the batch summary.

**[MergeHub materialization order]** → The hub sink must be materialized before sources attach. Mitigation: `PreMaterialize` returns the sink immediately; sources connect in their own startup phase.

**[KillSwitch for shutdown]** → Without the actor lifecycle binding the stream to the host, shutdown needs explicit `KillSwitch.Shutdown()`. Mitigation: register the kill switch with `IHostApplicationLifetime.ApplicationStopping`.

## Open Questions

- Should the `Expand` stage apply deduplication (skip a target if the same (location, model) is already in-flight in the throttle/fetch)? Or defer to Phase 2 when multiple trigger sources actually exist?
- Exact buffer size for the MergeHub — 4 per producer is a starting point; may need tuning under load.
