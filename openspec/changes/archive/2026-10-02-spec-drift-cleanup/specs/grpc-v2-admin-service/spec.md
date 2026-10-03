## REMOVED Requirements

### Requirement: SetLocations uses replace-all semantics
**Reason**: Scenarios are dropped or renamed so the text matches the code (OpenSpec refuses to drop scenarios through MODIFIED): Empty list rejected without force.
**Migration**: Re-added in this delta as `SetLocations replaces the whole location list` with the corrected scenarios.

## MODIFIED Requirements

### Requirement: ConfigResponse includes config, projection, and warnings
All mutation RPCs SHALL return a `ConfigResponse` with `bool applied`, `NjordConfig config` (current state after mutation), `BudgetProjection budget_projection`, `repeated string warnings`, and `string rejection_reason` (when `applied = false`).

#### Scenario: Successful mutation returns updated config
- **WHEN** a mutation is applied successfully
- **THEN** `applied` SHALL be true and `config` SHALL reflect the new state

#### Scenario: Rejected mutation returns reason
- **WHEN** a mutation is rejected (e.g. budget exceeded)
- **THEN** `applied` SHALL be false and `rejection_reason` SHALL explain why

#### Scenario: Budget warning near the limit
- **WHEN** a mutation is applied and the projected monthly API usage is above 80% and at most 100% of the monthly budget
- **THEN** `applied` SHALL be true and `warnings` SHALL contain a message stating the projected usage percentage

## ADDED Requirements

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
