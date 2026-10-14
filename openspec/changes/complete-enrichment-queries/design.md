## Context

See proposal.md for the motivation. Today `ForecastReadModel` stores `LatestResults.LastAnalysis`, a single
`PipelineEvent`, and `LatestAnalysisResult(LocationId, PipelineEvent Analysis)` hands that one event to the
gRPC mapping, which routes it into one `GetEnrichmentsResponse` field by `EnrichmentEvent.PayloadCase`.
`StreamEnrichments` keeps every enrichment event regardless of `request.location`; `StreamForecasts`
already filters by name but never validates the requested location.

Constraints: `Njord.Messages` is Akka-free and holds every query result; `PipelineEvent` variants are typed
records (no `object`); `EnrichmentKind` lives in `Njord.Messages.Pipeline`; the gRPC project only asks and
maps; the streaming golden masters (`ProtoMappingGoldenMasterSpec`) must not change.

## Goals / Non-Goals

**Goals:**
- One `GetEnrichments` response carries every kind the read model holds for the location.
- Location filtering for both streaming RPCs with one shared semantics (empty = all, known = that location,
  unknown = `NOT_FOUND`).

**Non-Goals:**
- Changing `QueryForecast`/`GetForecast` location keying.
- Any change to the mapping of a single event (`ToEnrichmentEvent`, `ForecastUpdate`).

## Decisions

### Result shape: one typed nullable property per kind

`LatestAnalysisResult(LocationId Location, ConsensusComputed? Consensus, AlertComputed? Alerts,
DerivedComputed? Derived, TrendComputed? Trends, IndexComputed? Indices)`.

- Each property has the exact event type of its kind, so the mapping reads `result.Alerts?.Result`
  without casts or a `switch`, and the compiler proves a consensus slot can only hold a consensus event.
- The events (not the bare results) are kept because the event carries the computation `Timestamp`
  needed for `consensus_updated_at`, and future per-kind timestamps stay available.
- The property set mirrors the proto fields one-to-one, so a reader sees at a glance what
  `GetEnrichmentsResponse` can contain.
- Alternative considered: `IReadOnlyDictionary<EnrichmentKind, IEnrichmentEvent>`. Keys are typed, but
  every reader must downcast `IEnrichmentEvent` to the concrete record and nothing stops a mismatched
  kind/event pair; it also makes record equality reference-based for the dictionary. Rejected.
- Naming stays `LatestAnalysisResult` / `LatestAnalysisNotAvailable` (the `<Noun>Result` /
  `<Noun>NotAvailable` scheme). `NotAvailable` is answered when no kind has arrived yet, so
  "no data yet" keeps its current wire behaviour (only the location set).

The read model's per-location state holds the `LatestAnalysisResult` itself and updates one slot with a
`with` expression per event, so the query answer is the stored record. The state is written by the
materialized stream stage and read by the actor's query handlers, so it becomes an immutable map that the
stream swaps as a whole (a `volatile` reference) instead of a `Dictionary` mutated from two threads.

### `IEnrichmentEvent.Location`

Each enrichment record exposes `LocationId Location` (computed from its result/snapshot, so record
equality and the positional shape are unchanged). The read model keys enrichment events by it and the
enrichment stream filters by it, replacing the duplicated per-variant location `switch`.

### Location semantics for the streams

The main specs already say `StreamForecasts` accepts an optional location with "empty = all", and
`StreamEnrichments` delivers to "subscribers matching the location filter". Neither says what an unknown
location does. Following `GetForecast`/`GetEnrichments`, a non-empty location is resolved through the
catalog with `FindLocation` (case-insensitive) and an unknown one fails with `NOT_FOUND`; the resolved
`LocationId` is then compared with each event's `LocationId` (case-insensitive equality). An empty location
skips the catalog lookup and streams every location, as before.

`GetEnrichments` also passes the resolved catalog `LocationId` to `QueryLatestAnalysis` instead of the raw
request string: the read model keys its state by the name the pipeline reports (the configured spelling),
so a request in a different letter case would otherwise miss the data although the catalog accepted it.

### Testing the streams without a server

`WeatherGrpcServiceSpec` gets a fake read model that answers `RequestReadModelStream` with a `SourceRef`
of a finite `Source` of events, and a collecting `IServerStreamWriter`. The finite source completes, the
RPC returns, and the spec asserts the written events — no waits, no timing.

## Risks / Trade-offs

- [ha-njord relied on unfiltered streams or on an unknown location keeping a stream open] → ha-njord
  subscribes with configured locations or empty; the old behaviour for an unknown location was a silent
  stream, which is a bug, not a contract. Called out in the proposal impact.
- [`LatestAnalysisResult` shape change breaks other `Njord.Messages` users] → only the read model, the
  gRPC mapping and their specs use it (checked with grep); all are updated in this change.
- [Response size grows: up to five payloads instead of one] → the payloads were already sent one per
  call; the stream sends all of them every cycle anyway.
