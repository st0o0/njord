## REMOVED Requirements

### Requirement: Forecast snapshot DTOs use only serialization-safe types
**Reason**: Forecast and enrichment snapshots are no longer persisted. `ForecastSnapshotDto`, `ModelForecastDto`, `ForecastPointDto`, `EnrichmentSnapshotDto`, `EnrichmentEntryDto` and `EnrichmentSnapshotMapping` were removed together with the snapshot actors; `Njord.Persistence` holds only the budget and poll-scheduler records.
**Migration**: See `persistence-records` and `persistence-funkarr-alignment` for the remaining persistence records.

### Requirement: Enrichment snapshot DTOs use discriminated wrapper
**Reason**: Forecast and enrichment snapshots are no longer persisted. `ForecastSnapshotDto`, `ModelForecastDto`, `ForecastPointDto`, `EnrichmentSnapshotDto`, `EnrichmentEntryDto` and `EnrichmentSnapshotMapping` were removed together with the snapshot actors; `Njord.Persistence` holds only the budget and poll-scheduler records.
**Migration**: See `persistence-records` and `persistence-funkarr-alignment` for the remaining persistence records.

### Requirement: DTO mapping handles missing parameters gracefully
**Reason**: Forecast and enrichment snapshots are no longer persisted. `ForecastSnapshotDto`, `ModelForecastDto`, `ForecastPointDto`, `EnrichmentSnapshotDto`, `EnrichmentEntryDto` and `EnrichmentSnapshotMapping` were removed together with the snapshot actors; `Njord.Persistence` holds only the budget and poll-scheduler records.
**Migration**: See `persistence-records` and `persistence-funkarr-alignment` for the remaining persistence records.

### Requirement: Enrichment result inner JSON wire names are pinned
**Reason**: Forecast and enrichment snapshots are no longer persisted. `ForecastSnapshotDto`, `ModelForecastDto`, `ForecastPointDto`, `EnrichmentSnapshotDto`, `EnrichmentEntryDto` and `EnrichmentSnapshotMapping` were removed together with the snapshot actors; `Njord.Persistence` holds only the budget and poll-scheduler records.
**Migration**: See `persistence-records` and `persistence-funkarr-alignment` for the remaining persistence records.

### Requirement: CLAUDE.md caveat is removed
**Reason**: Forecast and enrichment snapshots are no longer persisted. `ForecastSnapshotDto`, `ModelForecastDto`, `ForecastPointDto`, `EnrichmentSnapshotDto`, `EnrichmentEntryDto` and `EnrichmentSnapshotMapping` were removed together with the snapshot actors; `Njord.Persistence` holds only the budget and poll-scheduler records.
**Migration**: See `persistence-records` and `persistence-funkarr-alignment` for the remaining persistence records.
