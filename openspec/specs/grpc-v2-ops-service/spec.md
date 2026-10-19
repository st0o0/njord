# Capability: grpc-v2-ops-service

## Purpose

OpsService gRPC service for operational queries and actions — server status, trigger target discovery, and on-demand poll triggering. All temporal fields use Timestamp.
## Requirements
### Requirement: OpsService definition
`protos/njord/v2/ops.proto` SHALL define an `OpsService` with 3 RPCs: `GetStatus`, `GetTargets`, `TriggerPoll`. The package SHALL be `njord.v2` with `csharp_namespace = "Njord.Grpc.V2"`. It SHALL import `common.proto` for `google.protobuf.Timestamp`.

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 3 OpsService RPCs without errors

### Requirement: GetStatus returns server status with Timestamps
`OpsService.GetStatus` SHALL return a `StatusResponse` containing `string version`, `int64 uptime_seconds`, `google.protobuf.Timestamp process_start`, `BudgetStatus budget`, `repeated LocationStatus locations`, `repeated string active_enrichments`, and the flags `bool budget_partial` and `bool locations_partial`. Polling is per location (not per model). The service SHALL read the budget from `BudgetTracker` (`QueryBudgetStatus`) and the poll states from the poll manager (`IPollManager`, `QueryPollStates`), each through `ActorAsk` with its 5-second default timeout.

#### Scenario: Status includes location poll states
- **WHEN** a client calls `GetStatus` after polls have run
- **THEN** each location status SHALL contain location, phase, `next_poll` as Timestamp, miss_count, and per-domain fetch status
- **AND** `last_change` SHALL be present as Timestamp if the location has received data

#### Scenario: Budget usage included
- **WHEN** a client calls `GetStatus`
- **THEN** `BudgetStatus` SHALL contain monthly/daily limits and usage counts

#### Scenario: Poll manager timeout returns status without models
- **WHEN** the poll manager does not respond within 5 seconds
- **THEN** the response contains an empty `models` list and `models_partial = true`, but still includes version, uptime, budget, and enrichments

#### Scenario: Budget tracker timeout returns zero usage
- **WHEN** `BudgetTracker` does not respond within 5 seconds
- **THEN** the response contains version, uptime, models and enrichments as normal, `BudgetStatus` reports zero monthly and daily usage against the configured limits, `budget_partial` is `true`, and the call logs a warning and does not fail

#### Scenario: Active enrichments lists enabled features
- **WHEN** a client calls `GetStatus`
- **THEN** `active_enrichments` SHALL contain the names of the enabled enrichment features and no others

### Requirement: GetTargets returns trigger target list
`OpsService.GetTargets` SHALL return a `GetTargetsResponse` with `repeated TriggerTarget targets` built from the poll manager's `PollStatesResult`. Each `TriggerTarget` SHALL contain `string location`, `PollPhase phase`, `google.protobuf.Timestamp next_poll`, `optional google.protobuf.Timestamp last_change`, `int32 miss_count`, `optional int64 cycle_seconds`. Targets are per location (not per model).

#### Scenario: Returns all configured locations
- **WHEN** a client calls `GetTargets` with 2 locations configured
- **THEN** the response SHALL contain 2 `TriggerTarget` entries

#### Scenario: Poll manager timeout returns empty list
- **WHEN** the poll manager does not respond within 5 seconds
- **THEN** the response contains an empty `targets` list without error and a warning is logged

#### Scenario: Each target carries its poll state
- **WHEN** a client calls `GetTargets` after polls have run
- **THEN** each `TriggerTarget` SHALL have `phase` set to the `PollPhase` steady or discovery member, a `next_poll` Timestamp and a `miss_count`
- **AND** `last_change` SHALL be set if the model has received data at least once and `cycle_seconds` SHALL be set if the target's poll worker has learned a cycle duration

### Requirement: TriggerPoll triggers immediate polls
`OpsService.TriggerPoll` SHALL accept `TriggerPollRequest` with optional `string location` (empty = wildcard), ask the poll manager (`IPollManager`) with `TriggerImmediatePoll` and return `TriggerPollResponse` with `int32 triggered_count` and `repeated string targets` (location names).

#### Scenario: Trigger specific location
- **WHEN** a client calls `TriggerPoll` with `location = "Lucerne"`
- **THEN** `triggered_count` SHALL be 1 and `targets` SHALL contain `"Lucerne"`

#### Scenario: Trigger all
- **WHEN** a client calls `TriggerPoll` with empty location
- **THEN** `triggered_count` SHALL equal the total number of configured locations

#### Scenario: Unknown location returns zero
- **WHEN** a client calls `TriggerPoll` with a nonexistent location
- **THEN** `triggered_count` SHALL be 0 and `targets` SHALL be empty

#### Scenario: Trigger does not wait for fetches
- **WHEN** a client calls `TriggerPoll`
- **THEN** the response SHALL return the scheduled count and targets without waiting for any fetch to complete

