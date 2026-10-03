## Context

The consensus pipeline computes hourly and daily forecasts with horizon keys
relative to the computation time (`h0` = now, `d0` = today at computation
time). Consumers must know the computation anchor to resolve these keys to
absolute timestamps.

Currently, the gRPC layer stamps `consensus_updated_at` with
`timeProvider.GetUtcNow()` at API-call or stream-emit time — not at
computation time. This causes a phase shift equal to the delta between
computation and query, manifesting as inverted temperature curves (hourly)
and wrong-day daily values.

The computation timestamp exists nowhere in the data path: `ConsensusSnapshot`
does not record it, `ConsensusResult` does not carry it, and
`EgressEvent.EnrichmentUpdate` does not propagate it.

## Goals / Non-Goals

**Goals:**

- Every consensus result carries the wall-clock time at which it was computed.
- gRPC responses (`GetEnrichments`, `StreamEnrichments`) use this stored
  timestamp, not the current time, for consensus events.
- The timestamp survives Akka Persistence snapshot round-trips.

**Non-Goals:**

- Changing horizon semantics (offset-based keys stay).
- Adding a new proto field — the existing `updated_at` /
  `consensus_updated_at` fields carry the corrected value.
- Modifying non-consensus enrichment timestamps — those are stateless
  computations emitted inline and `timeProvider.GetUtcNow()` is appropriate
  for them.

## Decisions

### D1: Store `ComputedAt` in `ConsensusSnapshot`, propagate through `ConsensusResult`

`ConsensusSnapshot.Compute` already receives `TimeProvider` and calls
`GetUtcNow()`. The snapshot will store this value as `ComputedAt`.
`ConsensusResult` gains a matching `ComputedAt` property, populated by
`EnrichmentActor.ComputeAll`.

**Why not store it only in ConsensusResult?** Because the snapshot is the
source of truth for the enrichment pipeline. Other enrichment features
(alerts, trends, derived) consume `ConsensusSnapshot` — if any future
feature needs the anchor, it's available without threading extra parameters.

### D2: Add `UpdatedAt` to `EgressEvent.EnrichmentUpdate`

`EnrichmentUpdate` gains a nullable `DateTimeOffset? UpdatedAt` field. For
consensus events, this is populated from `ConsensusResult.ComputedAt`. For
other enrichment types, it remains null and the gRPC layer falls back to
`timeProvider.GetUtcNow()`.

**Alternative considered**: A separate `ConsensusEgressEvent` subtype. Rejected
because it would fragment the egress dispatch logic for a single optional
field.

### D3: gRPC layer reads `UpdatedAt` from the event or result, not wall clock

`EnrichmentProtoMapper.MapToEvent` gains an overload or parameter that
accepts the stored timestamp. `WeatherGrpcService.GetEnrichments` reads
`ComputedAt` from the stored `ConsensusResult`. `StreamEnrichments` reads
`UpdatedAt` from the `EnrichmentUpdate` event.

For non-consensus enrichments, the existing `timeProvider.GetUtcNow()`
behavior is preserved.

### D4: Persistence round-trip via nullable `ComputedAt` on `ConsensusResult`

`ConsensusResult` already serializes via Newtonsoft.Json inside
`EnrichmentSnapshotDtos`. The new `ComputedAt` property gets a
`[JsonProperty("computedAt")]` attribute. On recovery from old snapshots
where the field is absent, `ComputedAt` defaults to `null`; the gRPC layer
falls back to `timeProvider.GetUtcNow()` in that case. No DTO schema
version bump needed — the field is additive and nullable.

## Risks / Trade-offs

- **[Stale fallback after upgrade]** On the first startup after deployment,
  recovered consensus snapshots lack `ComputedAt`. The gRPC layer falls back
  to `timeProvider.GetUtcNow()`, reproducing the old (buggy) behavior until
  the first poll cycle completes (~60 min max). → Acceptable; self-healing.

- **[Non-consensus enrichments still use wall clock]** Alerts, trends, etc.
  use `timeProvider.GetUtcNow()` as their timestamp. This is correct for
  those types (they have no offset-based horizons), but creates a minor
  inconsistency in the `updated_at` semantics within `EnrichmentEvent`. →
  Documented in the spec; consumers should only use `updated_at` for
  horizon resolution on consensus events.
