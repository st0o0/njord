## MODIFIED Requirements

### Requirement: GetConfig returns current configuration
`AdminService.GetConfig` SHALL return the current `NjordConfig` including all locations with resolved models, default models, horizons, forecast days, poll interval, parameter config, detailed enrichment config, budget projection, and optional budget override. The returned config SHALL reflect any runtime mutations applied via `SetEnrichment`, `SetSettings`, `SetLocations`, or `SetBudget`, even when baseline configuration is provided via environment variables.

#### Scenario: Config reflects current state
- **WHEN** a client calls `GetConfig`
- **THEN** the response SHALL contain complete configuration with enrichment details and budget projection

#### Scenario: Config reflects mutation over env var
- **WHEN** environment variable `Njord__Enrichment__Alerts__Enabled=true` is set
- **AND** a prior `SetEnrichment` mutation disabled alerts
- **THEN** `GetConfig` SHALL return `alerts.enabled = false`

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

## ADDED Requirements

### Requirement: Mutation methods use WritableOptions pattern
All mutation RPCs (`SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget`) SHALL use `IWritableOptions<NjordOptions>.Update(Action<NjordOptions>)` to apply changes. The `Update` call SHALL handle cloning, persistence, and reload. AdminService SHALL NOT manually clone options or call a separate persistence service.

#### Scenario: SetEnrichment uses Update
- **WHEN** a client calls `SetEnrichment` with `alerts.enabled = false`
- **THEN** the service SHALL call `IWritableOptions.Update(opt => opt.Enrichment.Alerts.Enabled = false)`
- **AND** the mutation SHALL be persisted and reloaded atomically
