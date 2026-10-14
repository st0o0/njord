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
`AdminService.GetConfig` SHALL return the current `NjordConfig` including all locations with resolved models, default models, horizons, forecast days, poll interval, parameter config, detailed enrichment config, budget projection, and optional budget override. The returned config SHALL reflect any runtime mutations applied via `SetEnrichment`, `SetSettings`, `SetLocations`, or `SetBudget`, even when baseline configuration is provided via environment variables.

#### Scenario: Config reflects current state
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain complete configuration with enrichment details and budget projection

#### Scenario: Config reflects mutation over env var
- **WHEN** environment variable `Njord__Enrichment__Alerts__Enabled=true` is set
- **AND** a prior `SetEnrichment` mutation disabled alerts
- **THEN** `GetConfig` SHALL return `alerts.enabled = false`

### Requirement: StreamConfig pushes config changes
`AdminService.StreamConfig` SHALL be a server-streaming RPC. It SHALL send a full `NjordConfig` immediately on subscription and push a new snapshot on every configuration change.

#### Scenario: Initial config sent on subscribe
- **WHEN** a client calls `StreamConfig`
- **THEN** it SHALL immediately receive one `NjordConfig` message

#### Scenario: Config change triggers push
- **WHEN** the configuration changes
- **THEN** all `StreamConfig` subscribers SHALL receive a new `NjordConfig` snapshot

### Requirement: SetSettings consolidates forecast settings
`AdminService.SetSettings` SHALL accept a `SetSettingsRequest` with optional fields: `int64 poll_interval_seconds`, `repeated int32 horizons`, `int32 forecast_days`, `ParameterConfig parameters`, `repeated string default_models`. Only provided fields SHALL be updated. It SHALL validate the resulting budget.

#### Scenario: Partial update applies only provided fields
- **WHEN** a client sends `SetSettings` with only `poll_interval_seconds = 1800`
- **THEN** only the poll interval SHALL change; horizons, forecast_days, parameters, and default_models SHALL remain unchanged

#### Scenario: Poll interval minimum enforced
- **WHEN** a client sends `SetSettings` with `poll_interval_seconds = 30`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`

### Requirement: IndexConfig message

`IndexConfig` SHALL contain fields: `optional bool enabled`, `optional double indoor_temp`, `optional double ideal_outdoor_temp`, `optional double heat_sensitivity`, `optional double humidity_sensitivity`, `optional double wind_sensitivity`, `optional double rain_sensitivity`, `optional double running_ideal_temp_low`, `optional double running_ideal_temp_high`, `optional double bbq_min_temp`, `optional double bbq_ideal_wind_low`, `optional double bbq_ideal_wind_high`. It SHALL NOT contain `heating_base_temp`, `cooling_base_temp`, or any `reserved` statements. Field numbers SHALL be sequential starting from 1.

#### Scenario: IndexConfig without degree-day fields
- **WHEN** `IndexConfig` is inspected
- **THEN** it SHALL have 12 fields with sequential numbering and no reserved entries

### Requirement: SetBudget updates budget override
`AdminService.SetBudget` SHALL accept a `SetBudgetRequest` with optional `int32 requests_per_month` and `int32 requests_per_minute`. When both are omitted, it SHALL clear the budget override and revert to free-tier defaults. When `requests_per_month` is provided and is ≤ 0, the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason. When `requests_per_minute` is provided and is ≤ 0, the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason.

#### Scenario: Set custom budget
- **WHEN** a client sends `SetBudget` with `requests_per_month = 500000`
- **THEN** the budget override SHALL be applied and budget projection recalculated

#### Scenario: Clear budget override
- **WHEN** a client sends `SetBudget` with no fields set
- **THEN** the budget override SHALL be cleared, reverting to free-tier defaults

#### Scenario: Zero monthly budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason` containing "must be greater than zero"

#### Scenario: Negative monthly budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = -100`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason` containing "must be greater than zero"

#### Scenario: Zero per-minute budget rejected
- **WHEN** a client sends `SetBudget` with `requests_per_minute = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`

#### Scenario: Valid month with zero minute rejected
- **WHEN** a client sends `SetBudget` with `requests_per_month = 300000` and `requests_per_minute = 0`
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false`

### Requirement: ConfigResponse matches subsequent GetConfig
All mutation RPCs SHALL return a `ConfigResponse` with `bool applied`, `NjordConfig config` (current state after mutation), `BudgetProjection budget_projection`, `repeated string warnings`, and `string rejection_reason` (when `applied = false`). When `applied = true`, the `config` field in the response SHALL be identical to what a subsequent `GetConfig` call returns.

#### Scenario: Successful mutation returns updated config
- **WHEN** a mutation is applied successfully
- **THEN** `applied` SHALL be true and `config` SHALL reflect the new state

#### Scenario: Response config matches GetConfig
- **WHEN** a mutation returns `applied = true` with a `config` snapshot
- **AND** the client immediately calls `GetConfig`
- **THEN** the `GetConfig` response SHALL match the mutation response's `config`

#### Scenario: Rejected mutation returns reason
- **WHEN** a mutation is rejected (e.g. budget exceeded)
- **THEN** `applied` SHALL be false and `rejection_reason` SHALL explain why

#### Scenario: Budget warning near the limit
- **WHEN** a mutation is applied and the projected monthly API usage is above 80% and at most 100% of the monthly budget
- **THEN** `applied` SHALL be true and `warnings` SHALL contain a message stating the projected usage percentage

### Requirement: SetLocations replaces the whole location list
`AdminService.SetLocations` SHALL accept a `SetLocationsRequest` with `repeated LocationInput locations`. Each `LocationInput` SHALL have `string name`, `double latitude`, `double longitude`, `repeated string models` (empty = use defaults). The RPC SHALL replace the entire location list atomically. It SHALL validate the resulting budget and reject if it exceeds limits.

#### Scenario: Replace all locations
- **WHEN** a client sends `SetLocations` with 3 locations
- **THEN** the configuration SHALL contain exactly those 3 locations
- **AND** any previously configured locations not in the list SHALL be removed

#### Scenario: Empty models uses defaults
- **WHEN** a `LocationInput` has empty `models`
- **THEN** the location SHALL use the global `default_models`

#### Scenario: Budget exceeded rejects mutation
- **WHEN** the projected monthly API usage of the resulting location/model matrix would exceed 100% of the monthly budget
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and `rejection_reason`

#### Scenario: Empty list rejected
- **WHEN** a client sends `SetLocations` with an empty list
- **THEN** the RPC SHALL return `ConfigResponse` with `applied = false` and a rejection reason

### Requirement: DetailedEnrichmentConfig carries one config per analysis feature
`DetailedEnrichmentConfig` SHALL contain exactly the fields `ConsensusConfig consensus = 1`, `AlertConfig alerts = 2`, `DerivedConfig derived = 3`, `TrendConfig trends = 4` and `IndexConfig indices = 5`, and SHALL mark field number 6 (the removed history config) as `reserved`. It SHALL NOT contain energy-related fields.

#### Scenario: No history or energy config field
- **WHEN** `DetailedEnrichmentConfig` is inspected
- **THEN** it has the five feature configs numbered 1–5 and `reserved 6`

### Requirement: SetEnrichment updates analysis configuration
`AdminService.SetEnrichment` SHALL accept a `SetEnrichmentRequest` with optional `ConsensusConfig consensus = 1`, `AlertConfig alerts = 2`, `DerivedConfig derived = 3`, `TrendConfig trends = 4` and `IndexConfig indices = 5`, with field number 6 `reserved`. Only provided sub-messages SHALL be updated; within each sub-message, only provided fields SHALL be applied.

#### Scenario: Enable single enrichment
- **WHEN** a client sends `SetEnrichment` with only `consensus: { enabled: true }`
- **THEN** consensus is enabled; all other analysis settings remain unchanged

#### Scenario: No history field in mutation request
- **WHEN** `SetEnrichmentRequest` is inspected
- **THEN** it has five optional config fields numbered 1–5 and `reserved 6`

### Requirement: Mutation RPCs delegate to ConfigManager
All mutation RPCs SHALL map their request with a `ToCommand()` extension to a `Njord.Messages` command (`SetLocations` → `UpdateLocations`, `SetSettings` → `UpdateSettings`, `SetEnrichment` → `UpdateAnalysisConfig`, `SetBudget` → `UpdateBudget`), ask the `ConfigManager` actor (`IConfigManager`) through `ActorAsk`, and map the `*Completed`/`*Rejected`/`*Failed` reply to `ConfigResponse`. `ConfigManager` SHALL validate and persist the change through `IConfigWriter.UpdateAsync` per options section. `AdminGrpcService` SHALL NOT read, clone or write options itself.

#### Scenario: SetEnrichment is handled by ConfigManager
- **WHEN** a client calls `SetEnrichment` with `alerts.enabled = false`
- **THEN** the service asks `ConfigManager` with an `UpdateAnalysisConfig` command, and `ConfigManager` persists `Njord:Enrichment:Alerts:Enabled = false` through `IConfigWriter`

#### Scenario: Rejection is mapped to the response
- **WHEN** `ConfigManager` answers `UpdateSettingsRejected("Poll interval must be at least 60 seconds")`
- **THEN** the RPC returns `ConfigResponse` with `applied = false` and that `rejection_reason`

