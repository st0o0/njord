## REMOVED Requirements

### Requirement: ForecastSnapshotActor stores the latest forecasts as ShardRegion entities
**Reason**: `ForecastSnapshotActor`, `EnrichmentSnapshotActor` and `GrpcSnapshotConsumerActor` no longer exist; there are no shard regions and no persisted forecast or enrichment snapshots.
**Migration**: Latest forecasts and analysis results are held in memory by `ForecastReadModel` (src/Njord.Pipeline/ForecastReadModel.cs), queried with `QueryForecast` and `QueryLatestAnalysis`; see the `consumer-model` capability.

### Requirement: EnrichmentSnapshotActor stores the latest enrichment results as ShardRegion entities
**Reason**: `ForecastSnapshotActor`, `EnrichmentSnapshotActor` and `GrpcSnapshotConsumerActor` no longer exist; there are no shard regions and no persisted forecast or enrichment snapshots.
**Migration**: Latest forecasts and analysis results are held in memory by `ForecastReadModel` (src/Njord.Pipeline/ForecastReadModel.cs), queried with `QueryForecast` and `QueryLatestAnalysis`; see the `consumer-model` capability.

### Requirement: GrpcSnapshotConsumerActor routes events from producer BroadcastHubs to ShardRegion entities
**Reason**: `ForecastSnapshotActor`, `EnrichmentSnapshotActor` and `GrpcSnapshotConsumerActor` no longer exist; there are no shard regions and no persisted forecast or enrichment snapshots.
**Migration**: Latest forecasts and analysis results are held in memory by `ForecastReadModel` (src/Njord.Pipeline/ForecastReadModel.cs), queried with `QueryForecast` and `QueryLatestAnalysis`; see the `consumer-model` capability.
