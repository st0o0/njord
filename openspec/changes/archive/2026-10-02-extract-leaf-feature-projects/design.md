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

**Goals:** three leaf libs compile on their own, reference only Core, are wired by the host (actors registered centrally under their `IXxxActor` markers, services through one `Add*` each), and keep build + the full test suite green after every task.

**Non-Goals:** Pipeline/Egress/Enrichment/Mqtt extraction, behavior changes, per-lib test projects, analyzer rollout.

## Decisions

### 1. Cross-project actor access: FunkArr-style marker interfaces (created in stage 1)

User decision (2026-10-02): no `IRequiredActor<T>`; do it like FunkArr. Stage 1 (`extract-core-projects`, Decision 6) already creates `src/Njord.Core/Actors/ActorKeys.cs` (13 empty `IXxxActor` marker interfaces), registers every actor under its marker in the host `NjordActorSystemSetup` and replaces all class-keyed lookups. This stage therefore only moves code; it adds no new key mechanism. After the move `Njord.Grpc` resolves `ISchedulerActor`, `IBudgetTrackerActor`, `IEgressActor`, `ISensorHubActor` (cross-lib) and `IForecastSnapshotActor`/`IEnrichmentSnapshotActor` (own) through `ActorRegistry.Get<IXxxActor>()`/`Context.GetActorAsync<IXxxActor>()` and never references `Njord.Pipeline/Egress/Sensors`. Request/response messages Grpc sends (`QueryPollStates`, `TriggerImmediatePoll`, `RequestEgressSource`, `PushResult`, budget queries) must already be in `Njord.Messages` after stage 1; if any is not, that is a stage-1 gap to fix there, not here.

*Alternatives rejected:* class keys plus project references to the feature libs (recreates the cross-feature dependency); `IRequiredActor<T>` (still keyed by the class, user rejected); marker classes (`SchedulerActorKey`; not the FunkArr pattern).

### 2. Registration stays central in the host; libraries expose services and endpoints only

FunkArr registers all actors in one host-side Akka setup (`AkkaSetupContainer`, per-domain private methods) and keeps per-domain setup in the host. njord already has that shape after stage 1 (`RegisterSensorActors`, `RegisterGrpcActors` in `NjordActorSystemSetup`, `RegisterWithBackoff<TKey, TActor>` stays private there). So the libraries do NOT get `WithXActors()` extensions; instead their actor classes become `public` so the host can call `resolver.Props<T>()`. Libraries expose: `AddNjordGrpc()` (`services.AddGrpc()`, `ConfigPersistence` stays in Core (stage 1 placed it in `Njord.Core/Configuration`)), `AddNjordIngest()` (renamed `AddOpenMeteoIngest`, same body; internal API, no compat shim), `MapNjordGrpc(this WebApplication)` (replaces the four `MapGrpcService<>()` calls); `AddNjordSensors()` only if it carries a service registration (none today, omit). Host setup files stay thin shells (`NjordServiceSetup`, `NjordActorSystemSetup`, `NjordApplicationSetup`); persistence config (`WithSqlPersistence`) stays in the host and runs before the actor registrations.

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

Sensors (smallest, proves the move with markers already in place) -> Ingest (no marker, tests use internals) -> Grpc (largest, protos) -> setup shells/ArchUnit/Dockerfile/docs.

## Risks / Trade-offs

- [Stage 1 gaps: a message/interface Grpc needs is still in a feature project] -> grep Grpc's `using Njord.*` after stage 1; fix in stage 1 or add the type to Core here as a prerequisite task.
- [Marker registration (stage 1) is the only runtime link between libraries; a missing registration fails at runtime, not compile time] -> keep `GrpcSnapshotConsumerTerminatedSpec`, `*GrpcServiceSpec` and `NjordServiceSetupSpec`; the stage-1 `ActorKeyRegistrationSpec` already resolves every marker; keep it green after each move.
- [Protobuf build item path and `ProtoRoot` break after the move, silently producing no generated code] -> compile `Njord.Grpc` alone as a task checkpoint; proto package/namespace unchanged.
- [`InternalsVisibleTo` omission breaks tests that use internals] -> compile `Njord.Tests` after each project move.
- [Docker build breaks because restore sees only one csproj] -> Dockerfile task with a local `docker build` check.
- [`AddOpenMeteoIngest` rename breaks `NjordServiceSetupSpec`] -> update the spec in the same task.
- [Concurrent in-flight changes (`akka-failure-hygiene`) touch the same actors/setup] -> do not start before it is merged.

## Migration Plan

Prerequisites: `extract-core-projects` applied; `akka-failure-hygiene` merged. One commit per project move (Sensors, Ingest, Grpc) plus one for setup/arch/docs. Rollback: `git revert` per commit; no data or wire format changes.

## Open Questions

- Per-library test projects (FunkArr style) after stage 3? Deferred; default is to keep one.
- `ConfigPersistence` is used only by `AdminGrpcService` and its DI registration (grep-verified); stage 1 placed it in `Njord.Core/Configuration`; it stays there (task 4.1).
