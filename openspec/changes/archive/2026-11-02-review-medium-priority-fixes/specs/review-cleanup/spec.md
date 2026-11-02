## ADDED Requirements

### Requirement: BudgetTracker handles correct snapshot deletion response

The BudgetTracker SHALL handle `DeleteSnapshotsSuccess` (plural) after calling `DeleteSnapshots(criteria)`, not `DeleteSnapshotSuccess` (singular).

#### Scenario: Snapshot cleanup completes
- **WHEN** the BudgetTracker persists a snapshot and calls `DeleteSnapshots` to remove old ones
- **THEN** the `DeleteSnapshotsSuccess` response is handled without hitting dead letters

### Requirement: Polling telemetry uses correct metric prefix

Polling telemetry counters SHALL use the `njord_polling_` prefix, not `njord_pipeline_`. The domain tag for `DomainKind.Forecast` SHALL be `"forecast"`, not `"weather"`.

#### Scenario: Metrics are scraped by Prometheus
- **WHEN** a Prometheus scrape collects polling metrics
- **THEN** counters are named `njord_polling_data_changed_total`, `njord_polling_domain_polls_total`, `njord_polling_cycle_learned_total`
- **THEN** the domain tag for Forecast data is `"forecast"`

### Requirement: Location names must be unique

The `NjordOptionsValidator` SHALL reject configurations with duplicate location names (case-insensitive).

#### Scenario: Two locations share a name
- **WHEN** the configuration contains two locations with the same name (case-insensitive)
- **THEN** startup validation fails with an error identifying the duplicate name

### Requirement: Model comparison rules declare domain requirements

ForecastModelComparison SHALL declare `DomainKind.Forecast` in its `DomainRequirements`. EnsembleModelComparison SHALL declare `DomainKind.Ensemble`. This prevents unnecessary evaluation on unrelated domain updates.

#### Scenario: Air quality data arrives
- **WHEN** an AirQuality domain update arrives at the FeatureEngine
- **THEN** ForecastModelComparison and EnsembleModelComparison are not evaluated

### Requirement: WeatherGrpcService uses a shared materializer

The `WeatherGrpcService` SHALL use a shared `IMaterializer` injected via constructor instead of creating one per streaming RPC call.

#### Scenario: Multiple streaming clients connect
- **WHEN** two gRPC clients open streaming connections
- **THEN** both streams use the same shared materializer instance
