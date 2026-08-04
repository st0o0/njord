# Capability: grpc-v2-admin-service

## Purpose

AdminService gRPC service for configuration management. Replaces v1's ConfigService read/mutation RPCs with declarative SetLocations and consolidated SetSettings.

## Requirements

### Requirement: AdminService definition
`protos/njord/v2/admin.proto` SHALL define an `AdminService` with 6 RPCs: `GetConfig`, `StreamConfig`, `SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget`. The package SHALL be `njord.v2` with `csharp_namespace = "Njord.Grpc.V2"`. It SHALL import `common.proto`.

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 6 AdminService RPCs without errors

### Requirement: GetConfig returns current configuration
`AdminService.GetConfig` SHALL return the current `NjordConfig` including all locations with resolved models, default models, horizons, forecast days, poll interval, parameter config, detailed enrichment config, budget projection, and optional budget override.

#### Scenario: Config reflects current state
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain complete configuration with enrichment details and budget projection

### Requirement: StreamConfig pushes config changes
`AdminService.StreamConfig` SHALL be a server-streaming RPC. It SHALL send a full `NjordConfig` immediately on subscription and push a new snapshot on every configuration change.

#### Scenario: Initial config sent on subscribe
- **WHEN** a client calls `StreamConfig`
- **THEN** it SHALL immediately receive one `NjordConfig` message

#### Scenario: Config change triggers push
- **WHEN** the configuration changes
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

### Requirement: SetLocations uses replace-all semantics
`AdminService.SetLocations` SHALL accept a `SetLocationsRequest` with `repeated LocationInput locations`. Each `LocationInput` SHALL have `string name`, `double latitude`, `double longitude`, `repeated string models` (empty = use defaults). The RPC SHALL replace the entire location list atomically. It SHALL validate the resulting budget and reject if it exceeds limits.

#### Scenario: Replace all locations
- **WHEN** a client sends `SetLocations` with 3 locations
- **THEN** the configuration SHALL contain exactly those 3 locations
- **AND** any previously configured locations not in the list SHALL be removed

#### Scenario: Empty models uses defaults
- **WHEN** a `LocationInput` has empty `models`
- **THEN** the location SHALL use the global `default_models`

#### Scenario: Budget exceeded rejects mutation
- **WHEN** the resulting location/model matrix would exceed 80% of the monthly budget
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason`

#### Scenario: Empty list rejected without force
- **WHEN** a client sends `SetLocations` with an empty list
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason

### Requirement: SetSettings consolidates forecast settings
`AdminService.SetSettings` SHALL accept a `SetSettingsRequest` with optional fields: `int64 poll_interval_seconds`, `repeated int32 horizons`, `int32 forecast_days`, `ParameterConfig parameters`, `repeated string default_models`. Only provided fields SHALL be updated. It SHALL validate the resulting budget.

#### Scenario: Partial update applies only provided fields
- **WHEN** a client sends `SetSettings` with only `poll_interval_seconds = 1800`
- **THEN** only the poll interval SHALL change; horizons, forecast_days, parameters, and default_models SHALL remain unchanged

#### Scenario: Poll interval minimum enforced
- **WHEN** a client sends `SetSettings` with `poll_interval_seconds = 30`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`

### Requirement: DetailedEnrichmentConfig message

`DetailedEnrichmentConfig` SHALL contain fields: `ConsensusConfig consensus`, `AlertConfig alerts`, `DerivedConfig derived`, `TrendConfig trends`, `IndexConfig indices`, `HistoryConfig history`. It SHALL NOT contain any `reserved` statements or energy-related fields. Field numbers SHALL be sequential.

#### Scenario: No energy config field
- **WHEN** `DetailedEnrichmentConfig` is inspected
- **THEN** it SHALL have exactly 6 config fields with no gaps or reserved entries

### Requirement: IndexConfig message

`IndexConfig` SHALL contain fields: `optional bool enabled`, `optional double indoor_temp`, `optional double ideal_outdoor_temp`, `optional double heat_sensitivity`, `optional double humidity_sensitivity`, `optional double wind_sensitivity`, `optional double rain_sensitivity`, `optional double running_ideal_temp_low`, `optional double running_ideal_temp_high`, `optional double bbq_min_temp`, `optional double bbq_ideal_wind_low`, `optional double bbq_ideal_wind_high`. It SHALL NOT contain `heating_base_temp`, `cooling_base_temp`, or any `reserved` statements. Field numbers SHALL be sequential starting from 1.

#### Scenario: IndexConfig without degree-day fields
- **WHEN** `IndexConfig` is inspected
- **THEN** it SHALL have 12 fields with sequential numbering and no reserved entries

### Requirement: SetEnrichment updates enrichment configuration
`AdminService.SetEnrichment` SHALL accept a `SetEnrichmentRequest` with optional fields for each enrichment type (ConsensusConfig, AlertConfig, DerivedConfig, TrendConfig, IndexConfig, HistoryConfig). It SHALL NOT contain any `reserved` statements or energy-related fields. Field numbers SHALL be sequential. Only provided sub-messages SHALL be updated; within each sub-message, only provided fields SHALL be applied.

#### Scenario: Enable single enrichment
- **WHEN** a client sends `SetEnrichment` with only `consensus: { enabled: true }`
- **THEN** consensus SHALL be enabled; all other enrichment settings SHALL remain unchanged

#### Scenario: No energy field in mutation request
- **WHEN** `SetEnrichmentRequest` is inspected
- **THEN** it SHALL have exactly 6 optional config fields with no gaps

### Requirement: SetBudget updates budget override
`AdminService.SetBudget` SHALL accept a `SetBudgetRequest` with optional `int32 requests_per_month` and `int32 requests_per_minute`. When both are omitted, it SHALL clear the budget override and revert to free-tier defaults.

#### Scenario: Set custom budget
- **WHEN** a client sends `SetBudget` with `requests_per_month = 500000`
- **THEN** the budget override SHALL be applied and budget projection recalculated

#### Scenario: Clear budget override
- **WHEN** a client sends `SetBudget` with no fields set
- **THEN** the budget override SHALL be cleared, reverting to free-tier defaults

### Requirement: ConfigResponse includes config, projection, and warnings
All mutation RPCs SHALL return a `ConfigResponse` with `bool applied`, `NjordConfig config` (current state after mutation), `BudgetProjection budget_projection`, `repeated string warnings`, and `string rejection_reason` (when `applied = false`).

#### Scenario: Successful mutation returns updated config
- **WHEN** a mutation is applied successfully
- **THEN** `applied` SHALL be true and `config` SHALL reflect the new state

#### Scenario: Rejected mutation returns reason
- **WHEN** a mutation is rejected (e.g. budget exceeded)
- **THEN** `applied` SHALL be false and `rejection_reason` SHALL explain why
