## MODIFIED Requirements

### Requirement: GetStatus returns server status with Timestamps
`OpsService.GetStatus` SHALL return a `StatusResponse` containing `string version`, `int64 uptime_seconds`, `google.protobuf.Timestamp process_start`, `BudgetStatus budget`, `repeated ModelStatus models`, `repeated string active_enrichments`, and the flags `bool budget_partial` and `bool models_partial`. `ModelStatus` SHALL use `google.protobuf.Timestamp` for `next_poll` and `optional last_change` (replacing v1's int64 fields). The service SHALL read the configuration from `ConfigManager` (`QueryConfig`), the budget from `BudgetTracker` (`QueryBudgetStatus`) and the poll states from `PollScheduler` (`QueryPollStates`), each through `ActorAsk` with its 5-second default timeout.

#### Scenario: Status includes model poll states
- **WHEN** a client calls `GetStatus` after polls have run
- **THEN** each `ModelStatus` SHALL contain location, model, phase, `next_poll` as Timestamp, miss_count
- **AND** `last_change` SHALL be present as Timestamp if the model has received data

#### Scenario: Budget usage included
- **WHEN** a client calls `GetStatus`
- **THEN** `BudgetStatus` SHALL contain monthly/daily limits and usage counts

#### Scenario: Scheduler timeout returns status without models
- **WHEN** `PollScheduler` does not respond within 5 seconds
- **THEN** the response contains an empty `models` list and `models_partial = true`, but still includes version, uptime, budget, and enrichments

#### Scenario: Budget tracker timeout returns zero usage
- **WHEN** `BudgetTracker` does not respond within 5 seconds
- **THEN** the response contains version, uptime, models and enrichments as normal, `BudgetStatus` reports zero monthly and daily usage against the configured limits, `budget_partial` is `true`, and the call logs a warning and does not fail

#### Scenario: Active enrichments lists enabled features
- **WHEN** a client calls `GetStatus`
- **THEN** `active_enrichments` SHALL contain the names of the enabled enrichment features and no others

### Requirement: GetTargets returns trigger target list
`OpsService.GetTargets` SHALL return a `GetTargetsResponse` with `repeated TriggerTarget targets` built from `PollScheduler`'s `PollStatesResult`. Each `TriggerTarget` SHALL contain `string location`, `string model`, `string phase`, `google.protobuf.Timestamp next_poll`, `optional google.protobuf.Timestamp last_change`, `int32 miss_count`, `optional int64 cycle_seconds`.

#### Scenario: Returns all configured pairs
- **WHEN** a client calls `GetTargets` with 2 locations and 3 models each
- **THEN** the response SHALL contain 6 `TriggerTarget` entries

#### Scenario: Scheduler timeout returns empty list
- **WHEN** `PollScheduler` does not respond within 5 seconds
- **THEN** the response contains an empty `targets` list without error and a warning is logged

#### Scenario: Each target carries its poll state
- **WHEN** a client calls `GetTargets` after polls have run
- **THEN** each `TriggerTarget` SHALL have `phase` "steady" or "discovery", a `next_poll` Timestamp and a `miss_count`
- **AND** `last_change` SHALL be set if the model has received data at least once and `cycle_seconds` SHALL be set if the scheduler has computed a cycle duration
