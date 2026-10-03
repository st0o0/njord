## Context

The codebase has 608 passing tests but MqttEgressActor has zero coverage and enrichment Compute() methods are only exercised through integration-level actor tests. Five persistent actors (SchedulerActor, ForecastHistoryActor, BudgetTrackerActor, ForecastSnapshotActor, EnrichmentSnapshotActor) crash on journal/snapshot failures with no restart strategy. FetchOutcome, defined in `Njord.Ingest`, is consumed by `Njord.Egress.ModelStateActor` and `Njord.Enrichment.EnrichmentActor`, creating a cross-zone dependency that violates the architecture guardrail.

## Goals / Non-Goals

**Goals:**
- MqttEgressActor has unit tests covering message mapping, delta deduplication, and ref lifecycle.
- Each enrichment Compute() method has direct unit tests with known inputs and verified outputs.
- Persistent actors survive journal/snapshot store transient failures via BackoffSupervisor with exponential backoff.
- FetchOutcome lives in `Njord.Domain.Weather`, removing Ingest → Egress/Enrichment cross-zone imports.

**Non-Goals:**
- gRPC streaming integration tests.
- Changing FetchOutcome's type shape.
- Adding backoff to non-persistent actors.

## Decisions

### D1: Move FetchOutcome to Njord.Domain.Weather

**Choice:** Move `FetchOutcome.cs` from `Njord.Ingest` to `Njord.Domain.Weather`. Update the namespace and all `using` statements. FetchOutcome contains only domain types (ModelForecast, WeatherModel) and describes a domain concept (a fetch result), not an ingest-specific one.

**Why not a Pipeline namespace?** FetchOutcome is consumed across Pipeline, Egress, and Enrichment. Domain is the shared layer all three zones already reference.

### D2: BackoffSupervisor wrapping via Akka.Hosting WithActors

**Choice:** Register persistent actors via `builder.WithActors((system, registry) => ...)` using `BackoffSupervisor.Props(Backoff.OnFailure(...))` with DI-resolved child props. Min backoff 3s, max 30s, random factor 0.2.

**Why OnFailure (not OnStop)?** Persistence failures throw exceptions (crash), they don't gracefully stop the actor. `Backoff.OnFailure` restarts after crashes.

### D3: Direct unit tests for enrichment Compute

**Choice:** Test each enrichment feature's `Compute()` method directly by constructing `ModelSnapshot` inputs and asserting on the `EgressEvent` output. No actor system needed — these are pure functions.

**Why not actor tests?** The enrichment pipeline (EnrichmentActor + streams) is already tested via `EnrichmentFeatureContractSpec`. The gap is testing the individual Compute logic with edge cases (empty snapshots, single model, missing parameters).

### D4: MqttEgressActor tests via Akka.Hosting.TestKit

**Choice:** Test MqttEgressActor as an actor with TestProbes standing in for EgressActor and MqttConnectionActor. Verify message mapping, delta deduplication (same payload not re-published), and Terminated handling.

## Risks / Trade-offs

- [BackoffSupervisor changes actor paths] → Consumer actors resolve via `GetActorAsync` from the registry, not by path. The registry stores the BackoffSupervisor ref, and the child is a nested actor. Messages sent to the registry ref are forwarded by the BackoffSupervisor. No impact on existing resolution.
- [Moving FetchOutcome is a broad refactoring touch] → The change is mechanical (namespace + using statements). No logic changes. Tests will catch any missed references.
- [Internal enrichment classes need InternalsVisibleTo for testing] → The project already has `InternalsVisibleTo("Njord.Tests")` set up.
