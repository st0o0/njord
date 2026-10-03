## Context

See proposal.md. Facts from the current tree (read-only survey; stage 1 not yet applied, so file locations of moved types are assumptions):

- `src/Njord/Njord.csproj` is `Sdk.Web`, references Grpc.AspNetCore, prometheus-net.AspNetCore, Akka.*, MQTTnet, Servus, and compiles `protos/njord/v2/*.proto` (`GrpcServices="Server"`, `ProtoRoot="..\..\protos"`). Only `Grpc/` uses the generated `Njord.Grpc.V2` types.
- `Ingest/` (5 files): `IOpenMeteoClient`, `OpenMeteoClient`, `OpenMeteoDtos` (internal), `OpenMeteoJsonContext` (internal, STJ source generator), `IngestServiceCollectionExtensions` (`AddOpenMeteoIngest`). Consumers: `PipelineActor`, `SchedulerActor`, `FakeOpenMeteoClient` (Tests.Shared), `OpenMeteoClientSpec` (uses internals), `NjordServiceSetupSpec`.
- `Sensors/SensorHubActor.cs` (1 file, `ReceiveActor, IWithTimers`, reads `NjordOptions`; messages `SensorHubMessages` + `SensorReading` live in `Domain/Sensors`).
- `Grpc/` (11 files): `WeatherGrpcService`, `AdminGrpcService`, `OpsGrpcService`, `SensorGrpcService`, `EnrichmentProtoMapper`, `Forecast/EnrichmentSnapshotActor` + `*State`, `GrpcSnapshotConsumerActor`, `SnapshotMessages`. `AdminGrpcService.MapConfig/CloneOptions` are `internal static` (used by tests).
- Setup is centralized in `Configuration/NjordServiceSetup.cs` (`services.AddGrpc()`, `AddOpenMeteoIngest()`), `NjordActorSystemSetup.cs` (registers `SensorHubActor`, `GrpcSnapshotConsumerActor` via `WithResolvableActors`; `ForecastSnapshotActor`/`EnrichmentSnapshotActor` via a private `RegisterWithBackoff<T>` helper) and `NjordApplicationSetup.cs` (`MapGrpcService<...>()` x4).
- Tests: single `Njord.Tests` project (folders mirror source, incl. `Grpc/` 12 files, `Ingest/`, `Sensors/`); `Njord.Tests.Shared` references `Njord.csproj`; `NjordArchitecture.cs` loads one assembly via `typeof(Njord.Ingest.OpenMeteoClient).Assembly`.
- `Dockerfile` restores `Njord/Njord.csproj` only, then publishes it.

Target layout (stage 1 design is authoritative): `Domain <- Messages <- Core <- feature libs <- host`; feature libs reference only Core and never each other.

## Goals / Non-Goals

**Goals:** three leaf libs compile on their own, reference only Core, are registered through one `Add*`/`With*` pair each, and keep build + the full test suite green after every task.

**Non-Goals:** Pipeline/Egress/Enrichment/Mqtt extraction, behavior changes, per-lib test projects, analyzer rollout.

## Decisions

### 1. Cross-project actor access via marker keys in Core

`ActorRegistry.Get<T>()` keys on a type. Today `Njord.Grpc` keys on the actor classes of other features. After the move, `Njord.Grpc` must not reference `Njord.Pipeline/Egress/Sensors`. Exact markers needed (all in `Njord.Core`, e.g. `ActorKeys.cs`, empty marker types, FunkArr precedent `FunkArr.Core/ActorKeys.cs`):

| Marker | Used by (Grpc file) | Registered by |
|---|---|---|
| `SchedulerActorKey` | `OpsGrpcService.cs` (lines ~76, 150, 189) | host/Pipeline setup (`registry.Register<SchedulerActorKey>(supervisor)`) |
| `BudgetTrackerActorKey` | `OpsGrpcService.cs` (~46) | same |
| `EgressActorKey` | `WeatherGrpcService.cs` (~203) | same |
| `SensorHubActorKey` | `SensorGrpcService.cs` (19) | `Njord.Sensors` (`WithSensorsActors`) |

`ForecastSnapshotActor` and `EnrichmentSnapshotActor` are owned by `Njord.Grpc` itself (registered by `WithGrpcActors`), so they need no marker. Request/response messages Grpc sends (`QueryPollStates`, `TriggerImmediatePoll`, `RequestEgressSource`, `PushResult`, budget queries) must already be in `Njord.Messages` after stage 1; if any is not, that is a stage-1 gap to fix there, not here.

*Alternative:* keep class keys and reference the feature projects. Rejected: recreates the cross-feature dependency the split removes.
*Open point:* `WithResolvableActors(r => r.Register<T>(name))` (Servus) keys by `T` and builds the props from `T`. For marker-keyed registration use Akka.Hosting `WithActors` + `registry.Register<Marker>(ref)` (as the backoff helper already does), or verify whether Servus supports a separate key type. Decide in task 1.2.

### 2. Registration helpers move into the libs, wiring stays in the host

`AddNjordGrpc()` (`services.AddGrpc()`, `ConfigPersistence` if only Grpc uses it — confirm with grep; otherwise it stays in Core), `AddNjordIngest()` (renamed `AddOpenMeteoIngest`, same body, keep a one-release `[Obsolete]`-free rename; no compat shim needed, internal API), `AddNjordSensors()` (no services today; keep for symmetry only if it carries something — otherwise omit and register the actor only). `WithGrpcActors(AkkaConfigurationBuilder, ...)`/`WithSensorsActors(...)` carry the actor registrations. `MapNjordGrpc(this WebApplication)` replaces the four `MapGrpcService<>()` calls. The private `RegisterWithBackoff<TActor>` helper in `NjordActorSystemSetup` is needed by Grpc (two snapshot actors) and by later stages: extract it to `Njord.Core` as a public extension first (task 1.4), then reuse. Host setup files stay as thin shells (`NjordServiceSetup`, `NjordActorSystemSetup`, `NjordApplicationSetup`) calling the extensions; persistence config (`WithSqlPersistence`) stays in the host.

### 3. Project files

- `Njord.Grpc`: plain `Microsoft.NET.Sdk` + `<FrameworkReference Include="Microsoft.AspNetCore.App" />`; `Grpc.AspNetCore` (central version) and the `<Protobuf Include="..\..\protos\njord\v2\*.proto" GrpcServices="Server" ProtoRoot="..\..\protos" />` item move here (relative paths adjusted to `src/Njord.Grpc`). Add via `dotnet add package`, never version attributes. Namespace `Njord.Grpc` already matches; generated `Njord.Grpc.V2` stays.
- `Njord.Ingest`: plain SDK; `Microsoft.Extensions.Http` (needed for `AddHttpClient`; not currently a central package — add via `dotnet add package`) or the FrameworkReference; STJ source generator is in-box. `OpenMeteoDtos` and `OpenMeteoJsonContext` stay `internal`.
- `Njord.Sensors`: plain SDK, `Akka` via Core, `Microsoft.Extensions.Options` for `IOptions<NjordOptions>`; no AspNetCore dependency.
- Host `Njord.csproj`: add three `ProjectReference`s; remove `Grpc.AspNetCore` and `<Protobuf>`; keep `Sdk.Web`, prometheus-net, Serilog, persistence packages.
- `src/Njord.slnx`: add the three projects.
- `InternalsVisibleTo Include="Njord.Tests"` in each new csproj (tests use `OpenMeteoDtos` indirectly and `AdminGrpcService.MapConfig/CloneOptions`).

### 4. Tests: keep one test project in stage 2

FunkArr has one test project per domain lib (12). For njord keep `Njord.Tests` and its mirrored folders (`Grpc/`, `Ingest/`, `Sensors/`) unchanged. Reasons: zero spec moves (smaller, safer diff); `Njord.Tests.Shared` fixtures and Akka.Hosting.TestKit/persistence setup are shared across all slices; ArchUnit specs must load all Njord assemblies together anyway. Cost: `Njord.Tests` references all libs. Revisit per-lib test projects after stage 3 once the Pipeline/Egress/Enrichment/Mqtt specs exist (open question).

`Njord.Tests.Shared`: currently references `Njord.csproj` (for `IOpenMeteoClient`, options, `TestPersistenceConfig`). After stage 1 + 2 reference `Njord.Core` (+ `Njord.Ingest` only if `FakeOpenMeteoClient` needs Ingest-internal types — it should not: `IOpenMeteoClient` is in Core); verify, then narrow the reference.

### 5. ArchUnit per assembly

`NjordArchitecture` loads all Njord assemblies (Domain, Messages, Persistence, Core, Ingest, Sensors, Grpc, host) via a list instead of `typeof(Njord.Ingest.OpenMeteoClient).Assembly`; namespace-based rules keep working. The compiler now forbids upward/cross references, so replace the cross-zone namespace rules for these libs with assembly-reference assertions (`GetReferencedAssemblies()` of `Njord.Ingest`/`Njord.Grpc`/`Njord.Sensors` contain only Core-and-below, never each other, never `Njord`). `ProductionTypes` ("sealed", suffix rules) must select all production assemblies. Keep the rule red-first discipline (temporary violation proves each rule).

### 6. Dockerfile and CI: plan only

`Dockerfile` currently copies only `src/Njord/Njord.csproj` before `dotnet restore`. With project references restore needs every referenced csproj: add `COPY` lines for the new csprojs (and stage-1 projects) before restore, keep `COPY protos/ /protos/` before restore (the Protobuf item resolves at build/restore time). CI uses the shared `dotnet-ci.yml` on `Njord.slnx` and needs no change; verify `slnx` includes the new projects. No CI/Docker edits beyond what the Dockerfile needs to keep `docker build` working — validate with a local `docker build` if Docker is available, otherwise state it was not run.

### 7. Order inside the stage (leaf-first, each step green)

Sensors (smallest, proves the marker + registration pattern) -> Ingest (no marker, tests use internals) -> Grpc (largest, protos, markers) -> setup shells/ArchUnit/Dockerfile/docs.

## Risks / Trade-offs

- [Stage 1 gaps: a message/interface Grpc needs is still in a feature project] -> grep Grpc's `using Njord.*` after stage 1; fix in stage 1 or add the type to Core here as a prerequisite task.
- [Marker registration changes how actors are resolved; a missing registration fails at runtime, not compile time] -> keep `GrpcSnapshotConsumerTerminatedSpec`, `*GrpcServiceSpec` and `NjordServiceSetupSpec`; add one spec that builds the host and resolves every marker from `ActorRegistry`.
- [Protobuf build item path and `ProtoRoot` break after the move, silently producing no generated code] -> compile `Njord.Grpc` alone as a task checkpoint; proto package/namespace unchanged.
- [`InternalsVisibleTo` omission breaks tests that use internals] -> compile `Njord.Tests` after each project move.
- [Docker build breaks because restore sees only one csproj] -> Dockerfile task with a local `docker build` check.
- [`AddOpenMeteoIngest` rename breaks `NjordServiceSetupSpec`] -> update the spec in the same task.
- [Concurrent in-flight changes (`akka-failure-hygiene`) touch the same actors/setup] -> do not start before it is merged.

## Migration Plan

Prerequisites: `extract-core-projects` applied; `akka-failure-hygiene` merged. One commit per project move (Sensors, Ingest, Grpc) plus one for setup/arch/docs. Rollback: `git revert` per commit; no data or wire format changes.

## Open Questions

- Marker registration mechanism (Servus `Register<T>` vs Akka.Hosting `registry.Register<Marker>`): decided in task 1.2; does not change the layout.
- Per-library test projects (FunkArr style) after stage 3? Deferred; default is to keep one.
- `ConfigPersistence` is used only by `AdminGrpcService` and its DI registration (grep-verified); default: it moves to `Njord.Grpc` and `AddNjordGrpc()` registers it, unless stage 1 already placed it in Core for another consumer (confirm in task 4.1).
