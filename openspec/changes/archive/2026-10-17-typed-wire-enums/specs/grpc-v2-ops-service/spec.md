## MODIFIED Requirements

### Requirement: GetTargets returns trigger target list
`OpsService.GetTargets` SHALL return a `GetTargetsResponse` with `repeated TriggerTarget targets` built from the poll manager's `PollStatesResult`. Each `TriggerTarget` SHALL contain `string location`, `string model`, `PollPhase phase`, `google.protobuf.Timestamp next_poll`, `optional google.protobuf.Timestamp last_change`, `int32 miss_count`, `optional int64 cycle_seconds`.

#### Scenario: Returns all configured pairs
- **WHEN** a client calls `GetTargets` with 2 locations and 3 models each
- **THEN** the response SHALL contain 6 `TriggerTarget` entries

#### Scenario: Poll manager timeout returns empty list
- **WHEN** the poll manager does not respond within 5 seconds
- **THEN** the response contains an empty `targets` list without error and a warning is logged

#### Scenario: Each target carries its poll state
- **WHEN** a client calls `GetTargets` after polls have run
- **THEN** each `TriggerTarget` SHALL have `phase` set to the `PollPhase` steady or discovery member, a `next_poll` Timestamp and a `miss_count`
- **AND** `last_change` SHALL be set if the model has received data at least once and `cycle_seconds` SHALL be set if the target's poll worker has learned a cycle duration
