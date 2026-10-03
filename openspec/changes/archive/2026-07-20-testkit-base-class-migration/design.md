## Context

10 actor/stream specs use raw `ActorSystem.Create` with manual lifecycle
management. The `Akka.TestKit.Xunit.TestKit` base class (already available
transitively via `Akka.Persistence.TestKit.Xunit` 1.5.70) provides `Sys`,
`CreateTestProbe()`, TestKit assertions, and automatic shutdown — all for free
via inheritance.

The previous change (`deterministic-testkit-tests`) already migrated assertion
patterns. Three of those specs (ModelStateActorSpec, MqttConnectionActorSpec,
DiscoveryActorSpec) introduced `IAsyncDisposable` + HOCON shutdown-timeout
workarounds to fix the 10-second coordinated-shutdown penalty. This change
eliminates those workarounds by using TestKit's built-in `Shutdown()`.

## Goals / Non-Goals

**Goals:**
- Every actor/stream spec inherits from `TestKit` or `PersistenceTestKit` —
  no raw `ActorSystem.Create`.
- Remove all manual `IDisposable`/`IAsyncDisposable` implementations,
  HOCON shutdown-timeout overrides, and `ActorSystem.Create` calls.
- `CreateTestProbe()` available in every spec.

**Non-Goals:**
- Rewriting existing assertions or test logic (MessageCollector stays).
- Downgrading the 4 PersistenceTestKit specs that don't strictly need
  persistence (SinkRefConnectionSpec, BudgetThrottleStageSpec,
  PipelineConnectionSpec, BudgetThrottleStageSpec) — harmless overhead.
- Changing production code.

## Decisions

### TestKit vs PersistenceTestKit per spec

Specs that create or interact with `ReceivePersistentActor` subclasses need
the in-memory journal from `PersistenceTestKit`. All others use plain `TestKit`.

| Spec | Target base | Reason |
|------|-------------|--------|
| ModelStateActorSpec | TestKit | No persistence |
| EgressActorSpec | TestKit | No persistence |
| MqttConnectionActorSpec | TestKit | No persistence |
| DiscoveryActorSpec | TestKit | No persistence |
| PollPipelineSpec | TestKit | No persistence |
| ForecastGrpcServiceSpec | TestKit | No persistence |
| ConfigGrpcServiceSpec | TestKit | No persistence |
| EnrichmentActorSpec | TestKit | No persistence |
| ForecastSnapshotActorSpec | PersistenceTestKit | Creates persistent snapshot actors |
| EnrichmentSnapshotActorSpec | PersistenceTestKit | Creates persistent snapshot actors |

### Mechanical transformation pattern

Each spec follows the same pattern:

```
Before:
  public sealed class FooSpec : IDisposable          // or IAsyncDisposable
  {
      private readonly ActorSystem _system = ActorSystem.Create("foo-spec", ...);
      public void Dispose() => _system.Dispose();    // or DisposeAsync

After (TestKit):
  public sealed class FooSpec : Akka.TestKit.Xunit.TestKit
  {
      // Sys is provided by TestKit base class
      // Shutdown is handled automatically
```

All `_system` references become `Sys`. No constructor needed for default
config. If a spec needs custom HOCON, pass it via `base(config)`.

### Package reference

Add `Akka.TestKit.Xunit` as an explicit PackageReference (version 1.5.70,
matching the existing Akka packages). It's already available transitively but
an explicit reference makes the dependency visible and protects against
transitive removal.

### Sealed classes with inheritance

`TestKit` is not sealed, so `sealed class FooSpec : TestKit` works.
`PersistenceTestKit` is also not sealed.

## Risks / Trade-offs

**[TestKit uses actor system name "test" by default]** → This is fine. Each
xUnit test instance gets its own TestKit/ActorSystem. The `ActorRegistry` is
per-system, so registrations don't leak between tests.

**[TestKit.Dispose calls Shutdown which waits for termination]** → TestKit's
`Shutdown()` has a configurable timeout (default 5s) and does not use
coordinated shutdown's 10-second `actor-system-terminate` phase. This is
actually faster than the current workaround.

**[ConfigGrpcServiceSpec uses Guid-based system names]** → TestKit defaults to
"test". If unique names are needed for parallel execution, pass a custom name
via `base(actorSystemName: ...)`.
