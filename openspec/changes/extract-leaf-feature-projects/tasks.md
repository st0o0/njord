## 1. Preconditions and shared plumbing

- [ ] 1.1 Verify prerequisites: `extract-core-projects` applied (`src/Njord.Core`, `Njord.Domain`, `Njord.Messages`, `Njord.Persistence` exist), `akka-failure-hygiene` merged, `dotnet build Njord.slnx` and `dotnet run --project Njord.Tests/Njord.Tests.csproj` green from `src/`
- [ ] 1.2 Decide marker registration mechanism (Servus `WithResolvableActors(...Register<T>)` vs Akka.Hosting `registry.Register<Marker>(ref)`); write a failing spec in `src/Njord.Tests/Configuration/ActorMarkerRegistrationSpec.cs` that builds the host and resolves `SchedulerActorKey`, `BudgetTrackerActorKey`, `EgressActorKey`, `SensorHubActorKey` from `ActorRegistry`
- [ ] 1.3 Add marker types `SchedulerActorKey`, `BudgetTrackerActorKey`, `EgressActorKey`, `SensorHubActorKey` to `src/Njord.Core/ActorKeys.cs`; register them next to the existing class-keyed registrations (keep class keys until their last consumer moves); spec 1.2 passes
- [ ] 1.4 Extract the private `RegisterWithBackoff<TActor>` from `src/Njord/Configuration/NjordActorSystemSetup.cs` into a public extension in `src/Njord.Core` (same behavior: `BackoffSupervisor`, 3 s / 30 s / 0.2, `maxNrOfRetries: -1`, name `<name>-supervisor`); host calls it; build + tests green

## 2. Njord.Sensors

- [ ] 2.1 Create `src/Njord.Sensors/Njord.Sensors.csproj` (plain SDK, `ProjectReference` Njord.Core only, `InternalsVisibleTo Njord.Tests`); add to `src/Njord.slnx`
- [ ] 2.2 Move `src/Njord/Sensors/SensorHubActor.cs` to `src/Njord.Sensors/`; keep namespace `Njord.Sensors`; build green
- [ ] 2.3 Add `src/Njord.Sensors/SensorsAkkaExtensions.cs` with `WithSensorsActors(...)` registering `SensorHubActor` under name `sensor-hub` and `SensorHubActorKey`; host `NjordActorSystemSetup.cs` calls it and drops the direct `r.Register<SensorHubActor>("sensor-hub")`; skip `AddNjordSensors()` unless a service registration exists
- [ ] 2.4 Host `Njord.csproj` references `Njord.Sensors`; `src/Njord.Tests/Sensors/SensorHubActorSpec.cs` compiles unchanged; build + full test run green; commit

## 3. Njord.Ingest

- [ ] 3.1 Create `src/Njord.Ingest/Njord.Ingest.csproj` (plain SDK, references Njord.Core only; `dotnet add package Microsoft.Extensions.Http` if `AddHttpClient` is not available, `InternalsVisibleTo Njord.Tests`); add to `src/Njord.slnx`
- [ ] 3.2 Move `OpenMeteoClient.cs`, `OpenMeteoDtos.cs`, `OpenMeteoJsonContext.cs`, `IngestServiceCollectionExtensions.cs` from `src/Njord/Ingest/` (confirm `IOpenMeteoClient` lives in Core after stage 1; otherwise move it there as a prerequisite); namespace `Njord.Ingest` unchanged
- [ ] 3.3 Rename `AddOpenMeteoIngest` to `AddNjordIngest`; update `NjordServiceSetup.cs` and `src/Njord.Tests/Configuration/NjordServiceSetupSpec.cs`; update `src/Njord.Tests.Shared/FakeOpenMeteoClient.cs` references only if it needs more than `IOpenMeteoClient`
- [ ] 3.4 Host and `Njord.Tests` reference `Njord.Ingest`; `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` still compiles (internals via IVT); build + full test run green; commit

## 4. Njord.Grpc

- [ ] 4.1 Verify with grep that `src/Njord/Grpc/*.cs` references no `Njord.Pipeline`, `Njord.Egress`, `Njord.Sensors` classes after stage 1 (only Core/Messages/Domain/Persistence types); fix any leftover in Core first. Confirm `ConfigPersistence` users (`AdminGrpcService`, `NjordServiceSetup`) and decide its home (default: `Njord.Grpc`)
- [ ] 4.2 Create `src/Njord.Grpc/Njord.Grpc.csproj`: plain SDK + `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, `dotnet add package Grpc.AspNetCore`, the `<Protobuf Include="..\..\protos\njord\v2\*.proto" GrpcServices="Server" ProtoRoot="..\..\protos" />` item, `InternalsVisibleTo Njord.Tests`; references Njord.Core only; add to `src/Njord.slnx`
- [ ] 4.3 Move `src/Njord/Grpc/*.cs` (WeatherGrpcService, AdminGrpcService, OpsGrpcService, SensorGrpcService, EnrichmentProtoMapper, ForecastSnapshotActor/State, EnrichmentSnapshotActor/State, GrpcSnapshotConsumerActor, SnapshotMessages) to `src/Njord.Grpc/`; remove `<Protobuf>` and `Grpc.AspNetCore` from `src/Njord/Njord.csproj`
- [ ] 4.4 Replace `actorRegistry.Get<SchedulerActor>()`/`BudgetTrackerActor`/`EgressActor`/`SensorHubActor` in `OpsGrpcService.cs`, `WeatherGrpcService.cs`, `SensorGrpcService.cs` with the marker keys; adjust the specs under `src/Njord.Tests/Grpc/` that register fake actors (`OpsGrpcServiceSpec`, `WeatherGrpcServiceSpec`) to register under the markers; failing spec first, then green
- [ ] 4.5 Add `src/Njord.Grpc/GrpcServiceCollectionExtensions.cs` (`AddNjordGrpc()`: `services.AddGrpc()`, `ConfigPersistence` if owned here), `GrpcAkkaExtensions.cs` (`WithGrpcActors()`: `GrpcSnapshotConsumerActor` resolvable `grpc-snapshot-consumer`; `ForecastSnapshotActor` and `EnrichmentSnapshotActor` via the Core `RegisterWithBackoff`, names unchanged), `GrpcEndpointExtensions.cs` (`MapNjordGrpc(this WebApplication)` mapping the four services); host setup files call them
- [ ] 4.6 Host and `Njord.Tests` reference `Njord.Grpc`; confirm the generated `Njord.Grpc.V2` types still compile; run `dotnet build Njord.slnx` and the full test run; commit

## 5. Setup shells, architecture rules, docker, docs

- [ ] 5.1 Update `src/Njord.Tests/Architecture/NjordArchitecture.cs` to load every Njord assembly (list instead of `typeof(Njord.Ingest.OpenMeteoClient).Assembly`); add specs asserting `Njord.Ingest`, `Njord.Grpc`, `Njord.Sensors` reference only Core-and-below assemblies and never each other or the host; prove each red with a temporary violation, then remove it; `ProductionTypes` covers all production assemblies
- [ ] 5.2 Narrow `src/Njord.Tests.Shared/Njord.Tests.Shared.csproj` references from the host to Core (+ whatever it really uses); build green
- [ ] 5.3 Update `Dockerfile`: `COPY` the new csprojs before `dotnet restore` (keep `COPY protos/ /protos/`); run `docker build .` locally if Docker is available, otherwise record that it was not run
- [ ] 5.4 Update `AGENTS.md` "Solution structure" for the new projects and reference direction; update paths cited in `.claude/skills/njord-persistent-actor/SKILL.md`, `njord-enrichment-feature/SKILL.md`, `njord-actor-spec/SKILL.md` (verify every cited path with `ls`)

## 6. Validation

- [ ] 6.1 From `src/`: `dotnet build Njord.slnx` (0 errors) and `dotnet run --project Njord.Tests/Njord.Tests.csproj` (all green, test count unchanged except the new marker/architecture specs)
- [ ] 6.2 From `src/Njord/`: `dotnet run` with `Njord__Mqtt__Enabled=false` starts and serves gRPC and `/healthz` (smoke check; note if not run)
- [ ] 6.3 `openspec validate extract-leaf-feature-projects` passes; `git diff --stat` shows no behavior-related changes (moves, csproj, setup, docs only); grep confirms no `using Njord.Pipeline|Njord.Egress|Njord.Enrichment|Njord.Mqtt` remains in `src/Njord.Grpc`, `src/Njord.Ingest`, `src/Njord.Sensors`
- [ ] 6.4 Conventional Commits per step (`refactor: extract Njord.Sensors`, `refactor: extract Njord.Ingest`, `refactor: extract Njord.Grpc`, `docs: update structure for leaf projects`); do not push
