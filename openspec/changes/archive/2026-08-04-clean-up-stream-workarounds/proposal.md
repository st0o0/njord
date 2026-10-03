# Clean Up Stream Workarounds

## Problem

The Akka.Streams pipeline code has accumulated workarounds and anti-patterns:

1. **GrpcSnapshotConsumerActor** duplicates the entire `StreamConsumerActor` base class (retry, kill-switch, watch, stash) instead of extending it — the only reason was an extra resolution phase for snapshot actors.
2. **WeatherGrpcService** gRPC streaming endpoints use `TakeWhile` polling the CancellationToken, which only checks between elements — if the source is idle, the stream hangs until the next element arrives.
3. **EnrichmentActor** catches *all* exceptions from the SensorHub Ask with a bare `catch`, logging a misleading "timed out" message for any failure type.
4. **Mutable closure state** captured in stream operators across 3 pipelines (EnrichmentActor, ModelStateActor, MqttEgressActor) — safe only at parallelism=1 but fragile.
5. **`is var _ ? update : update`** trick in GrpcSnapshotConsumerActor to force side-effects in a switch expression — obscure, unnecessary.
6. **Empty `PostStop()` overrides** in 3 actors (PipelineActor, SchedulerActor, GrpcSnapshotConsumerActor).

## Approach

One change, six tasks ordered by dependency and risk:

- **A** — Refactor GrpcSnapshotConsumerActor to extend StreamConsumerActor (removes duplication + the `is var _` trick)
- **B** — Replace TakeWhile in gRPC streaming with KillSwitch + CancellationToken registration
- **C** — Narrow bare catch to specific exception types in EnrichmentActor
- **D** — Convert mutable closures to `Scan` in 3 pipelines
- **E** — Remove empty PostStop overrides
- **F** — Run tests + slopwatch + format to verify

## Scope

Stream-layer cleanup only. No functional changes, no new features, no API surface changes.
