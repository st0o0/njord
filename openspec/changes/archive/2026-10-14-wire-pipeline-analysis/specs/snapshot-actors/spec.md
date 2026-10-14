## REMOVED Requirements

### Requirement: ForecastSnapshotActor stores the latest forecasts as ShardRegion entities
**Reason**: `ForecastSnapshotActor` persisted forecasts for unary `GetForecast`
queries. With `GrpcConsumerActor` holding per-model forecasts in memory, the
snapshot shard is redundant. After restart, forecasts are "not available" until
the first poll completes — this matches the behavior of enrichments and avoids
persistence overhead for transient data.
**Migration**: `GetForecast` queries route through `GrpcConsumerActor` instead
of the `IForecastSnapshotRegion` shard. `ForecastSnapshotActor`, its shard
registration, persistence DTOs (`ForecastSnapshotDto`, `ForecastPointDto`,
`DailyForecastPointDto`, `ModelForecastDto`), and `IForecastSnapshotRegion`
marker interface are removed.
