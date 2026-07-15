# config-mutation-api Specification

## Purpose

gRPC mutation RPCs on `ConfigService` for adding, removing, and updating
locations, forecast settings, enrichment configuration, and budget overrides
at runtime. Every mutation validates constraints (uniqueness, budget) before
persisting and returns a `ConfigResponse` with the resulting config and
budget projection.

## Requirements

### Requirement: AddLocation creates a new location with budget validation
`ConfigService.AddLocation` SHALL accept a name, latitude, longitude, and optional model list. It SHALL validate the name is unique, coordinates are plausible, and the projected budget with the new location stays within limits. On success it SHALL persist the config, propagate via IOptionsMonitor, and return ConfigResponse with `applied = true`.

#### Scenario: Successful location add
- **WHEN** a client calls `AddLocation` with name "zürich", lat 47.37, lon 8.54
- **THEN** the config SHALL include the new location, the response SHALL have `applied = true`, and `StreamConfig` subscribers SHALL receive the updated config

#### Scenario: Duplicate name rejected
- **WHEN** a client calls `AddLocation` with a name that already exists
- **THEN** the response SHALL have `applied = false` and `rejection_reason` explaining the conflict

#### Scenario: Budget exceeded rejected
- **WHEN** adding the location would push projected monthly calls above the budget limit
- **THEN** the response SHALL have `applied = false` and `rejection_reason` mentioning budget

#### Scenario: Budget warning at 80%
- **WHEN** adding the location pushes projected usage above 80% but below 100%
- **THEN** the response SHALL have `applied = true` with a warning in `warnings`

### Requirement: RemoveLocation removes a location and its data
`ConfigService.RemoveLocation` SHALL accept a location name and optional `force` flag. It SHALL remove the location from config, persist, propagate, and return ConfigResponse.

#### Scenario: Successful removal
- **WHEN** a client calls `RemoveLocation` with name "zürich"
- **THEN** the location SHALL be removed from config, SchedulerActor SHALL stop poll jobs for it, and SnapshotActors SHALL clear data for it

#### Scenario: Unknown location rejected
- **WHEN** a client calls `RemoveLocation` with a name that doesn't exist
- **THEN** the response SHALL have `applied = false`

### Requirement: UpdateLocation modifies an existing location
`ConfigService.UpdateLocation` SHALL accept a location name and optional fields (models, latitude, longitude). Only provided fields SHALL be applied (patch semantics).

#### Scenario: Update models for a location
- **WHEN** a client calls `UpdateLocation` with name "lucerne" and models ["icon_d2", "ecmwf_ifs025"]
- **THEN** the location's model list SHALL be updated to exactly those models

#### Scenario: Update coordinates
- **WHEN** a client calls `UpdateLocation` with new latitude/longitude
- **THEN** the location's coordinates SHALL be updated and the next poll cycle SHALL use the new coordinates

### Requirement: UpdateForecastSettings changes forecast configuration
`ConfigService.UpdateForecastSettings` SHALL accept optional fields for poll interval, horizons, forecast days, parameter config, and default models. Only provided fields SHALL be applied.

#### Scenario: Change poll interval
- **WHEN** a client calls `UpdateForecastSettings` with `poll_interval_seconds = 1800`
- **THEN** the poll interval SHALL change to 30 minutes and SchedulerActor SHALL adjust its timer

#### Scenario: Change horizons
- **WHEN** a client calls `UpdateForecastSettings` with `horizons = [3, 12, 24]`
- **THEN** the horizon list SHALL be updated and DiscoveryActor SHALL update discovery payloads

#### Scenario: Parameter group change warns about restart
- **WHEN** a client calls `UpdateForecastSettings` with changed parameter groups
- **THEN** the response SHALL have `applied = true` with a warning "Parameter changes take effect after restart"

### Requirement: UpdateEnrichmentConfig changes enrichment settings
`ConfigService.UpdateEnrichmentConfig` SHALL accept optional sub-messages for each enrichment type. Each sub-message carries all configurable fields (enabled flag + type-specific parameters). Only provided sub-messages SHALL be applied.

#### Scenario: Enable trends enrichment
- **WHEN** a client calls `UpdateEnrichmentConfig` with `trends = { enabled: true }`
- **THEN** the trends enrichment SHALL be enabled

#### Scenario: Change alert thresholds
- **WHEN** a client calls `UpdateEnrichmentConfig` with `alerts = { frost_threshold: -2.0 }`
- **THEN** the frost threshold SHALL change to -2.0°C

#### Scenario: Change energy parameters
- **WHEN** a client calls `UpdateEnrichmentConfig` with `energy = { flow_temp: 40.0, carnot_efficiency: 0.5 }`
- **THEN** the energy enrichment SHALL use the new parameters

### Requirement: UpdateBudget overrides the request budget
`ConfigService.UpdateBudget` SHALL accept optional requests_per_month and requests_per_minute. Setting them overrides the free-tier defaults. Clearing them (zero/unset) reverts to free-tier defaults.

#### Scenario: Set custom budget
- **WHEN** a client calls `UpdateBudget` with `requests_per_month = 500000`
- **THEN** the effective budget SHALL use 500000/month instead of the free-tier 300000

#### Scenario: Clear budget override
- **WHEN** a client calls `UpdateBudget` with no fields set
- **THEN** the budget SHALL revert to the free-tier defaults

### Requirement: Every mutation returns ConfigResponse with budget projection
Every mutation RPC SHALL return `ConfigResponse` containing the new `NjordConfig`, a `BudgetProjection` (projected monthly calls, limit, usage %, within_budget), optional `warnings`, `applied` flag, and `rejection_reason`.

#### Scenario: ConfigResponse includes budget projection
- **WHEN** any mutation RPC succeeds
- **THEN** the response SHALL include `budget_projection` with accurate projected usage
