## ADDED Requirements

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

## REMOVED Requirements

### Requirement: DetailedEnrichmentConfig message
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "DetailedEnrichmentConfig carries one config per analysis feature" in this capability.

### Requirement: SetEnrichment updates enrichment configuration
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "SetEnrichment updates analysis configuration" in this capability.

### Requirement: Mutation methods use WritableOptions pattern
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Mutation RPCs delegate to ConfigManager" in this capability.
