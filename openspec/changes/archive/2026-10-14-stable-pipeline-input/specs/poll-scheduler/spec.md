## MODIFIED Requirements

### Requirement: PollScheduler sends FetchForecast to the gateway
`PollScheduler` (src/Njord.Pipeline/PollScheduler.cs, a `ReceivePersistentActor` with persistence id `scheduler`, marker `IPollScheduler`) SHALL resolve the gateway with `Context.GetActor<IForecastGateway>()` in its constructor and watch it. When a `ScheduledPoll(LocationId, ModelId)` fires it SHALL ask the gateway with `FetchForecast(location, modelId)` and a fetch reply timeout (120 s by default) that covers the gateway's queue, throttle admission and HTTP timeout, and SHALL pipe the reply to itself; an Ask that fails or times out SHALL arrive as `FetchForecastFailed(location, modelId, Transport, detail)`. Every scheduled poll therefore ends in exactly one reply handled by the scheduler. It SHALL NOT build a weighted target, own a queue or reference `IForecastPipeline`.

The actor SHALL have two behaviors: `Ready` (the initial behavior) and `WaitingForGateway`. On `Terminated` for the gateway it SHALL enter `WaitingForGateway` and re-resolve with `GetActorAsync<IForecastGateway>()`; a resolved ref equal to the terminated one SHALL be treated as dead and retried with jittered exponential backoff (1 s up to 30 s). While waiting, `ScheduledPoll` and every other message not explicitly handled SHALL be stashed and replayed on the transition back to `Ready`, after the poll states are re-initialized.

#### Scenario: Scheduler sends FetchForecast to the gateway
- **WHEN** a scheduled timer fires for (lucerne, icon_d2)
- **THEN** the actor asks the gateway with `FetchForecast(lucerne, "icon_d2")` and handles the piped reply

#### Scenario: Gateway terminated triggers re-resolve
- **WHEN** the gateway terminates
- **THEN** the actor enters `WaitingForGateway` and re-resolves with backoff

#### Scenario: ScheduledPoll stashed during WaitingForGateway
- **WHEN** a `ScheduledPoll` arrives while the actor is in `WaitingForGateway`
- **THEN** it is stashed and replayed on the transition to `Ready`

#### Scenario: Gateway that never answers
- **WHEN** the gateway does not answer a `FetchForecast` within the fetch reply timeout
- **THEN** the scheduler handles it as a `Transport` failure and schedules a backoff retry

### Requirement: Fetch feedback from the gateway updates the schedule
`PollScheduler` SHALL handle `FetchForecastCompleted(Location, ModelId, Hash)` by comparing the hash with `LastHash`. If it changed, the actor SHALL persist a `PersistedDataChanged` event, update `LastChangeUtc`/`PrevChangeUtc`, reset `MissCount` and `TransientFailureCount`, record the poll in `NjordHealthMonitor`, and schedule the next poll. If it is unchanged, the actor SHALL increment `MissCount` and schedule a retry. It SHALL NOT reply to fetch feedback. `FetchForecastFailed` and `FetchForecastRejected` SHALL be routed to reason-specific retry logic (see `failure-routing`) and count as a failed outcome of the location's poll cycle.

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** `FetchForecastCompleted` arrives with a hash different from `LastHash`
- **THEN** a `PersistedDataChanged` event is persisted, the state is updated and the next poll is scheduled

#### Scenario: Unchanged hash increments miss count
- **WHEN** `FetchForecastCompleted` arrives with a hash equal to `LastHash`
- **THEN** `MissCount` is incremented and a retry is scheduled

#### Scenario: Fetch failure triggers reason-based retry
- **WHEN** `FetchForecastFailed` with reason `Transport` arrives
- **THEN** the actor records a transient failure and schedules a backoff retry

#### Scenario: A rejection counts as a failed cycle outcome
- **WHEN** every model of a location has replied and one of the replies was `FetchForecastRejected`
- **THEN** the location's completed poll cycle reports that model as failed
