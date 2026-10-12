## MODIFIED Requirements

### Requirement: The PollScheduler sends FetchForecast to OpenMeteoGateway
The `PollScheduler` (renamed from `PollSchedulerActor`) SHALL resolve the `OpenMeteoGateway` reference via `Context.GetActor<IForecastGateway>()` in the constructor. The actor SHALL call `Context.Watch` on the gateway ref. When a `ScheduledPoll` fires, the actor SHALL send `FetchForecast(location, modelId)` to the gateway via Tell. The actor SHALL NOT build `WeightedTarget` — the gateway computes API call weight internally. The actor SHALL NOT resolve or reference `IForecastPipeline` — it communicates only with the gateway.

The actor SHALL have at most two Become phases: `WaitingForGateway` (resolving gateway ref with backoff) and `Ready`. On `Terminated` for the gateway, the actor SHALL re-resolve with dead-ref detection and exponential backoff, transitioning to `WaitingForGateway`. Poll timers already scheduled SHALL continue to fire; their `ScheduledPoll` messages SHALL be stashed until the actor is `Ready` again.

#### Scenario: Scheduler sends FetchForecast to gateway
- **WHEN** a ScheduleOnce timer fires for (borken, icon_d2)
- **THEN** the actor sends `FetchForecast("borken", "icon_d2")` to the OpenMeteoGateway via Tell

#### Scenario: No WeightedTarget constructed
- **WHEN** the PollScheduler creates a poll target
- **THEN** it SHALL NOT construct a `WeightedTarget` — it sends only location and model ID

#### Scenario: Two Become phases only
- **WHEN** the PollScheduler starts
- **THEN** it transitions through at most `WaitingForGateway` → `Ready`

#### Scenario: Gateway terminated triggers re-resolve
- **WHEN** the OpenMeteoGateway terminates
- **THEN** the actor transitions to `WaitingForGateway` and re-resolves with backoff

#### Scenario: ScheduledPoll stashed during WaitingForGateway
- **WHEN** a `ScheduledPoll` arrives while the actor is in `WaitingForGateway`
- **THEN** it is stashed and replayed on transition to `Ready`

### Requirement: Hash results from the gateway update the schedule
The PollScheduler SHALL handle `ForecastFetched(Location, ModelId, Hash)` messages from the gateway. On receipt, the actor SHALL compare the hash with `lastHash`. If changed: persist a `DataChanged` event, update `lastChangeUtc`/`prevChangeUtc`, reset `missCount`, and schedule the next poll. If unchanged: increment `missCount` and schedule retry.

The PollScheduler SHALL handle `ForecastFetchFailed(Location, ModelId, Reason, Detail)` messages from the gateway and route to reason-specific retry logic (transient failure backoff, rate-limit floor delay).

#### Scenario: Changed hash triggers persist and reschedule
- **WHEN** a `ForecastFetched` arrives with a hash different from `lastHash`
- **THEN** a `DataChanged` event is persisted, the state is updated, and the next poll is scheduled

#### Scenario: Unchanged hash increments miss count
- **WHEN** a `ForecastFetched` arrives with a hash equal to `lastHash`
- **THEN** `missCount` is incremented and retry is scheduled with backoff

#### Scenario: ForecastFetchFailed triggers reason-based retry
- **WHEN** a `ForecastFetchFailed(Transport)` is received from the gateway
- **THEN** the scheduler increments transient failure count and schedules a backoff retry

### Requirement: QueryPollStates is handled in all behaviors
The PollScheduler SHALL handle `QueryPollStates` messages in ALL behaviors (`WaitingForGateway` and `Ready`) by responding immediately with a `QueryPollStatesCompleted` of the current `SchedulerState.States` dictionary. The handler SHALL NOT stash, delay, or drop the message in any state.

#### Scenario: Query returns current state during gateway resolution
- **WHEN** a `QueryPollStates` message is received while the actor is in `WaitingForGateway`
- **THEN** the actor SHALL respond with `QueryPollStatesCompleted` (which may be empty if no states are initialized yet)

#### Scenario: Query returns current state in Ready
- **WHEN** a `QueryPollStates` message is received in Ready state with 6 model poll states
- **THEN** the actor SHALL respond with `QueryPollStatesCompleted` containing 6 entries

## REMOVED Requirements

### Requirement: The PollSchedulerActor sends ScheduledPoll via Tell to PipelineActor
**Reason**: PollScheduler now communicates with OpenMeteoGateway instead of PipelineActor. The scheduler no longer needs a reference to the pipeline — it sends `FetchForecast` to the gateway and receives `ForecastFetched`/`ForecastFetchFailed` feedback.
**Migration**: Replace `GetActorAsync<IPipelineActor>()` with `Context.GetActor<IForecastGateway>()`. Replace `WeightedTarget` Tell with `FetchForecast` Tell. Replace `HashResult`/`FetchFailed` handlers with `ForecastFetched`/`ForecastFetchFailed` handlers.

### Requirement: Timer fires offer a target into the local queue
**Reason**: Removed from poll-scheduler spec. The SchedulerActor no longer maintains a local queue or builds WeightedTarget. It sends FetchForecast messages directly to the gateway.
**Migration**: `ScheduledPoll` handler sends `FetchForecast(location, modelId)` to the gateway instead of offering `WeightedTarget` into a queue.
