# Capability: trigger-poll-rpc

## Purpose

gRPC RPC for on-demand poll triggering: allows tests and tooling to fire immediate poll cycles for specific location/model pairs without waiting for the scheduled interval.

## Requirements

### Requirement: TriggerPoll RPC on ConfigService
`ConfigService` SHALL expose a unary `TriggerPoll` RPC that accepts a `TriggerPollRequest` with optional `location` and `model` string fields. The RPC SHALL tell the `SchedulerActor` to schedule immediate poll(s) for the matching location/model pairs and return a `TriggerPollResponse` with the count of triggered polls and a list of target strings (format `"{location}/{model}"`). The RPC SHALL NOT wait for the polls to complete -- it is fire-and-forget.

#### Scenario: Trigger all models for all locations
- **WHEN** a client calls `TriggerPoll` with empty `location` and empty `model`
- **THEN** the response SHALL contain `triggered_count` equal to the total number of configured location x model pairs
- **AND** the `targets` list SHALL contain one entry per pair in `"{location}/{model}"` format

#### Scenario: Trigger all models for a specific location
- **WHEN** a client calls `TriggerPoll` with `location = "home"` and empty `model`
- **THEN** only models configured for location "home" SHALL be triggered
- **AND** `triggered_count` SHALL equal the number of models for that location

#### Scenario: Trigger a specific location and model
- **WHEN** a client calls `TriggerPoll` with `location = "home"` and `model = "icon_d2"`
- **THEN** `triggered_count` SHALL be 1
- **AND** `targets` SHALL contain exactly `"home/icon_d2"`

#### Scenario: Unknown location returns zero triggers
- **WHEN** a client calls `TriggerPoll` with `location = "nonexistent"`
- **THEN** `triggered_count` SHALL be 0
- **AND** `targets` SHALL be empty

### Requirement: TriggerPoll proto messages
The `config_service.proto` SHALL define `TriggerPollRequest` with optional string fields `location` and `model`, and `TriggerPollResponse` with `int32 triggered_count` and `repeated string targets`.

#### Scenario: Proto compiles with new messages
- **WHEN** the proto file is compiled
- **THEN** `TriggerPollRequest` and `TriggerPollResponse` types SHALL be generated in namespace `Njord.Grpc.V1`

### Requirement: SchedulerActor accepts TriggerImmediatePoll message
The `SchedulerActor` SHALL handle a `TriggerImmediatePoll(string Location, string Model)` message by scheduling an immediate `ScheduledPoll` for the specified target. When `Location` or `Model` is empty, it SHALL expand to all matching configured pairs.

#### Scenario: Immediate poll bypasses normal schedule
- **WHEN** `SchedulerActor` receives `TriggerImmediatePoll("home", "icon_d2")`
- **THEN** a `ScheduledPoll("home", "icon_d2")` SHALL be offered to the pipeline queue within 1 second
- **AND** the normal schedule for that model SHALL NOT be disrupted
