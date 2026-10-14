## MODIFIED Requirements

### Requirement: GetEnrichments reads the latest analysis from ForecastReadModel
`WeatherService.GetEnrichments` SHALL first resolve the location through `ConfigManager`'s catalog (`QueryCatalog`, case-insensitive) and then ask `ForecastReadModel` with `QueryLatestAnalysis` for the resolved catalog location through `ActorAsk`. It SHALL map the reply with `LatestAnalysisResponse.ToProto()` from `Njord.Grpc/Mapping/AnalysisProtoMappings.cs`, which SHALL fill every payload field (`consensus`, `alerts`, `indices`, `trends`, `derived`) for which the read model holds a result and leave the others unset. When a consensus result is present, the response SHALL set `consensus_updated_at` to the timestamp the pipeline stamped on `PipelineEvent.ConsensusComputed` when it computed the consensus, not the time of the query.

#### Scenario: Enrichments queried via actor Ask
- **WHEN** a client calls `GetEnrichments` with location "lucerne"
- **THEN** the service asks `ForecastReadModel` with `QueryLatestAnalysis(lucerne)` and maps the reply to the proto response

#### Scenario: Every available enrichment kind is returned
- **WHEN** the read model holds a consensus, an alert, a derived, a trend and an index result for "lucerne" and a client calls `GetEnrichments` for "lucerne"
- **THEN** the response sets `consensus`, `consensus_updated_at`, `alerts`, `derived`, `trends` and `indices` in one response

#### Scenario: Missing kinds stay unset
- **WHEN** the read model holds only an alert and a trend result for "lucerne"
- **THEN** the response sets `alerts` and `trends` and leaves `consensus`, `consensus_updated_at`, `derived` and `indices` unset

#### Scenario: Location matched case-insensitively
- **WHEN** the configured location is "lucerne" and a client calls `GetEnrichments` with "Lucerne"
- **THEN** the service queries the read model for the configured location and returns its enrichments

#### Scenario: No data yet returns empty enrichments
- **WHEN** a client calls `GetEnrichments` before any analysis has been computed
- **THEN** `ForecastReadModel` answers `LatestAnalysisNotAvailable` and the response carries only the location, with empty enrichment fields (not an error)

#### Scenario: Unknown location returns NOT_FOUND
- **WHEN** a client calls `GetEnrichments` with an unconfigured location
- **THEN** the RPC returns gRPC status `NOT_FOUND`

#### Scenario: Consensus timestamp reflects computation time, not query time
- **WHEN** consensus was computed at 06:00 UTC and a client calls `GetEnrichments` at 12:00 UTC
- **THEN** `consensus_updated_at` is `06:00:00Z`, not `12:00:00Z`

#### Scenario: No consensus result omits the timestamp
- **WHEN** a client calls `GetEnrichments` and no consensus has been computed for the location
- **THEN** `consensus_updated_at` is left unset

### Requirement: StreamEnrichments pushes analysis events in real time
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC. A non-empty `location` SHALL first be resolved through `ConfigManager`'s catalog (case-insensitive); an unconfigured location SHALL fail the call with `NOT_FOUND` before any stream is requested. It SHALL then request a `SourceRef<PipelineEvent>` from `ForecastReadModel` (`RequestReadModelStream`), keep the `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed` and `IndexComputed` events whose location matches the requested location (every location when `location` is empty), map each with `ToEnrichmentEvent()` and write it to the response stream. `type_name` SHALL be the event's `Kind.ToWireName()` and `updated_at` SHALL be the event's `Timestamp` (the time the pipeline computed it) for every event type. A client cancellation SHALL shut down that call's stream through a shared kill switch.

#### Scenario: Alert update pushed to client
- **WHEN** the pipeline computes a new alert result for location "lucerne"
- **THEN** every `StreamEnrichments` client receives an `EnrichmentEvent` with type "alerts" and an `AlertUpdate` payload containing severity and confidence per alert type

#### Scenario: Events of other locations are not pushed
- **WHEN** a client streams with location "lucerne" and the pipeline computes results for "lucerne" and "zurich"
- **THEN** the client receives only the `EnrichmentEvent`s of "lucerne"

#### Scenario: Update carries computation timestamp
- **WHEN** a consensus result computed at 06:15:00 UTC is written to the stream at 06:15:02 UTC
- **THEN** `EnrichmentEvent.updated_at` is `06:15:00Z`

#### Scenario: Index update carries daily slices
- **WHEN** an index result arrives with 3 day score sets
- **THEN** the `IndexUpdate` contains 3 `DayScoreSet` entries with scores, envelopes, frost, and VPD
