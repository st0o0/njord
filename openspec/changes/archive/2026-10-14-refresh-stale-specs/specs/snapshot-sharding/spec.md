## REMOVED Requirements

### Requirement: Akka.Streams fan-out for cross-shard forecast queries
**Reason**: There are no snapshot shard regions to fan out over; `IForecastSnapshotRegion`, `IEnrichmentSnapshotRegion`, `QueryAllForecasts` and `QueryAllEnrichments` were removed.
**Migration**: gRPC reads ask the single `ForecastReadModel` actor (marker `IForecastReadModel`) through `ActorAsk`; see `consumer-model` and `api-adapter-boundary`.

### Requirement: Akka.Streams fan-out for cross-shard enrichment queries
**Reason**: There are no snapshot shard regions to fan out over; `IForecastSnapshotRegion`, `IEnrichmentSnapshotRegion`, `QueryAllForecasts` and `QueryAllEnrichments` were removed.
**Migration**: gRPC reads ask the single `ForecastReadModel` actor (marker `IForecastReadModel`) through `ActorAsk`; see `consumer-model` and `api-adapter-boundary`.
