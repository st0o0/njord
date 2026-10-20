## 1. Rename and move

- [x] 1.1 Rename `IForecastReadModel` → `IGrpcConsumer` in `src/Njord.Core/Actors/ActorKeys.cs`
- [x] 1.2 Rename `RequestReadModelStream`/`RequestReadModelStreamCompleted` → `RequestGrpcStream`/`RequestGrpcStreamCompleted` in `src/Njord.Core/Actors/StreamWiringMessages.cs`
- [x] 1.3 Move `src/Njord.Pipeline/ForecastReadModel.cs` → `src/Njord.Grpc/GrpcConsumer.cs`, rename class to `GrpcConsumer`
- [x] 1.4 Remove ForecastReadModel registration from `src/Njord.Pipeline/Configuration/PipelineSetupContainer.cs`
- [x] 1.5 Add GrpcConsumer registration in `src/Njord.Grpc/Configuration/GrpcSetupContainer.cs` (added Akka.Cluster.Hosting + Servus.Akka packages)
- [x] 1.6 Update all references in `src/Njord.Grpc/WeatherGrpcService.cs`
- [x] 1.7 Njord.Grpc and Njord.Grpc.Tests build cleanly

## 2. Replace volatile state with actor state

- [x] 2.1 GrpcConsumer uses `Sink.ActorRef<PipelineEvent>(Self, new StreamCompleted(), ...)` instead of `.Select(UpdateState).Recover().To(hubSink)`
- [x] 2.2 Added `Receive<PipelineEvent>(OnPipelineEvent)` handler that calls UpdateState + _hubInput.Tell(evt)
- [x] 2.3 `_state` is a regular `ImmutableDictionary` field, no `volatile`
- [x] 2.4 Removed Recover hack — stream errors end the subscription, base class re-attaches

## 3. Replace MergeHub with Source.ActorRef

- [x] 3.1 `Source.ActorRef<PipelineEvent>(256, DropHead).ToMaterialized(BroadcastHub.Sink<PipelineEvent>(2), Keep.Both).Run(Mat)` — stores _hubInput (IActorRef) and _clientHubSource
- [x] 3.2 Kept `Sink.Ignore` drain on the BroadcastHub
- [x] 3.3 MaterializeClientHub simplified: no MergeHub, just Source.ActorRef → BroadcastHub

## 4. Update tests

- [x] 4.1 Moved `ForecastReadModelSpec.cs` → `src/Njord.Grpc.Tests/GrpcConsumerSpec.cs`, renamed class + all message types
- [x] 4.2 Moved `ForecastReadModelRewireSpec.cs` → `src/Njord.Grpc.Tests/GrpcConsumerRewireSpec.cs`
- [x] 4.3 Updated GatewayRestartSpec (Pipeline.Tests) — now creates GrpcConsumer, added Njord.Grpc project reference
- [x] 4.4 Updated WeatherGrpcServiceSpec — renamed IGrpcConsumer, fake actors
- [x] 4.5 Grpc.Tests: 140 pass

## 5. Update documentation

- [x] 5.1 Updated AGENTS.md: all ForecastReadModel → GrpcConsumer references
- [x] 5.2 Updated AGENTS.md solution structure: GrpcConsumer in Grpc section, removed from Pipeline section

## 6. Validation

- [x] 6.1 Njord.Grpc and Njord.Grpc.Tests build cleanly. Full solution has errors only from parallel healthcheck-ask-pattern fork (not this change).
- [x] 6.2 `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — 140 pass
- [x] 6.3 Pipeline.Tests cannot build due to healthcheck fork removing PollHealthState — not this change's issue
- [x] 6.4 Architecture tests updated: `Grpc_contains_no_actors` → `Grpc_contains_only_the_GrpcConsumer_actor`
- [x] 6.5 Formatted with dotnet format
- [x] 6.6 Test counts: Grpc.Tests 125→140 (+15 moved from Pipeline.Tests), Pipeline.Tests will decrease correspondingly
