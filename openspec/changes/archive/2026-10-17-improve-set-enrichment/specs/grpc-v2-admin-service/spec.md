## MODIFIED Requirements

### Requirement: SetEnrichment updates analysis configuration
`AdminService.SetEnrichment` SHALL accept a `SetEnrichmentRequest` with optional `ConsensusConfig consensus = 1`, `AlertConfig alerts = 2`, `DerivedConfig derived = 3`, `TrendConfig trends = 4` and `IndexConfig indices = 5`, `repeated EnrichmentSection reset = 7` and `bool dry_run = 8`, with field number 6 `reserved`. Only provided sub-messages SHALL be updated; within each sub-message, only provided fields SHALL be applied.

#### Scenario: Enable single enrichment
- **WHEN** a client sends `SetEnrichment` with only `consensus: { enabled: true }`
- **THEN** consensus is enabled; all other analysis settings remain unchanged

#### Scenario: No history field in mutation request
- **WHEN** `SetEnrichmentRequest` is inspected
- **THEN** it has five optional config fields numbered 1–5, `reserved 6`, and the additional `reset` and `dry_run` fields

### Requirement: Mutation RPCs delegate to ConfigManager
All mutation RPCs SHALL map their request with a `ToCommand()` extension to a `Njord.Messages` command (`SetLocations` → `UpdateLocations`, `SetSettings` → `UpdateSettings`, `SetEnrichment` → `UpdateAnalysisConfig`, `SetBudget` → `UpdateBudget`), ask the `ConfigManager` actor (`IConfigManager`) through `ActorAsk`, and map the `*Completed`/`*Rejected`/`*Failed` reply to `ConfigResponse`. `ConfigManager` SHALL validate and persist the change through `IConfigWriter`: `UpdateAsync` per options section, and `UpdateManyAsync` for commands that touch several sections (`UpdateAnalysisConfig`). `AdminGrpcService` SHALL NOT read, clone or write options itself.

#### Scenario: SetEnrichment is handled by ConfigManager
- **WHEN** a client calls `SetEnrichment` with `alerts.enabled = false`
- **THEN** the service asks `ConfigManager` with an `UpdateAnalysisConfig` command, and `ConfigManager` persists `Njord:Enrichment:Alerts:Enabled = false` through `IConfigWriter`

#### Scenario: Rejection is mapped to the response
- **WHEN** `ConfigManager` answers `UpdateSettingsRejected("Poll interval must be at least 60 seconds")`
- **THEN** the RPC returns `ConfigResponse` with `applied = false` and that `rejection_reason`
