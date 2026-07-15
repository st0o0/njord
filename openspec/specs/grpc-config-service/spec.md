# grpc-config-service Specification

## Purpose

Separate gRPC service for querying and streaming njord configuration. Defined in
its own proto file so config-only clients need not import forecast types.

## Requirements

### Requirement: GetConfig returns current njord configuration
`ConfigService.GetConfig` SHALL return the current `NjordConfig` including full enrichment details (not just enabled flags). The `NjordConfig` message SHALL include `ConsensusConfig`, `AlertConfig` (with all thresholds), `EnergyConfig`, `IndexConfig`, `HistoryConfig`, `DerivedConfig`, and `TrendConfig` sub-messages providing complete round-trip fidelity.

#### Scenario: Config reflects current state
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain all configured locations with their resolved models, the default model list, horizons [3,6,12,24,48,72], forecast_days 4, and enabled enrichment features

#### Scenario: Per-location models are resolved
- **WHEN** a location has per-location model overrides
- **THEN** the `LocationConfig.models` field SHALL contain the resolved model list for that location (merged with globals, deduplicated)

#### Scenario: Config includes enrichment details
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain full enrichment configuration including alert thresholds, energy parameters, index base temps, consensus method, and history retention settings

#### Scenario: Config includes budget projection
- **WHEN** a client calls `GetConfig`
- **THEN** the `NjordConfig` SHALL include a `BudgetProjection` showing projected vs. actual usage

### Requirement: StreamConfig pushes config changes
`ConfigService.StreamConfig` SHALL be a server-streaming RPC. It SHALL send a full `NjordConfig` snapshot immediately on subscription (current state) and push a new snapshot whenever the configuration changes.

#### Scenario: Initial config sent on subscribe
- **WHEN** a client calls `StreamConfig`
- **THEN** it SHALL immediately receive one `NjordConfig` message with the current configuration

#### Scenario: Config change triggers push
- **WHEN** the njord configuration changes (e.g. via hot-reload or future config API)
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

#### Scenario: Stream ends on client disconnect
- **WHEN** a client disconnects from the `StreamConfig` stream
- **THEN** the server-side stream SHALL be disposed cleanly

### Requirement: ConfigService is a separate gRPC service
`ConfigService` SHALL be defined in `protos/njord/v1/config_service.proto`. It SHALL include read RPCs (`GetConfig`, `StreamConfig`, `GetStatus`) and mutation RPCs (`AddLocation`, `RemoveLocation`, `UpdateLocation`, `UpdateForecastSettings`, `UpdateEnrichmentConfig`, `UpdateBudget`).

#### Scenario: Proto compiles independently
- **WHEN** `config_service.proto` is compiled
- **THEN** it SHALL not depend on `forecast_service.proto`

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 9 ConfigService RPCs without errors
