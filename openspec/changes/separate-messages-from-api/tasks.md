Test conventions for all groups: `Spec` suffix, `sealed` class, BDD-style method names.
Actor and async specs carry `[Fact(Timeout = 5000)]`, or `TestTimeouts.Hosted` for hosted
actor systems. Pure mapper specs use a plain `[Fact]`. The golden masters and the existing
service specs must pass without re-approval after every group.

## 1. Messages becomes the Akka-free contract project

- [x] 1.1 Move `RequestForecastStream` and its responses from `src/Njord.Messages/Forecast/RequestForecastStream.cs` to `src/Njord.Core/Actors/StreamWiringMessages.cs`; update the gateway and pipeline
- [x] 1.2 Remove the `Akka` and `Akka.Streams` package references from `src/Njord.Messages/Njord.Messages.csproj`, add the `Njord.Compute` project reference; update `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` (Messages → Weather, Sensors, Compute; no Akka)
- [x] 1.3 Move `PipelineEvent`/`IEnrichmentEvent` from `src/Njord.Core/Egress/PipelineEvent.cs` to `src/Njord.Messages/Pipeline/PipelineEvent.cs` (and `EnrichmentKind` alongside if Messages needs it); update namespaces in Pipeline, Mqtt, Grpc
- [x] 1.4 Move `QueryForecast` from `Snapshots/` to `Forecast/`, `QueryBudgetUsage`/`RecordApiCall` to `Budget/`; switch location strings in messages to `LocationId`; update all senders and receivers and their specs

## 2. Read model out of the API project

- [x] 2.1 Add `IForecastReadModel` to `src/Njord.Core/Actors/ActorKeys.cs` and the handshake `RequestReadModelStream` to `StreamWiringMessages.cs`; add `QueryLatestAnalysis` (+ result hierarchy) to `src/Njord.Messages/Pipeline/`
- [x] 2.2 Move `src/Njord.Grpc/GrpcConsumer.cs` to `src/Njord.Pipeline/ForecastReadModel.cs` with no public nested records; register it in `NjordActorSystemSetup`; port its specs to `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs` (unary query, no data after restart, streaming hub, dead-client idle timeout)

## 3. Config and budget through messages

- [x] 3.1 Add `src/Njord.Messages/Config/` contracts: `ConfigSnapshot` tree, `QueryConfig`, `QueryCatalog` (with model coverage), `UpdateLocations`, `UpdateSettings`, `UpdateAnalysisConfig`, `UpdateBudget`, each with a `Completed`/`Rejected`/`Failed` hierarchy carrying budget projection and warnings
- [x] 3.2 Extend `src/Njord/Configuration/ConfigManager.cs`: answer the queries (snapshot built from options; catalog from scope options plus `ModelCoverageRegistry`), apply update commands through the section writer with `ConfigReloadPolicy` warnings, and expose `RequestConfigStream` (`BroadcastHub<ConfigSnapshot>`); specs in `src/Njord.Tests/Configuration/ConfigManagerSpec.cs`, including "every options value reaches the snapshot"
- [x] 3.3 Add `QueryBudgetStatus` (usage plus effective budget) to `src/Njord.Messages/Budget/`, answered by `src/Njord.Budget/BudgetTracker`; spec in `src/Njord.Budget.Tests/`

## 4. gRPC as a pure adapter

- [x] 4.1 Add `src/Njord.Grpc/ActorAsk.cs` (resolve marker, ask with timeout, map to `Unavailable`/`DeadlineExceeded`); spec in `src/Njord.Grpc.Tests/ActorAskSpec.cs`
- [x] 4.2 Add `src/Njord.Grpc/Mapping/*ProtoMappings.cs` per domain; move the logic from `CatalogMapper`, `PollStatusMapper`, `EnrichmentProtoMapper` and the inline mappings; move their specs and the golden-master spec to the new names
- [x] 4.3 Rewrite `WeatherGrpcService` and `SensorGrpcService` to ask/map only (catalog via `QueryCatalog`, location/model validation via the catalog result or the target actor's rejection)
- [x] 4.4 Rewrite `OpsGrpcService` to ask/map only (budget via `QueryBudgetStatus`, toggles via `QueryConfig`)
- [x] 4.5 Rewrite `AdminGrpcService` to ask/map only (`GetConfig`/`Set*` via ConfigManager messages, `StreamConfig` via `RequestConfigStream`); keep `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs` expectations unchanged
- [x] 4.6 Remove `Microsoft.Extensions.Options` usage and any reference to `BudgetCalculator`/`ModelCoverageRegistry` from `src/Njord.Grpc`; delete `GrpcConsumer`, `CatalogMapper`, `PollStatusMapper`, `EnrichmentProtoMapper`

## 5. Enforce and document

- [ ] 5.1 Add ArchUnit rules in `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs`: no `ActorBase` subtype in `Njord.Grpc`; `Njord.Grpc` does not depend on `IOptions*`, `IConfigWriter`, `BudgetCalculator`, `ModelCoverageRegistry`; no public record nested in an actor class in production assemblies
- [x] 5.2 (done 2026-10-13 in the main-spec format repair) Repair the main specs `openspec/specs/message-conventions/spec.md` and `openspec/specs/message-domain-separation/spec.md` (they currently fail `openspec validate`: missing `## Purpose`/`## Requirements` structure) so this change's deltas can be merged on archive
- [ ] 5.3 Update AGENTS.md (solution structure, reference direction Messages → Weather/Sensors/Compute, consumers section) and CLAUDE.md skill routing if affected

## 6. Validation

- [ ] 6.1 `cd src && dotnet build Njord.slnx` with no new warnings
- [ ] 6.2 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build` (golden masters and service specs unchanged)
- [ ] 6.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` and `dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj --no-build`
- [ ] 6.4 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build` and `dotnet run --project Njord.Tests/Njord.Tests.csproj --no-build`
- [ ] 6.5 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 6.6 Full loop over all `Njord.*Tests` projects (AGENTS.md "Build & test")
- [ ] 6.7 Run the `e2e-test` skill's gRPC sections against the Docker stack to confirm the RPC responses are unchanged
- [ ] 6.8 From the repo root: `openspec validate separate-messages-from-api --strict` and `openspec validate --all --no-interactive`
