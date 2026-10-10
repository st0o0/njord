## MODIFIED Requirements

### Requirement: GrpcConsumerActor serves unary and streaming RPCs

`GrpcConsumerActor` SHALL hold the latest pipeline results in-memory per
location for unary query responses. The in-memory state SHALL include per-model
forecasts (not just the latest single forecast) so `GetForecast` can be served
from it. The `ForecastSnapshotActor` shard region is removed; all forecast
queries go through `GrpcConsumerActor`.

#### Scenario: Unary query
- **WHEN** a gRPC client calls `GetEnrichments` for a location
- **THEN** `GrpcConsumerActor` responds from its in-memory state with typed
  analysis results — no persistence, no shard region

#### Scenario: Unary forecast query
- **WHEN** a gRPC client calls `GetForecast` for a location and model
- **THEN** `GrpcConsumerActor` responds from its in-memory per-model forecast
  state — no `ForecastSnapshotActor`, no persistence

#### Scenario: Unary query after restart
- **WHEN** `GrpcConsumerActor` restarts and a client queries before the first
  poll cycle completes
- **THEN** the response indicates no data is available yet

#### Scenario: Streaming RPC
- **WHEN** a gRPC client calls `StreamForecasts` or `StreamEnrichments`
- **THEN** the gRPC service requests a `SourceRef` from `GrpcConsumerActor`'s
  secondary BroadcastHub and streams filtered `PipelineEvent` instances

#### Scenario: Dead gRPC streaming client
- **WHEN** a gRPC streaming client disconnects without completing
- **THEN** the secondary BroadcastHub's IdleTimeout detects and cleans up the
  dead subscription without affecting other streaming clients

### Requirement: EnrichmentSnapshotActor shard region is removed

The `EnrichmentSnapshotActor` shard region, its persistence DTOs
(`EnrichmentSnapshotDto`, `EnrichmentEntryDto`), and all untyped enrichment
messages (`UpdateEnrichment`, `QueryEnrichment`, `QueryEnrichmentCompleted`,
`QueryAllEnrichmentsCompleted`, `IWithEnrichmentKey`) SHALL be removed.

#### Scenario: No enrichment shard region
- **WHEN** the actor system starts
- **THEN** no `EnrichmentSnapshotActor` shard region is registered

#### Scenario: No untyped messages
- **WHEN** the codebase is searched for `IWithEnrichmentKey` or `UpdateEnrichment`
- **THEN** no results are found
