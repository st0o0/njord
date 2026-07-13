## Context

njord's bootstrap lives entirely in `Program.cs` (74 lines): options binding,
DI registrations, Akka system configuration with inline HOCON, and actor
creation via `.WithActors`. Four actors (`MqttEgressActor`, `PipelineActor`,
`SchedulerActor`, `EnrichmentActor`) each inject `ActorRegistry` and call
`_registry.Get<T>()` to resolve peers. `ForecastHistoryActor` is created
manually via `Props.Create(() => new ...)` without DI.

Servus.Core (`Servus` v0.34.0) provides the `SetupContainer` pattern for
modular, testable bootstrap. Servus.Akka (v0.3.13) adds
`ActorSystemSetupContainer`, `WithResolvableActors`, and typed actor
resolution extensions. Both packages are used in the team's
Akka.Streams.Http project.

## Goals / Non-Goals

**Goals:**
- Modularize `Program.cs` into composable `SetupContainer` implementations
- Replace verbose actor registration with `WithResolvableActors`
- Replace raw `ActorRegistry` lookups with typed `GetActor<T>()` extensions
- Create DI-aware child actors via `ResolveChildActor<T>()` instead of
  manual `Props.Create`
- Maintain exact same runtime behavior — pure refactoring

**Non-Goals:**
- Changing actor behavior, message protocols, or stream topology
- Adding new actors, consumers, or features
- Migrating tests to Akka.TestKit (separate change)
- PostgreSQL persistence (separate change)
- Using `ActorRef<T>` DI wrapper (too heavy for this codebase size)
- Using `AppBuilder.Create().Build().Run()` — njord uses
  `WebApplication.CreateBuilder` for the health endpoint; the setup
  containers plug into that builder, they don't replace it

## Decisions

### D1: Three SetupContainers, no AppBuilder

**Decision:** Create three `IServiceSetupContainer` implementations that
are called explicitly from `Program.cs`, rather than using `AppBuilder`:

1. `NjordServiceSetup` — options binding, `TimeProvider`, `ParameterRegistry`,
   ingest/egress DI registrations
2. `NjordActorSystemSetup` — extends `ActorSystemSetupContainer`, owns the
   `AddAkka` call with HOCON configuration and `WithResolvableActors`
3. Health checks stay inline (one line, not worth a container)

**Why not AppBuilder:** njord uses `WebApplication.CreateBuilder` for the
`/healthz` endpoint. `AppBuilder` expects to own the host lifecycle and
uses `IHostBuilder` / `IHostApplicationBuilder`, which would fight with the
existing `WebApplicationBuilder`. Calling `.SetupServices(services, config)`
on each container from `Program.cs` is simpler and preserves the current
host setup.

**Alternatives rejected:**
- Single monolithic container — defeats the modularity purpose
- Per-concern containers (one for options, one for ingest, one for egress) —
  too granular for 4 registrations; the two-container split (services vs
  actor system) matches the natural boundary

### D2: WithResolvableActors for top-level actors

**Decision:** Replace the `.WithActors` callback with
`.WithResolvableActors`:

```csharp
// Before
.WithActors((system, registry, resolver) =>
{
    registry.Register<MqttEgressActor>(
        system.ActorOf(resolver.Props<MqttEgressActor>(), "mqtt-egress"));
    // ... repeat for each actor
})

// After (in NjordActorSystemSetup.BuildSystem)
builder.WithResolvableActors(r =>
{
    r.Register<MqttEgressActor>("mqtt-egress");
    r.Register<PipelineActor>("pipeline");
    r.Register<SchedulerActor>("scheduler");
    r.Register<EnrichmentActor>("enrichment");
});
```

**Why:** Eliminates the `system.ActorOf(resolver.Props<T>(), name)` boilerplate.
`WithResolvableActors` handles DI-aware `Props`, `ActorOf`, and registry
registration in one call.

### D3: GetActor<T> for peer resolution

**Decision:** Replace `_registry.Get<T>()` with `Context.GetActor<T>()` (or
`Context.System.GetActor<T>()`) from Servus.Akka's `RegistryExtensions`.

Affected actors and their lookups:

| Actor | Current | After |
|-------|---------|-------|
| `PipelineActor` | `_registry.Get<SchedulerActor>()` | `Context.GetActor<SchedulerActor>()` |
| `MqttEgressActor` | `_registry.Get<PipelineActor>()` (2×) | `Context.GetActor<PipelineActor>()` |
| `EnrichmentActor` | `_registry.Get<PipelineActor>()` (2×), `_registry.Get<MqttEgressActor>()` (2×) | `Context.GetActor<T>()` |
| `SchedulerActor` | no `_registry` usage in message handlers | Remove `_registry` field |

**Why:** Drops the `ActorRegistry` constructor parameter and field from each
actor. `GetActor<T>()` resolves from the `ActorRegistry` attached to the
`ActorSystem` — same underlying mechanism, less boilerplate.

### D4: ResolveChildActor for ForecastHistoryActor

**Decision:** Replace the manual `Props.Create(() => new ForecastHistoryActor(...))`
in `EnrichmentActor.MaterializeHistoryConsumer` with
`Context.ResolveChildActor<ForecastHistoryActor>(name, args)`.

**Why:** `ForecastHistoryActor` currently bypasses DI entirely — its
constructor takes raw values (`string location`, `HistoryOptions`,
`ResolvedParameterSet`). Using `ResolveChildActor` routes through the
`DependencyResolver`, making DI services available if the actor ever needs
them (e.g. `ILogger`, `TimeProvider`). The location-specific arguments
pass as constructor args after the DI-resolved ones.

This requires adjusting `ForecastHistoryActor`'s constructor to put
DI-resolvable parameters first and location-specific ones at the end
(matching `DependencyResolver.Props<T>(args)` convention).

### D5: Existing DI extension methods become implementation details

**Decision:** `AddOpenMeteoIngest()` and `AddMqttEgress()` remain as
extension methods but are called from inside `NjordServiceSetup.SetupServices`
rather than from `Program.cs` directly.

**Why:** Keeps the existing granular extension methods (useful for tests that
wire up only ingest or only egress), but routes the standard app startup
through the container. No public API change — the methods stay `public static`.

## Risks / Trade-offs

- **[Package dependency]** Two new NuGet packages (`Servus` 0.34.0,
  `Servus.Akka` 0.3.13) — these are team-maintained packages with stable
  APIs. → Mitigated by pinning versions in `Directory.Packages.props`.

- **[ForecastHistoryActor constructor change]** Reordering constructor
  parameters to put DI-resolved services first could break tests that
  construct the actor manually. → Mitigated by updating test constructors
  in the same change; no external consumers.

- **[SetupContainer without AppBuilder]** Calling `.SetupServices()` manually
  means we don't get `AppBuilder`'s startup-gate orchestration or automatic
  container discovery. → Acceptable: njord has no startup gates and only
  two containers; the explicit calls in `Program.cs` are clearer.
