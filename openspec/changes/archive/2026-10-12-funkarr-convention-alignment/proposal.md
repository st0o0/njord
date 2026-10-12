## Why

The codebase deviates from FunkArr conventions in logging setup, message naming,
response hierarchies, persistence mapping, and actor/state naming. A full
convention audit identified 20+ findings across these areas. Aligning now —
while the project is 0.x and breaking renames are allowed — avoids accumulating
further drift before v1 locks the public surface.

## What Changes

- **BREAKING** Rename all query response types to drop `Query` prefix
  (`QueryBudgetUsageCompleted` → `BudgetUsageResult`, 10 types across 4 queries)
- **BREAKING** Rename completed/failed variants to match command names
  (`ForecastFetched` → `FetchForecastCompleted`, 5 types)
- **BREAKING** Rename `MqttState` actor → `MqttStatePublisher`
  (`IMqttState` → `IMqttStatePublisher`)
- **BREAKING** Rename `SchedulerState` → `PollSchedulerState`
  (+ extensions, references)
- Add missing `Failed` variants to 3 response hierarchies
  (`TriggerImmediatePoll`, `BudgetUsage`, `PollStates`)
- Add missing abstract response bases for `FetchForecast` and
  `RequestPipelineOutput`
- Dissolve `BudgetTrackerDtoMapping` and `SchedulerDtoMapping` — inline
  conversion logic into StateExtensions per FunkArr pattern
- Fix actor logging: `private readonly ILoggingAdapter _log = Context.GetLogger()`
  inline init across all 8 actors (currently `null!` + PreStart)
- Split multi-record message files (`MqttMessages.cs`, `SensorHubMessages.cs`)
  into one file per command/query
- Fix AGENTS.md: Pattern B → Pattern A, add logging convention

## Non-goals

- No runtime behavior changes — message semantics stay identical
- No polling changes (no API-budget impact)
- `FetchForecastFailed` keeps `PollFailureReason` instead of `Exception Cause`
  — domain-typed failure is a deliberate design choice
- `SubscribeInbound`'s `IActorRef` parameter is kept — it's a subscription
  pattern where the subscriber identity must be explicit

## Capabilities

### New Capabilities

- `actor-logging-convention`: Inline readonly `_log` field initialization
  across all actors
- `message-naming-convention`: FunkArr VerbNoun/QueryNoun naming for all
  message types and response hierarchies
- `dto-mapping-dissolution`: Dissolve separate mapping classes into
  StateExtensions
- `actor-state-naming`: Align actor and state record names with FunkArr
  conventions

### Modified Capabilities

- `message-conventions`: Response types renamed to match FunkArr pattern
- `message-naming-consistency`: Query responses drop `Query` prefix

## Impact

- `Njord.Messages/` — all response type renames, file splits
- `Njord.Pipeline/` — `SchedulerState` rename, `SchedulerDtoMapping` dissolved
- `Njord.Budget/` — `BudgetTrackerDtoMapping` dissolved
- `Njord.Mqtt/` — `MqttState` actor rename, message file split
- `Njord.SensorHub/` — message file split
- `Njord.Core/Actors/ActorKeys.cs` — `IMqttState` → `IMqttStatePublisher`
- `Njord.Grpc/`, `Njord.OpenMeteo/`, all test projects — reference updates
- All 8 actor files — logging field change
- `AGENTS.md` — documentation updates
