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
`OpsService.GetStatus` SHALL return a `StatusResponse` containing `string version`, `int64 uptime_seconds`, `google.protobuf.Timestamp process_start`, `BudgetStatus budget`, `repeated ModelStatus models`, `repeated string active_enrichments`. `ModelStatus` SHALL use `google.protobuf.Timestamp` for `next_poll` and `optional last_change` (replacing v1's int64 fields).

#### Scenario: Status includes model poll states
- **WHEN** a client calls `GetStatus` after polls have run
- **THEN** each `ModelStatus` SHALL contain location, model, phase, `next_poll` as Timestamp, miss_count
- **AND** `last_change` SHALL be present as Timestamp if the model has received data

#### Scenario: Budget usage included
- **WHEN** a client calls `GetStatus`
- **THEN** `BudgetStatus` SHALL contain monthly/daily limits and usage counts

#### Scenario: Scheduler timeout returns status without models
- **WHEN** the SchedulerActor does not respond within 5 seconds
- **THEN** the response SHALL contain empty `models` list but still include version, uptime, budget, and enrichments

### Requirement: GetTargets returns trigger target list
`OpsService.GetTargets` SHALL return a `GetTargetsResponse` with `repeated TriggerTarget targets`. Each `TriggerTarget` SHALL contain `string location`, `string model`, `string phase`, `google.protobuf.Timestamp next_poll`, `optional google.protobuf.Timestamp last_change`, `int32 miss_count`, `optional int64 cycle_seconds`.

#### Scenario: Returns all configured pairs
- **WHEN** a client calls `GetTargets` with 2 locations and 3 models each
- **THEN** the response SHALL contain 6 `TriggerTarget` entries

#### Scenario: Scheduler timeout returns empty list
- **WHEN** the SchedulerActor does not respond within 5 seconds
- **THEN** the response SHALL contain an empty `targets` list without error

### Requirement: TriggerPoll triggers immediate polls
`OpsService.TriggerPoll` SHALL accept `TriggerPollRequest` with optional `string location` and `string model` (empty = wildcard). It SHALL return `TriggerPollResponse` with `int32 triggered_count` and `repeated string targets` (format `"{location}/{model}"`).

#### Scenario: Trigger specific model
- **WHEN** a client calls `TriggerPoll` with `location = "Lucerne"` and `model = "icon_d2"`
- **THEN** `triggered_count` SHALL be 1 and `targets` SHALL contain `"Lucerne/icon_d2"`

#### Scenario: Trigger all
- **WHEN** a client calls `TriggerPoll` with empty location and model
- **THEN** `triggered_count` SHALL equal the total number of configured location/model pairs

#### Scenario: Unknown location returns zero
- **WHEN** a client calls `TriggerPoll` with a nonexistent location
- **THEN** `triggered_count` SHALL be 0 and `targets` SHALL be empty
