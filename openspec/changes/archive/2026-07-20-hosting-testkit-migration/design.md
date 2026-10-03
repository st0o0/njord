## Context

Actor tests currently inherit from `Akka.TestKit.Xunit.TestKit` (8 classes) or `Akka.Persistence.TestKit.PersistenceTestKit` (8 classes). Both use the inherited `Sys` property but wire dependencies manually: `Options.Create(...)`, `NullLogger.Instance`, `ActorRegistry.For(Sys)`, `Props.Create(() => new Actor(...))`. The `SchedulerActorSpec` contains a 270-line `TestableSchedulerActor` that duplicates the production actor with test-friendly modifications (configurable PersistenceId, capped scheduler delays). This clone already diverges — it omits `ScheduleNext` for `ModelUnavailable`/`MalformedPayload` failure reasons that the production actor now handles.

## Goals / Non-Goals

**Goals:**
- Actor tests use a real DI container matching production wiring
- The production `SchedulerActor` is tested directly, not a copy
- Test boilerplate (Options.Create, NullLogger, manual registry) is eliminated
- Tests remain fast, deterministic, and CI-friendly

**Non-Goals:**
- Changing pure unit tests (no ActorSystem)
- Consolidating fake actors across test classes
- Adding new test coverage
- Changing the `HealthEndpointSpec` (WebApplicationFactory)

## Decisions

### Decision 1: Use `Akka.Hosting.TestKit` as the primary actor test base class

`Akka.Hosting.TestKit` extends `TestKit` and adds:
- `ConfigureServices(HostBuilderContext, IServiceCollection)` — register DI services
- `ConfigureAkka(AkkaConfigurationBuilder, IServiceProvider)` — configure ActorSystem with hosting APIs

Test classes override these methods to register the actor under test with real DI, substituting only external dependencies (TimeProvider, collaborator actors).

**Why not stay on raw TestKit?** The manual wiring duplicates what the hosting layer does in production, creating divergence risk (the SchedulerActor clone is proof). Hosting.TestKit keeps test wiring structurally identical to production startup.

**Why not `Akka.Hosting.TestKit.Xunit2`?** The project uses xUnit v3. The `Akka.Hosting.TestKit` base class is framework-agnostic — it doesn't depend on a specific xUnit version. The xUnit2-specific package adds a `TestKit` that inherits from `Xunit.TestKit`, but we can use the base `Akka.Hosting.TestKit.TestKit` directly.

### Decision 2: In-memory persistence via HOCON config, not PersistenceTestKit

For persistence actors that don't need interception (journal write/snapshot load failure injection), configure in-memory persistence through the hosting builder:

```csharp
protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider sp)
{
    builder
        .AddHocon(TestPersistenceConfig.InMemory, HoconAddMode.Prepend)
        .WithResolvableActors(r => r.Register<SchedulerActor>("scheduler"));
}
```

Where `TestPersistenceConfig.InMemory` is a shared HOCON snippet configuring `akka.persistence.journal.plugin` and `akka.persistence.snapshot-store.plugin` to in-memory implementations.

**Exception:** `ForecastSnapshotRecoverySpec` and `EnrichmentSnapshotRecoverySpec` stay on `PersistenceTestKit` — they use `WithSnapshotLoad(load => load.Fail())` which requires PersistenceTestKit's intercepting store.

### Decision 3: Delete TestableSchedulerActor, use short DiscoveryInterval

The clone exists because:
1. **Configurable PersistenceId** — not needed with Hosting.TestKit (each test class gets its own ActorSystem with in-memory journal, so `"scheduler"` doesn't collide)
2. **Capped scheduler delay** — the real actor uses `Context.System.Scheduler.ScheduleTellOnceCancelable(state.NextPollUtc - now, ...)`. In tests, set `DiscoveryInterval = TimeSpan.FromMilliseconds(50)` so computed delays are naturally short
3. **Omitted dependencies** (ILogger, NjordHealthState) — with DI, these are injected normally (NullLogger, real NjordHealthState instance)

### Decision 4: Fake collaborator actors stay as TestProbe or private nested classes

Collaborator actors (FakePipelineSource, FakeEgressSinkProvider, etc.) remain as private nested classes in each test. They are registered in the ActorRegistry with `overwrite: true` in the test's `ConfigureAkka` override or in test setup methods. This is the standard Akka.Hosting.TestKit pattern — real actor for the subject, fakes for collaborators.

### Decision 5: Shared in-memory persistence config in Njord.Tests.Shared

A small static class `TestPersistenceConfig` provides the HOCON config for in-memory journal and snapshot store. This avoids duplicating the HOCON across every persistence actor test class.

## Risks / Trade-offs

- **[xUnit v3 compatibility]** Akka.Hosting.TestKit targets xUnit v2 conventions. The framework-agnostic base may need minor adaptation for xUnit v3's `IAsyncLifetime`. → Mitigation: Verify the base class works with xUnit v3 in the first migration (SchedulerActorSpec) before proceeding with the rest.
- **[Behavioral change from real actor]** Using the real SchedulerActor instead of the clone means tests exercise real code paths (including logging, health state updates). Some tests may need adjusted assertions. → Mitigation: This is desirable — it catches the divergence that already exists.
- **[Scheduler timing in CI]** Short `DiscoveryInterval` (50ms) means real Akka scheduler delays. On slow CI runners, these may occasionally be tight. → Mitigation: Keep `[Fact(Timeout = 5000)]` which gives 100x headroom over 50ms delays.
