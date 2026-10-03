## Why

The tech debt audit identified three remaining items: (1) MqttEgressActor has zero test coverage and enrichment Compute() methods are untested outside integration, (2) persistent actors lack backoff supervision for journal/snapshot store failures, and (3) FetchOutcome lives in `Njord.Ingest` but is consumed by `Njord.Egress` and `Njord.Enrichment`, violating the three-zone architecture guardrail.

## Non-goals

- gRPC streaming tests (StreamForecasts/StreamEnrichments) — these require full stream materialization with gRPC server call contexts and are better addressed with integration tests against a running service, not unit tests.
- Adding new enrichment features or changing enrichment output.
- Changing the FetchOutcome record shape or its subtypes.
- Per-call Materializer() cleanup in WeatherGrpcService (separate concern).

## What Changes

- Add unit tests for MqttEgressActor (message mapping, delta publishing, ref lifecycle).
- Add unit tests for enrichment Compute() methods (ConsensusEnrichment, AlertEnrichment, DerivedEnrichment, EnergyEnrichment, IndexEnrichment, TrendEnrichment).
- Wrap persistent actors (SchedulerActor, ForecastHistoryActor, BudgetTrackerActor, ForecastSnapshotActor, EnrichmentSnapshotActor) with `BackoffSupervisor` for exponential backoff on persistence failures.
- Move `FetchOutcome` and `FetchFailureReason` from `Njord.Ingest` to `Njord.Domain.Weather` so Egress and Enrichment reference only Domain types.

## Capabilities

### New Capabilities

- `persistence-backoff`: BackoffSupervisor wrapping for all ReceivePersistentActor instances.

### Modified Capabilities

- `egress-stream-graph`: ModelStateActor imports FetchOutcome from Domain instead of Ingest.
- `enrichment-actor`: EnrichmentActor imports FetchOutcome from Domain instead of Ingest.

## Impact

- **Code**: FetchOutcome moves from `Njord.Ingest` to `Njord.Domain.Weather`; all `using Njord.Ingest` references to it must update. Akka.Hosting actor registrations gain BackoffSupervisor wrapping. New test files in `Njord.Tests/`.
- **No API budget impact** — no polling changes.
