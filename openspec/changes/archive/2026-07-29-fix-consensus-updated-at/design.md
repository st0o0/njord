## Context

`WeatherGrpcService.GetEnrichments` (`src/Njord/Grpc/WeatherGrpcService.cs`)
already builds a correct `EnrichmentEvent` per enrichment type via
`EnrichmentProtoMapper.MapToEvent(location, typeName, resultObj,
timeProvider.GetUtcNow())`, then copies only the `oneof` payload
(`evt.Consensus`, `evt.Alerts`, etc.) into `GetEnrichmentsResponse` — the
`updated_at` computed on that event is discarded. `GetEnrichmentsResponse`
(`protos/njord/v2/weather.proto`) has no timestamp field of its own, unlike
`EnrichmentEvent`, which carries `updated_at` for the streaming path.

Only consensus is affected in practice: it's the one enrichment type whose
payload (`ConsensusUpdate` → `HorizonConsensus.horizon`, e.g. `"h0"`) is a
horizon *offset* rather than an absolute value, so a consumer needs a
reference timestamp to resolve what `h0` currently means. The other six
enrichment types (alerts, indices, trends, energy, derived, history) carry
direct values with no horizon-offset indirection, so they don't need a
timestamp to be interpreted correctly.

## Goals / Non-Goals

**Goals:**
- Give unary `GetEnrichments` callers a real server timestamp to resolve
  consensus horizon offsets against, closing the gap that lets a client
  default to its own `now()`.
- Keep the fix minimal and wire-compatible — additive proto field only.

**Non-Goals:**
- Not restructuring `GetEnrichmentsResponse` to carry a timestamp per
  enrichment type — only consensus needs one, so only consensus gets one.
- Not changing `EnrichmentEvent`/`StreamEnrichments`, which is unaffected.
- Not touching the HA client (`grpc_client.py`) — separate repo, separate
  change.

## Decisions

- **Field placement**: add `consensus_updated_at` directly on
  `GetEnrichmentsResponse` (top-level, sibling to `consensus`), not nested
  inside `ConsensusUpdate`. `ConsensusUpdate` is shared with the streaming
  path via the `EnrichmentEvent.payload` oneof, where the wrapper already
  carries `updated_at` — adding a timestamp inside `ConsensusUpdate` itself
  would duplicate that field on every streamed consensus update for no
  benefit. Keeping it on the unary response only matches where the gap
  actually is.
- **Timestamp source**: reuse the exact `timeProvider.GetUtcNow()` value
  already passed into `EnrichmentProtoMapper.MapToEvent` for the consensus
  result, rather than introducing a second `now()` call or threading the
  domain-level consensus computation time through. This matches what
  `StreamEnrichments` already does for the same data and keeps the fix to a
  couple of lines — the response is a live snapshot, so "when this snapshot
  was assembled" is an accurate and sufficient timestamp for horizon
  resolution.
- **Field number**: append as the next free field number on
  `GetEnrichmentsResponse` (after `consensus = 8`), preserving wire
  compatibility for existing clients that don't yet read the new field.

## Risks / Trade-offs

- [Only consensus gets a timestamp, so a future enrichment type that adds
  its own horizon-offset semantics would reintroduce this bug class] →
  Mitigated by scoping the fix explicitly to the one enrichment type that
  currently needs it, and noting the pattern (horizon-offset payloads need a
  reference timestamp) in the spec requirement so it's discoverable if it
  recurs.
- [HA client won't consume the new field until its own follow-up change
  ships, so the bug persists end-to-end until both sides land] → Accepted;
  this repo's fix is a prerequisite for that follow-up, not a full fix on
  its own. Called out explicitly in the proposal's Impact section.
