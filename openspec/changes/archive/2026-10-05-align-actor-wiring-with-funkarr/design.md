## Context

njord registers all actors in a single `WithActors` callback in
`NjordActorSystemSetup`. Because `system.ActorOf()` runs the constructor and
`PreStart` **synchronously** before returning, actors that depend on other
actors cannot use sync `Context.GetActor<T>()` — the dependency may not be
registered yet. This forces an async init-dance:
`GetActorAsync` → `PipeTo` → `Become(WaitingForX)` → stash → resolve →
`Become(Ready)` → unstash.

FunkArr avoids this by using `WithSingleton` (from Akka.Cluster.Hosting),
which registers each actor ref in the `ActorRegistry` before the next
registration runs. Combined with `Context.GetActor<T>()` (from Servus.Akka),
actors resolve their dependencies synchronously in the constructor.

Exploratory tests confirmed:
- `system.ActorOf()` runs constructors AND `PreStart` synchronously.
- `BackoffSupervisor` breaks circular dependencies: the supervisor ref is
  registered immediately, but the child starts later on the dispatcher.
- `Context.GetActor<T>()` throws `MissingActorRegistryEntryException` if the
  key is not registered — no silent fallback.

## Goals / Non-Goals

**Goals:**
- Replace `WithActors` with ordered `WithSingleton` / `WithShardRegion` calls.
- Switch actor init from async `GetActorAsync` to sync `Context.GetActor<T>()`.
- Remove ~150 LOC of init-dance boilerplate (private message records,
  `WaitingForX` Become-states, stash/unstash).
- Replace `RetryBackoff` with `Servus.Resilience.BackoffPolicy` (adds jitter).
- Simplify `StreamConsumerActor` init path while keeping runtime recovery.

**Non-Goals:**
- Changing runtime recovery behaviour (Terminated → async re-resolve stays).
- Modifying BackoffSupervisor configuration or which actors are wrapped.
- Altering functional actor logic (polling, MQTT, gRPC, enrichment).

## Decisions

### D1: Registration order follows the dependency tier graph

The `WithSingleton` registration order SHALL follow the verified dependency
tiers. Each actor only resolves keys that are already registered:

```
Tier 0  ShardRegion:   ForecastHistoryRegion
Tier 0  BackoffSuper:  BudgetTrackerActor
Tier 0  Plain:         SensorHubActor, MqttConnectionActor
Tier 1  BackoffSuper:  SchedulerActor           (no sync deps)
Tier 1  Plain:         PipelineActor             → ISchedulerActor (supervisor ref)
Tier 2  Plain:         ModelStateActor            → IPipelineActor
Tier 2  Plain:         EnrichmentActor            → IPipelineActor, ISensorHubActor
Tier 3  BackoffSuper:  ForecastSnapshotActor     (no deps)
Tier 3  BackoffSuper:  EnrichmentSnapshotActor   (no deps)
Tier 3  Plain:         GrpcSnapshotConsumerActor  → IModelStateActor, IEnrichmentActor
Tier 3  Plain:         MqttConnectionActor (Tier 0, already registered)
Tier 3  Plain:         MqttDiscoveryActor         → IModelStateActor, IMqttConnectionActor
Tier 3  Plain:         MqttStateActor             → IModelStateActor, IEnrichmentActor
```

**Why over alternatives:**
- Alternative: Keep `WithActors` but reorder registrations. Rejected because
  `WithActors` runs `system.ActorOf()` synchronously — the constructor runs
  before the next `registry.Register()`, so later keys aren't available.
  `WithSingleton` registers the ref _before_ the constructor runs on the
  dispatcher.

### D2: Scheduler ↔ Pipeline circular dependency broken by BackoffSupervisor

`SchedulerActor` is already behind `BackoffSupervisor`. The supervisor ref is
registered at Tier 1. `PipelineActor` resolves `ISchedulerActor` and gets the
supervisor ref. The `BackoffSupervisor` starts the `SchedulerActor` child
later (async on the dispatcher), and by that time `IPipelineActor` is
registered.

```
Timeline:
  1. WithSingleton<ISchedulerActor>  → BackoffSupervisor ref registered
     (child SchedulerActor NOT created yet)
  2. WithSingleton<IPipelineActor>   → PipelineActor.ctor() runs:
       Context.GetActor<ISchedulerActor>() → supervisor ref ✓
     → pipeline ref registered
  3. BackoffSupervisor starts SchedulerActor child (async):
       Context.GetActor<IPipelineActor>() → pipeline ref ✓
```

Tested and confirmed in `CircularActorResolutionSpec`.

**Why not break the circular dependency structurally:** The Scheduler tells
Pipeline what to fetch; Pipeline's stream graph sends results back to
Scheduler. Both directions are semantically correct. The BackoffSupervisor
indirection breaks the _init-time_ cycle without changing the runtime
protocol.

### D3: StreamConsumerActor dual-path init/recovery

The base class gets two resolution paths:
- **Init (sync):** Subclass overrides a new `ResolveInitialDependencies()`
  method that calls `Context.GetActor<T>()` and returns refs. Called in the
  constructor. No Become, no stash.
- **Recovery (async):** On `Terminated`, the existing `ResolveDependencies()`
  path fires: `GetActorAsync` + `PipeTo` + `WaitingForRefs` + stash. This
  stays because the crashed dependency might not be re-registered yet.

**Why not make recovery sync too:** After a crash, the `BackoffSupervisor`
restarts its child with delay. During that delay, the registry still holds the
old (dead) ref or the supervisor ref. `GetActorAsync` waits for the new ref
to appear; sync `GetActor` would get the dead ref and fail.

### D4: RetryBackoff → Servus BackoffPolicy

Replace `Njord.Core/Actors/RetryBackoff.cs` (hardcoded `2^attempt`, max 30s,
no jitter) with `Servus.Resilience.Backoff.Create(initialDelay: 1s,
maxDelay: 30s)` and use `DelayWithJitter(attempt)`.

Benefits:
- Jitter prevents thundering herd when multiple actors re-resolve
  simultaneously after a crash.
- No custom code to maintain.
- Test override: inject a fast `BackoffPolicy` in tests instead of the HOCON
  override (`TestRetryBackoffConfig`).

### D5: Standalone actors (Scheduler, Pipeline) lose their init-dance entirely

`SchedulerActor` and `PipelineActor` currently have their own `WaitingForX`
Become-states with `GetActorAsync` + `PipeTo` + retry. After the switch:
- Resolve dependency in constructor via `Context.GetActor<T>()`.
- Start in `Ready` directly.
- `Terminated` handler for the dependency stays — re-resolves async and
  enters a recovery state (same as today but only for runtime, not init).

## Risks / Trade-offs

- **[Registration order is load-bearing]** → Mitigated by the tier graph
  being explicit and tested. An `ActorKeyRegistrationSpec` already verifies
  all keys are registered; extend it to verify order.
- **[Init-time crash on missing key]** → Correct behaviour. A missing key at
  startup is a wiring bug. `BackoffSupervisor` handles transient startup
  ordering issues for wrapped actors.
- **[Scheduler resolves supervisor ref, not child ref]** → Same as FunkArr's
  MediathekViewWebManager pattern. `BackoffSupervisor` forwards messages to
  the child transparently. The only difference is `ActorPath` (includes
  `/scheduler-supervisor/scheduler` instead of `/scheduler`), which no code
  inspects.

## Open Questions

_None — all critical questions answered by exploratory tests._
