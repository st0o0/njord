## 1. Implementation

- [x] 1.1 Add snapshot replay to `GrpcConsumer.HandleStreamRequest` in `src/Njord.Grpc/GrpcConsumer.cs`: prepend `Source.From(_state.DomainData.Values.Concat<PipelineEvent>(_state.Results.Values))` before `.Concat(_clientHubSource)`

## 2. Tests

- [x] 2.1 Add `GrpcConsumerStreamReplayTests` in `src/Njord.Grpc.Tests/GrpcConsumerStreamReplayTests.cs`: feed GrpcConsumer with PipelineEvents via a TestProbe pipeline, then send `RequestGrpcStream`, materialize the SourceRef and verify cached events arrive immediately (before any new pipeline event)
- [x] 2.2 Add empty-state test: send `RequestGrpcStream` before any PipelineEvent, verify the SourceRef source produces no replay events and then receives a live event when one is sent
- [x] 2.3 Add test: send PipelineEvents for multiple domains + feature results, then `RequestGrpcStream`, verify all cached events are present in the replay burst

## 3. Validation

- [x] 3.1 Run `dotnet build Njord.slnx` from `src/`
- [x] 3.2 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` from `src/`
- [x] 3.3 Run all test projects sequentially to verify no regressions
- [x] 3.4 Run `dotnet format Njord.slnx --verify-no-changes` from `src/`
