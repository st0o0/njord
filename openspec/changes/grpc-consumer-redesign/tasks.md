## 1. Rename and move

- [ ] 1.1 Rename `IForecastReadModel` → `IGrpcConsumer` in `src/Njord.Core/Actors/ActorKeys.cs`
- [ ] 1.2 Rename `RequestReadModelStream`/`RequestReadModelStreamCompleted` → `RequestGrpcStream`/`RequestGrpcStreamCompleted` in `src/Njord.Core/Actors/StreamWiringMessages.cs`
- [ ] 1.3 Move `src/Njord.Pipeline/ForecastReadModel.cs` → `src/Njord.Grpc/GrpcConsumer.cs`, rename class to `GrpcConsumer`
- [ ] 1.4 Remove ForecastReadModel registration from `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`
- [ ] 1.5 Add GrpcConsumer registration in Njord.Grpc (new or existing setup container)
- [ ] 1.6 Update all references in `src/Njord.Grpc/` services
- [ ] 1.7 Build solution — verify compiles

## 2. Replace volatile state with actor state

- [ ] 2.1 In GrpcConsumer: change `MaterializeGraph` to use `Sink.ActorRef<PipelineEvent>(Self, StreamCompleted.Instance)` instead of `.Select(UpdateState).Recover().To(hubSink)`
- [ ] 2.2 Add `Receive<PipelineEvent>(OnPipelineEvent)` handler that calls UpdateState + _hubInput.Tell(evt)
- [ ] 2.3 Remove `volatile` from state fields — make them regular fields
- [ ] 2.4 Remove Recover hack

## 3. Replace MergeHub with Source.ActorRef

- [ ] 3.1 Replace MergeHub→BroadcastHub with `Source.ActorRef<PipelineEvent>(256, DropHead).ToMaterialized(BroadcastHub.Sink<PipelineEvent>(2), Keep.Both).Run(Mat)` — store hubInput (IActorRef) and clientHubSource
- [ ] 3.2 Keep `Sink.Ignore` drain on the BroadcastHub
- [ ] 3.3 Remove MaterializeClientHub method and MergeHub references

## 4. Update tests

- [ ] 4.1 Move `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs` → `src/Njord.Grpc.Tests/GrpcConsumerSpec.cs`, rename, simplify
- [ ] 4.2 Move `src/Njord.Pipeline.Tests/ForecastReadModelRewireSpec.cs` → `src/Njord.Grpc.Tests/GrpcConsumerRewireSpec.cs`
- [ ] 4.3 Update any references in other test files (GatewayRestartSpec, etc.)
- [ ] 4.4 Build and run affected test projects

## 5. Update documentation

- [ ] 5.1 Update AGENTS.md references from ForecastReadModel to GrpcConsumer
- [ ] 5.2 Update AGENTS.md solution structure (move from Pipeline section to Grpc section)

## 6. Validation

- [ ] 6.1 `dotnet build Njord.slnx` — clean
- [ ] 6.2 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — all pass
- [ ] 6.3 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` — all pass
- [ ] 6.4 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — all pass
- [ ] 6.5 `dotnet format Njord.slnx --verify-no-changes` — clean
- [ ] 6.6 Update test counts in AGENTS.md if changed
