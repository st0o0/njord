## MODIFIED Requirements

### Requirement: GetConfig returns current njord configuration
`ConfigService.GetConfig` SHALL return the current `NjordConfig` including full enrichment details (not just enabled flags). The `NjordConfig` message SHALL include `ConsensusConfig`, `AlertConfig` (with all thresholds), `EnergyConfig`, `IndexConfig`, `HistoryConfig`, `DerivedConfig`, and `TrendConfig` sub-messages providing complete round-trip fidelity.

#### Scenario: Config includes enrichment details
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain full enrichment configuration including alert thresholds, energy parameters, index base temps, consensus method, and history retention settings

#### Scenario: Config includes budget projection
- **WHEN** a client calls `GetConfig`
- **THEN** the `NjordConfig` SHALL include a `BudgetProjection` showing projected vs. actual usage

### Requirement: ConfigService is a separate gRPC service
`ConfigService` SHALL be defined in `protos/njord/v1/config_service.proto`. It SHALL include read RPCs (`GetConfig`, `StreamConfig`, `GetStatus`) and mutation RPCs (`AddLocation`, `RemoveLocation`, `UpdateLocation`, `UpdateForecastSettings`, `UpdateEnrichmentConfig`, `UpdateBudget`).

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 9 ConfigService RPCs without errors
