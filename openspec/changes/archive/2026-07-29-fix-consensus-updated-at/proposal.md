## Why

`GetEnrichmentsResponse` carries no timestamp for its consensus payload, even
though the server already computes one correctly when mapping the same data
for `StreamEnrichments`. Because the unary RPC drops it, the HA client
(`grpc_client.py`, separate repo) fabricates `consensus_updated_at` as its own
`now()` on initial load. That makes the client's derived horizon offset
resolve to `0`, so the consensus tile reads horizon `h0` as of whenever the
njord process last computed consensus (e.g. early morning) instead of the
actual current hour — while per-model sensors are unaffected because they
index forecasts by absolute timestamp, not a derived offset. The bug
self-corrects once the first `StreamEnrichments` event arrives with a real
server timestamp, but until then the consensus tile can show a temperature
outside the range of every contributing model.

## What Changes

- Add a `consensus_updated_at` (`google.protobuf.Timestamp`) field to
  `GetEnrichmentsResponse` in `protos/njord/v2/weather.proto`.
- `WeatherGrpcService.GetEnrichments` sets this field from the same
  `timeProvider.GetUtcNow()` value already used when mapping the consensus
  payload via `EnrichmentProtoMapper.MapToEvent`, instead of discarding it.
- No change to `StreamEnrichments`/`EnrichmentEvent` — that path already
  carries `updated_at` correctly.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `grpc-enrichment-api`: `GetEnrichments` response gains a
  `consensus_updated_at` timestamp field, so unary consumers no longer have
  to guess a timestamp for horizon-relative consensus data.

## Impact

- `protos/njord/v2/weather.proto` — new field on `GetEnrichmentsResponse`
  (wire-compatible addition, new field number).
- `src/Njord/Grpc/WeatherGrpcService.cs` — `GetEnrichments` populates the new
  field.
- Regenerated proto C# bindings (build-time codegen, no manual edits).
- Out of scope: the HA client (`grpc_client.py`, separate repo) still needs a
  follow-up change to consume `consensus_updated_at` instead of defaulting to
  local `now()` — tracked separately, not in this repo.
- No polling/API-budget impact — this only changes what's already computed
  per request/stream event; no new Open-Meteo calls.

## Non-goals

- Not fixing or touching the HA client's `grpc_client.py` — that's a
  different repository and a separate change.
- Not adding per-enrichment-type timestamps for alerts/indices/trends/energy/
  derived/history — those enrichments use direct values rather than derived
  horizon offsets, so they aren't affected by this staleness class of bug.
- Not changing `StreamEnrichments`/`EnrichmentEvent`, which already carries a
  correct `updated_at`.
