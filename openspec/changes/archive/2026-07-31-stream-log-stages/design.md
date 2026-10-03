## Context

njord's data pipeline is a set of Akka.Streams graphs materialized by actors. After the unified-logging change, all actors use `Context.GetLogger()` (Akka `ILoggingAdapter`) for operational logs at Info level. There is currently no element-level stream tracing.

Akka.Streams provides a built-in `.Log(name, extract, log)` stage that:
- Passes elements through unchanged (zero-overhead passthrough)
- Logs each element at Debug level via `ILoggingAdapter`
- Logs completion at Debug, failure at Error
- Is effectively free when Debug is disabled (no string formatting)

## Goals / Non-Goals

**Goals:**
- Verbose Debug-level tracing at every semantic boundary in all 10 data-processing graphs
- Consistent naming convention for stage names
- Compact extractors that show location/model/type without dumping full payloads
- Easy activation per subsystem via existing Serilog `MinimumLevel.Override`

**Non-Goals:**
- Replacing existing `_log.Info(...)` calls — those are operational, these are diagnostic
- Infrastructure graphs (StreamRef vending)
- Custom log levels per `.Log()` stage — Debug everywhere is the right default

## Decisions

### Decision 1: Stage name convention `{subsystem}-{stage}`

**Choice:** Kebab-case names like `pipeline-fetch-in`, `mqtt-send`, `enrichment-consensus`. No actor class name prefix.

**Why:** The Akka.Streams `.Log()` stage uses the `name` parameter as the log source, which maps to SourceContext in Serilog. Short names keep log lines readable. The subsystem prefix (`pipeline-`, `egress-`, `enrichment-`, `mqtt-`, `grpc-`) groups stages logically and allows selective enabling via `MinimumLevel.Override`.

### Decision 2: One `.Log()` per semantic boundary, not per operator

**Choice:** Place `.Log()` stages at phase transitions — input, transform output, delivery — not between every operator in a chain.

**Why:** Logging between every `.Where()` and `.Select()` creates noise without insight. The interesting moments are: what entered the graph, what came out of the key transform, and what got delivered. Typically 1–3 `.Log()` stages per graph.

### Decision 3: Extractors return compact strings

**Choice:** Each `.Log()` uses an extractor function that formats a one-line summary: `"{Location}/{Model}"`, `"OK {Location}/{Model}"`, `"{Topic} [{Length}B]"`.

**Why:** `.Log()` without an extractor calls `ToString()` on the element, which for records dumps all properties. Compact extractors keep Debug output scannable. Structured Serilog properties are not available through `.Log()` — this is a tracing tool, not a structured logging tool.

### Decision 4: Pass `_log` explicitly to each `.Log()` stage

**Choice:** Always pass the actor's `ILoggingAdapter` as the third parameter: `.Log("name", extractor, _log)`.

**Why:** Without an explicit logger, `.Log()` creates its own from the materializer's actor system, which loses the actor's SourceContext hierarchy. Passing `_log` ensures the log event carries the actor's type as SourceContext, with the stage name as an additional context marker.

## Placement plan

### PipelineActor (2 stages)
- `pipeline-fetch-in` — after `BudgetThrottleStage`, before `SelectAsyncUnordered` (shows what's being fetched)
- `pipeline-fetch-out` — after `SelectAsyncUnordered`, before `Buffer` (shows fetch results)

### PipelineActor hash loop (1 stage)
- `pipeline-hash` — after `.Select(HashResult)`, before `.Ask` (shows hash computations)

### SchedulerActor failure consumer (1 stage)
- `pipeline-failure` — after `.Select(FetchFailed)`, before `Sink.ActorRef` (shows failures routed to scheduler)

### EgressActor hub (1 stage)
- `egress-hub` — between `mergeHubSource` and `.To(broadcastHubSink)` (shows all events entering the egress hub)

### ModelStateActor (2 stages)
- `egress-in` — after `.Via(killSwitch)`, shows raw `FetchOutcome` entering
- `egress-out` — after `.SelectMany`, shows `EgressEvent` leaving (CapabilityLearned + PerModelUpdate)

### EnrichmentActor (2 stages)
- `enrichment-snapshot` — output of `BuildScanSource` (shows `ModelSnapshot` after scan+filter)
- `enrichment-out` — after consensus+inline flow, before egress sink (shows enrichment `EgressEvent`)

### MqttEgressActor (2 stages)
- `mqtt-egress-in` — after `.Via(killSwitch)` (shows `EgressEvent` entering)
- `mqtt-egress-out` — after `.SelectMany(MapToMqttMessages)` (shows `MqttMessage` output)

### MqttConnectionActor (1 stage)
- `mqtt-send` — before `.SelectAsync(SendAsync)` (shows each `MqttMessage` before publish)

### DiscoveryActor capability listener (1 stage)
- `discovery-capability` — after `.Where(CapabilityLearned)` (shows capabilities entering)

### GrpcSnapshotConsumerActor (1 stage)
- `grpc-snapshot-in` — after `.Via(_killSwitch)` (shows `EgressEvent` entering gRPC consumer)

### WeatherGrpcService (2 stages)
- `grpc-stream-forecast` — after `.Where(location filter)` in `StreamForecasts` (shows updates streamed to client)
- `grpc-stream-enrichment` — after `.Where(location filter)` in `StreamEnrichments` (shows enrichments streamed to client)

**Total: 16 `.Log()` stages across 10 graphs in 8 files.**

## Risks / Trade-offs

**[Extractor evaluation when Debug disabled]** → Akka.Streams `.Log()` may evaluate the extractor even when Debug is off (unlike `_log.Debug` which skips formatting). For simple string interpolation extractors this is negligible. Mitigation: keep extractors trivially cheap — no LINQ, no serialization.

**[Log volume at Debug level]** → With 10 models × 2 locations × 60min polls, Debug output is modest (~20 elements/cycle through the pipeline). With shorter poll intervals or more locations, volume grows linearly. This is expected and desired for a Debug-level tracing tool.

## Open Questions

None.
