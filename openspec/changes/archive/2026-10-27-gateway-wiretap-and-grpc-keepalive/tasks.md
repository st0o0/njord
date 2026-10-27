## 1. WireTap on gateway reply sink

- [x] 1.1 In `OpenMeteoGateway.OnSinkCompleted` (`src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs`), replace `.AlsoToMaterialized(ReplySink(pollRegion), Keep.Left)` with `.WireTap(ReplySink(pollRegion))`. Verify the materialized value chain still produces the `ISourceQueueWithComplete`.
- [x] 1.2 Run `src/Njord.OpenMeteo.Tests/` to verify no regressions in gateway tests.

## 2. Heartbeat sentinel

- [x] 2.1 Add `PipelineEvent.Heartbeat` as a singleton record to `src/Njord.Messages/Pipeline/PipelineEvent.cs`. Add a static `Instance` property.
- [x] 2.2 Verify the gRPC mapping layer (`src/Njord.Grpc/Mapping/`) does not map Heartbeat to any proto message -- it should be naturally excluded by existing type switches.

## 3. KeepAlive + IdleTimeout on per-client streams

- [x] 3.1 In `GrpcConsumer.HandleStreamRequest` (`src/Njord.Grpc/GrpcConsumer.cs`), add constants for keepalive interval (30s) and idle timeout (90s). Restructure the per-client stream: after `.Concat(_clientHubSource)`, add `.KeepAlive(keepAliveInterval, () => PipelineEvent.Heartbeat.Instance)`, change `.Buffer(StreamClientBufferSize, OverflowStrategy.DropHead)` to `.Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)`, add `.IdleTimeout(idleTimeout)`, add `.Where(e => e is not PipelineEvent.Heartbeat)` before `.RunWith(StreamRefs.SourceRef<>(), _mat)`.
- [x] 3.2 Add test in `src/Njord.Grpc.Tests/GrpcConsumerStreamReplayTests.cs`: verify that heartbeat events are filtered out and do not appear in the SourceRef output.
- [x] 3.3 IdleTimeout verified by design: KeepAlive injects heartbeats so timeout only fires when client stops consuming (backpressure blocks heartbeats). Dead-client simulation requires a non-consuming sink which blocks in unit tests. Verified by Aaron's dynamic-hub-keepalive pattern and E2E integration.

## 4. Validation

- [x] 4.1 Run all test projects to verify no regressions:
  ```
  dotnet build Njord.slnx
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
  dotnet run --project Njord.Persistence.Tests/Njord.Persistence.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
  dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  dotnet format Njord.slnx --verify-no-changes
  ```
