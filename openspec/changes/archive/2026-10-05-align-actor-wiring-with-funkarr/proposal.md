## Why

Every njord actor that depends on another actor goes through a verbose async
init-dance: `GetActorAsync<T>()` + `PipeTo` + private resolve/fail message
records + a `Become(WaitingForX)` state that stashes everything + retry logic.
Seven actors carry this boilerplate (~20-30 LOC each, ~150 LOC total). FunkArr
solved the same problem with `WithSingleton` registration and sync
`Context.GetActor<T>()` — zero init-dance, zero extra messages. Aligning njord
removes ceremony, makes actor startup deterministic, and brings the two projects
to a shared pattern.

## What Changes

- **Host registration**: replace the single `WithActors` callback in
  `NjordActorSystemSetup` with ordered `WithSingleton` calls (and
  `WithShardRegion` for `ForecastHistoryRegion`). Registration order follows
  the verified dependency tiers so every `Context.GetActor<T>()` call in a
  constructor finds its target already registered. The `Scheduler ↔ Pipeline`
  circular dependency is broken by `SchedulerActor`'s existing
  `BackoffSupervisor` wrapper (supervisor ref registered immediately, child
  constructed later).
- **Actor init simplification**: remove `GetActorAsync` + `PipeTo` +
  `WaitingForX` / `Initializing` Become-states from `SchedulerActor`,
  `PipelineActor`, `ModelStateActor`, `EnrichmentActor`,
  `GrpcSnapshotConsumerActor`, `MqttDiscoveryActor`, `MqttStateActor`.
  Replace with sync `Context.GetActor<T>()` in the constructor (or field
  initializer). Remove the private `XxxResolved` / `XxxResolveFailed` message
  records that were only needed for the async path.
- **`StreamConsumerActor` init path**: simplify the template-method base class.
  `ResolveDependencies()` becomes a sync call that returns refs directly.
  The `Become` / `Stash` machinery stays for **runtime recovery**
  (`Terminated` → re-resolve → re-materialize) but the initial startup path
  is now sync and stash-free.
- **`RetryBackoff` → Servus `BackoffPolicy`**: delete
  `Njord.Core/Actors/RetryBackoff.cs` and replace its two call-sites
  (`SchedulerActor`, `StreamConsumerActor`) with
  `Servus.Resilience.Backoff.Create(...)`. Gains configurable jitter
  (`DelayWithJitter`) for free — prevents thundering-herd on simultaneous
  actor re-resolve after a crash.
- **Test config cleanup**: remove `TestRetryBackoffConfig` (the HOCON override
  for `RetryBackoff`) and replace with a fast `BackoffPolicy` in test helpers.

## Non-goals

- Changing the runtime recovery path of `StreamConsumerActor` (Terminated →
  re-resolve stays async with Become/Stash — the dependency might not be
  re-registered yet after a crash).
- Altering actor supervision strategies or BackoffSupervisor parameters.
- Changing which actors are behind BackoffSupervisor (Scheduler, BudgetTracker,
  ForecastSnapshot, EnrichmentSnapshot stay wrapped; others stay plain).
- Modifying polling behaviour, MQTT egress, or any functional actor logic.

## Capabilities

### New Capabilities

_None — this is a structural refactor with no new runtime capabilities._

### Modified Capabilities

- `stream-consumer-actor`: init path changes from async (ResolveDependencies +
  PipeTo + Become) to sync (constructor-time resolution). Runtime recovery path
  unchanged.
- `actor-resolve-failure-handling`: init-time resolve failures become
  constructor exceptions (actor dies, BackoffSupervisor restarts) instead of
  handled failure messages. Runtime re-resolve after Terminated still uses the
  existing async retry.

## Impact

- **Code**: ~150 LOC removed across 7 actors + base class + `RetryBackoff`.
  `NjordActorSystemSetup` rewritten from `WithActors` to `WithSingleton`.
- **Dependencies**: no new packages. `Servus.Resilience` is already transitively
  available via `Servus.Akka`.
- **Tests**: actor specs that register dependencies via `ActorRegistry` keep
  working — they already pre-register refs before creating the actor under test.
  `TestRetryBackoffConfig` deleted, replaced with fast `BackoffPolicy`.
- **Risk**: low. Registration order is deterministic and tested. The only
  behavioural change is that an init-time resolve failure (registry key missing)
  now crashes the actor instead of retrying — which is correct because a missing
  registry key at startup is a wiring bug, not a transient failure.
