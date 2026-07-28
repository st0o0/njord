# server-status-api Specification

## Purpose

`GetStatus` RPC on `ConfigService` exposing server health, version, uptime,
budget consumption counters, and per-model fetch status so operators and
dashboards can monitor njord without scraping logs.

## Requirements

### Requirement: GetStatus returns server health and budget usage
`ConfigService.GetStatus` SHALL return a `ServerStatus` message containing server
version, uptime in seconds, budget usage (monthly/daily limits and used counts),
per-model poll status from the SchedulerActor, a list of active enrichment
feature names, and `process_start_utc` as a Unix timestamp. Budget usage SHALL be
sourced from the `BudgetTrackerActor` via `Ask<BudgetUsage>` with the existing
5-second timeout.

#### Scenario: Status includes version and uptime
- **WHEN** a client calls `GetStatus`
- **THEN** the response SHALL contain the server version from assembly metadata and uptime since service start

#### Scenario: Budget usage shows monthly and daily counts
- **WHEN** a client calls `GetStatus` after njord has fetched data
- **THEN** the `BudgetStatus` SHALL show monthly_used, daily_used, and corresponding limits sourced from `BudgetTrackerActor`

#### Scenario: Per-model status shows poll state from SchedulerActor
- **WHEN** a client calls `GetStatus` with active models in Discovery and Steady phases
- **THEN** each `ModelStatus` SHALL show location, model, phase ("discovery" or "steady"), next_poll_utc (unix seconds), last_change_utc (optional unix seconds), miss_count, and cycle_seconds (optional)

#### Scenario: Active enrichments lists enabled features
- **WHEN** a client calls `GetStatus` with consensus, alerts, and trends enabled
- **THEN** `active_enrichments` SHALL contain `["consensus", "alerts", "trends"]`

#### Scenario: SchedulerActor unreachable returns empty model list
- **WHEN** a client calls `GetStatus` but the SchedulerActor Ask times out
- **THEN** the response SHALL contain version, uptime, and budget as normal with an empty `models` list
- **AND** the call SHALL NOT fail with an error

#### Scenario: BudgetTrackerActor unreachable returns zero usage
- **WHEN** a client calls `GetStatus` but the `BudgetTrackerActor` Ask times out
- **THEN** the response SHALL contain version, uptime, and models as normal
- **AND** `BudgetStatus` SHALL show `monthly_used=0` and `daily_used=0`
- **AND** the call SHALL log a warning but NOT fail

#### Scenario: Status includes process start timestamp
- **WHEN** a client calls `GetStatus`
- **THEN** the response SHALL contain `process_start_utc` as Unix timestamp (seconds since epoch) of when the service process started

### Requirement: ServerStatus proto includes active_enrichments field
The `ServerStatus` proto message SHALL include `repeated string active_enrichments = 5`
listing the names of enabled enrichment features.

#### Scenario: All features enabled
- **WHEN** all 7 enrichment features are enabled
- **THEN** `active_enrichments` SHALL contain `["consensus", "alerts", "derived", "trends", "indices", "energy", "history"]`

#### Scenario: No features enabled
- **WHEN** all enrichment features are disabled
- **THEN** `active_enrichments` SHALL be empty

### Requirement: ModelStatus proto reflects poll state fields
The `ModelStatus` proto message SHALL contain: `location` (string, field 1),
`model` (string, field 2), `phase` (string, field 3), `next_poll_utc` (int64,
field 4), `last_change_utc` (optional int64, field 5), `miss_count` (int32,
field 6), `cycle_seconds` (optional int64, field 7).

#### Scenario: Proto fields map to ModelPollState
- **WHEN** a model is in Steady phase with cycle 3h and 1 miss
- **THEN** the `ModelStatus` SHALL have phase="steady", cycle_seconds=10800, miss_count=1
