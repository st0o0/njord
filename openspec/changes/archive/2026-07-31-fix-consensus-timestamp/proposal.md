## Why

ConsensusResult carries no computation timestamp. The gRPC layer sets
`consensus_updated_at` to the API-call time (`timeProvider.GetUtcNow()`)
instead of the time when the consensus was actually computed. Consumers that
use `updated_at` as the anchor for horizon-to-absolute-time conversion get
systematically shifted values: the hourly temperature curve is phase-shifted
by the delta between computation time and query time (typically several
hours), and daily horizons can silently reference the wrong calendar day when
the boundary crosses midnight UTC.

## What Changes

- Capture the computation wall-clock time inside `ConsensusSnapshot.Compute`
  and carry it through `ConsensusResult` and `EgressEvent.EnrichmentUpdate`
  to all egress layers.
- gRPC endpoints (`GetEnrichments`, `StreamEnrichments`) use the stored
  computation timestamp as `updated_at` / `consensus_updated_at` for
  consensus events instead of the current wall-clock time.
- Enrichment persistence DTO includes the computation timestamp so it
  survives snapshot recovery.

## Non-goals

- Changing horizon key semantics (h0 = offset from anchor, d0 = offset from
  today-at-computation-time). These stay relative.
- Adding an explicit `anchor` field to the proto — the existing `updated_at`
  field is sufficient once it carries the correct value.
- Fixing the ha-njord consumer — that code is correct once the server
  provides the right timestamp.
- No API-budget impact: this change does not add or alter polling.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `consensus-snapshot`: `ConsensusSnapshot` record gains a `ComputedAt` property set inside `Compute`.
- `egress-event`: `EnrichmentUpdate` gains an `UpdatedAt` timestamp propagated from the enrichment pipeline.
- `grpc-enrichment-api`: `GetEnrichments` and `StreamEnrichments` use the stored computation timestamp for consensus events.
- `snapshot-persistence-dtos`: `EnrichmentSnapshotDtos` must preserve `ComputedAt` through serialization round-trips.

## Impact

- **Domain**: `ConsensusSnapshot`, `ConsensusResult` gain one `DateTimeOffset` field each.
- **Enrichment pipeline**: `EnrichmentActor.ComputeAll` propagates the timestamp from snapshot to result and egress event.
- **Egress**: `EgressEvent.EnrichmentUpdate` gains an optional `UpdatedAt` field.
- **gRPC**: `WeatherGrpcService` and `EnrichmentProtoMapper` read the stored timestamp instead of calling `timeProvider.GetUtcNow()` for consensus events.
- **Persistence**: `EnrichmentSnapshotDtos` adds a nullable `computed_at` field to the consensus JSON payload (backwards-compatible: missing = fall back to current time on recovery).
- **Proto**: No `.proto` changes — the existing `updated_at` / `consensus_updated_at` fields carry the corrected value.
- **Tests**: Existing consensus tests use `FakeTimeProvider`, so the computation timestamp is deterministic. New assertions verify propagation.
