## Context

See proposal.md for motivation and the target layout. Verified state (read from the tree, no builds):

- One host project `src/Njord/Njord.csproj` (Sdk.Web, `InternalsVisibleTo Njord.Tests`). `Njord.Tests` and `Njord.Tests.Shared` both `ProjectReference` the host; `Directory.Build.props` is shared (net10.0, nullable, implicit usings, `<Version>`).
- Persisted objects are the `*Dto` classes in `Persistence/` (Newtonsoft `[JsonProperty]` short names). The mapping classes live in the same files: `SchedulerDtoMapping` (uses `SchedulerActor.DataChanged`, `ModelPollState`), `BudgetTrackerDtoMapping`, `ForecastSnapshotMapping`, `EnrichmentSnapshotMapping` (type map over `Domain.Analysis` results, `TypeNameHandling.None`, short `GetType().Name`), `ForecastHistoryDtoMapping`.
- `NjordActorSystemSetup` configures no custom Akka serializer; test persistence is in-memory (`TestPersistenceConfig`).
- Cross-cutting messages are already top-level records, except `BudgetResponse`/`BudgetUsageResult`/`BudgetResponseFailed` (top of `BudgetTrackerActor.cs`), the nested `BudgetTrackerActor.RecordApiCall`/`QueryBudgetUsage`, `PushResult` (end of `SensorHubActor.cs`) and `PollPhase` (top of `ModelPollState.cs`). `SensorHubMessages.cs` sits in `Domain/Sensors`.
- `Dockerfile` restores `Njord/Njord.csproj` after copying only that csproj; CI builds `Njord.slnx`.
- `ArchUnit` specs (`src/Njord.Tests/Architecture`) load `typeof(OpenMeteoClient).Assembly` plus the tests assembly and select zones by namespace regex.

## Goals / Non-Goals

**Goals:** a compiling, test-green bottom layer (`Domain`, `Persistence`, `Messages`, `Core`) that stages 2/3 can reference; no cycle left among the four; persisted data stays readable.

**Non-Goals:** extracting any feature library; changing message shapes; renaming wire names; CI redesign.

## Decisions

### 1. Persistence mapping moves out, DTOs stay pure

The DTO classes carry no domain reference once the mapping is removed, so `Njord.Persistence` has no project references. Mapping classes move to the code that owns the aggregate: `SchedulerDtoMapping` + `BudgetTrackerDtoMapping` → `src/Njord/Pipeline/`, `ForecastSnapshotMapping` + `EnrichmentSnapshotMapping` → `src/Njord/Grpc/` (consumers: `ForecastSnapshotActor`, `EnrichmentSnapshotActor`, states), `ForecastHistoryDtoMapping` → `src/Njord/Enrichment/`. Consumers are confirmed by grep in task 3.1 before moving. Mapping keeps its current namespace (`Njord.Persistence`) in stage 1 to avoid touching callers; stages 2/3 re-home it with the owning project. This matches FunkArr's `*MappingExtensions.cs` and removes `Persistence` → `Pipeline` (`ModelPollState`/`PollPhase` need not move for persistence).
*Alternative:* move `ModelPollState`/`PollPhase` to `Core` and keep mapping beside DTOs. Rejected: keeps DTOs coupled to scheduling types and diverges from FunkArr.

### 2. `LocationOptions` and four analysis option types go to `Domain`

`AlertOptions`, `HistoryOptions`, `IndexOptions`, `IndexPreferences`, `LocationIndexOverride` are consumed by `Domain.Analysis` (`AlertEvaluator`, `HistoryComputer`, `IndexComputer`, `PreferenceResolver`). `LocationOptions` is a dependency-free POCO needed by `WeightedTarget` (a `Messages` type, since `PipelineSinkResponse` carries `ISinkRef<WeightedTarget>`) and `IOpenMeteoClient`. All six are placed in `src/Njord.Domain/Options/` and keep the namespace `Njord.Configuration` in stage 1, so `NjordOptions` and every `IOptions<>` binding stay untouched. `IndexOptionsValidator` stays in `Core` (it references `Domain.Analysis`, which `Core` can see).
*Alternative:* rename the namespace to `Njord.Domain.Options`. Rejected for stage 1 (large mechanical diff, no benefit yet); listed as an open question.

### 3. `Messages` namespaces

Moving messages into one assembly while keeping `Njord.Egress`/`Njord.Pipeline`/`Njord.Grpc`/`Njord.Enrichment` namespaces would split those namespaces across assemblies and blur the ArchUnit namespace zones. Messages therefore get `Njord.Messages.{Egress,Pipeline,Sensors,Snapshots,Common}` (`Ack` → `Common`). Cost: a mechanical `using` update in consumers; benefit: assembly and namespace align and later zone rules can be assembly-based. `Domain`, `Persistence`, `Core` (Configuration/Diagnostics/Health) keep existing namespaces: they already align with, or are neutral to, their new assembly. `StreamSupervision` moves from `Njord.Pipeline` to `Njord.Actors` next to `StreamConsumerActor` (it is Akka plumbing, not pipeline logic).
*Alternative:* keep all namespaces. Rejected for `Messages` only, as above.

### 4. Persisted-data break is accepted (0.x)

Moving DTO types to a new assembly changes their assembly-qualified name; Akka.NET's default persistence serializer may record `Namespace.Type, Assembly`, so rows written by the current build may become unreadable. Decision (user, 2026-10-02): njord is 0.x, so this is an accepted breaking change. No `TypeForwardedTo` shims, no golden-payload fixtures, no migration code. The commit is `refactor!:` with a `BREAKING CHANGE:` footer stating that persisted scheduler/budget/snapshot data may need to be reset. `[JsonProperty]` names and `Version` stay unchanged, so the extend-only rule for property names keeps holding; AGENTS.md gets a short 0.x note next to the persistence rule (task in group 7). Revisit once the project reaches 1.0.

### 5. Stage-1 scope boundaries

Only types that are needed below the line move. Types shared across features but entangled with the later descriptor refactor (`IEnrichmentFeature` and its three sub-interfaces, `MqttMessage`, `DiscoveryContext`, `TopicScheme`, payload builders, `TopicSlug`, `HorizonProjection`, `ForecastHistoryMessages`) stay in the host. `IBudgetProvider`/`IBudgetGate`/`BudgetThrottleStage`, `ModelPollState`, all actors and the three `*Setup.cs` stay too. The actor marker interfaces are NOT deferred: they are created in stage 1 (Decision 6) so stages 2 and 3 only move code.

### 6. FunkArr-style actor marker interfaces (no `IRequiredActor<T>`, no class keys)

Today every cross-feature actor lookup is keyed by the concrete actor class (`registry.Get<SchedulerActor>()`, `Context.GetActorAsync<PipelineActor>()`, ~70 registrations/lookups incl. tests), which would force `Njord.Grpc` etc. to reference `Njord.Pipeline`. FunkArr solves this with empty marker interfaces in `FunkArr.Core/ActorKeys.cs` (`public interface IScoringManager;`), registers every actor under its marker in one host-side Akka setup (`AkkaSetupContainer`, grouped per domain in private methods) and resolves with `Context.GetActor<IMarker>()` / `registry.GetAsync<IMarker>()`; actor classes never implement the marker and never take `IActorRef` constructor parameters. njord adopts exactly that (user decision, 2026-10-02: not `IRequiredActor<T>`):

- `src/Njord.Core/Actors/ActorKeys.cs`: one marker per registry-registered actor, named `I<ActorClass>` — `ISchedulerActor`, `IBudgetTrackerActor`, `IPipelineActor`, `IEgressActor`, `IModelStateActor`, `IEnrichmentActor`, `ISensorHubActor`, `IForecastSnapshotActor`, `IEnrichmentSnapshotActor`, `IGrpcSnapshotConsumerActor`, `IMqttConnectionActor`, `IMqttEgressActor`, `IDiscoveryActor`. (Derived from the registrations in `NjordActorSystemSetup.cs`; every actor resolved at runtime has one, FunkArr's rule.)
- Registration stays central in the host `NjordActorSystemSetup` (as FunkArr's `AkkaSetupContainer`), grouped per domain (`RegisterPipelineActors`, `RegisterEgressActors`, `RegisterMqttActors`, ...): `registry.Register<IXxxActor>(system.ActorOf(resolver.Props<XxxActor>(), name))` through Akka.Hosting `WithActors((system, registry, resolver) => ...)`; the existing `RegisterWithBackoff<TActor>` becomes `RegisterWithBackoff<TKey, TActor>` (key = marker). Servus `WithResolvableActors(r => r.Register<T>(name))` keys by `T` and is therefore replaced for all keyed actors (task 6.0 verifies whether Servus offers a separate key type; if it does it may be used, behavior and actor names stay identical).
- Spike result (task 6.0, `servus.akka` 0.3.14, reflection over the assembly): `ActorRegistrationHelper.Register<TActor>(name, args)` and `WithResolvableActor<TActor>` key the registry by the actor type only; there is no separate key type. Hence Akka.Hosting `WithActors` + `registry.Register<IMarker>(ref)` is used everywhere (host and tests). The markers live in `src/Njord.Core/Actors/ActorKeys.cs` (`Njord.Actors`); `Njord.Core` needs no extra package for them.
- Resolution: Servus `Context.GetActorAsync<IXxxActor>()` and `ActorRegistry.Get<IXxxActor>()` (both already in use, they work with any registry key). No new package: `Servus.Akka` and `Akka.Hosting` are already referenced and move into `Njord.Core`'s package set, so feature libraries inherit them as `FunkArr.Core` does.
- Actor classes stay in their folders for now; they must be `public` once they move so the host can call `resolver.Props<T>()` (stages 2/3).
- *Alternatives rejected:* `IRequiredActor<T>` (Akka.Hosting DI type keyed by the class again, needs the class assembly reference); marker *classes* (`SchedulerActorKey`) instead of interfaces (not the FunkArr pattern); per-library `WithXActors()` registration (FunkArr registers centrally in the host; the host references every library anyway).

### 7. Build wiring

- All new projects use `Microsoft.NET.Sdk` (library); the host keeps `Sdk.Web` and the `Protobuf` item (protos move with `Grpc` in stage 2).
- Packages are added per project with `dotnet add package` so versions stay only in `Directory.Packages.props`; the package set per project is derived from compile errors (`Persistence`: Newtonsoft.Json; `Messages`: Akka.Streams; `Core`: Akka, Akka.Streams, Options/DI abstractions, and a `FrameworkReference` to `Microsoft.AspNetCore.App` only if validators/health state require it).
- `InternalsVisibleTo Include="Njord.Tests"` on each new project; `Njord.Tests`/`Njord.Tests.Shared` keep referencing only the host (project references flow transitively), so test csprojs do not change.
- `Dockerfile`: copy each new csproj before `dotnet restore` (layer caching) and the project folders before `publish`; no CI file change.
- `ArchUnit`: `NjordArchitecture` loads all production assemblies; the sealed-class rule scans all of them; the zone specs keep their namespace regexes (still valid) and keep the `Timeout = 60_000` rationale (architecture load takes 6–10 s). A new spec asserts the bottom-layer reference graph (`Domain` references no Njord assembly; `Persistence` none; `Messages` only `Domain`; `Core` only those three).

## Risks / Trade-offs

- [Persisted journal/snapshot rows may be unreadable after the assembly move] → Accepted: 0.x breaking change, `refactor!:` commit with BREAKING CHANGE footer; reset persistence data on upgrade.
- [Large mechanical diff hides a behavior change] → each task is one move and leaves build and the full suite green; moves use `git mv`; Verify `.verified.*` files must not change (`git status` check).
- [Hidden `internal` usage breaks across assemblies] → `InternalsVisibleTo` per project; compile errors guide the exact set.
- [Package set in `Core` pulls the Web framework into a library] → decide in task 5.1 by compile need; prefer package references over `FrameworkReference`.
- [Messages namespace change touches many files] → mechanical; applied in a dedicated task with the build as the check.
- [Docker restore layer changes] → build the image locally if Docker is available; otherwise rely on the CI `lint`/`test` jobs and flag it.

## Migration Plan

Ship as one branch with one commit per task group; each commit builds and passes the suite. Rollback: revert the branch; persisted data is unaffected because DTO property names do not change and shims keep old rows readable.

## Open Questions

1. Should the six moved option POCOs get the namespace `Njord.Domain.Options` later? (Deferred; no impact on tasks here.)
2. Namespace for `StreamSupervision`/`StreamConsumerActor` in `Core`: `Njord.Actors` (chosen) vs a new `Njord.Streams`.
