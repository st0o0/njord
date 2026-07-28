# trigger-targets-rpc Specification

## Purpose

RPC for retrieving all configured location/model trigger targets with their
current poll state. Exposes scheduler internals to gRPC clients for monitoring
and manual-trigger UIs.

## Requirements

### Requirement: GetTriggerTargets RPC on ConfigService
`ConfigService` SHALL expose a unary `GetTriggerTargets` RPC that accepts a `GetTriggerTargetsRequest` (empty message) and returns a `GetTriggerTargetsResponse` containing a flat list of all configured location/model pairs with their current poll state. The RPC SHALL query the `SchedulerActor` for poll states with a 5-second timeout.

#### Scenario: Returns all configured location/model pairs
- **WHEN** a client calls `GetTriggerTargets` with two locations ("Lucerne" with 3 models, "Zurich" with 2 models)
- **THEN** the response SHALL contain exactly 5 `TriggerTarget` entries
- **AND** each entry SHALL have non-empty `location` and `model` fields

#### Scenario: Each target includes poll state
- **WHEN** a client calls `GetTriggerTargets` after polls have run
- **THEN** each `TriggerTarget` SHALL include `phase` ("steady" or "discovery"), `next_poll` as a `Timestamp`, and `miss_count`
- **AND** `last_change` SHALL be present if the model has received data at least once
- **AND** `cycle_seconds` SHALL be present if the scheduler has computed a cycle duration

#### Scenario: SchedulerActor timeout returns empty list
- **WHEN** the `SchedulerActor` does not respond within 5 seconds (e.g. during startup stash)
- **THEN** the response SHALL contain an empty `targets` list
- **AND** the RPC SHALL NOT throw an error

### Requirement: TriggerTarget proto message uses Timestamp types
The `TriggerTarget` message SHALL use `google.protobuf.Timestamp` for temporal fields `next_poll` and `last_change` instead of raw `int64` unix seconds. The proto file SHALL import `google/protobuf/timestamp.proto`.

#### Scenario: Proto compiles with Timestamp import
- **WHEN** `dotnet build` runs
- **THEN** the `TriggerTarget` type SHALL be generated with `Google.Protobuf.WellKnownTypes.Timestamp` properties for `next_poll` and `last_change`

#### Scenario: Timestamp round-trips correctly
- **WHEN** the server converts a `DateTimeOffset` via `Timestamp.FromDateTimeOffset()`
- **THEN** the client SHALL receive the same instant when calling `.ToDateTimeOffset()` on the response field
