## 1. Stream Topology Proof Tests

- [x] 1.1 Create `src/Njord.Grpc.Tests/SourceRefIdleTimeoutTests.cs` (TestKit base). Add `Producer_side_idle_timeout_fires_during_demand_stall`: Source.Never + KeepAlive(200ms) + Buffer(16,BP) + IdleTimeout(1s) + SourceRef; consumer with Throttle(1, 2s). Assert TimeoutException.
- [x] 1.2 Add `Consumer_side_idle_timeout_survives_upstream_silence`: Source.Never + KeepAlive(200ms) + SourceRef; consumer IdleTimeout(1s) + Where(drop heartbeat) + Buffer(16,BP) + Sink.Ignore. Assert stream alive after 3s.
- [x] 1.3 Add `Consumer_side_idle_timeout_detects_silent_producer`: Source.Queue (no offers, no KeepAlive) + SourceRef; consumer IdleTimeout(1s) + Buffer(16,BP) + Sink.Ignore. Assert TimeoutException.
- [x] 1.4 Add `Consumer_side_keepalives_reset_timeout_under_backpressure`: Source.Never + KeepAlive(200ms) + SourceRef; consumer IdleTimeout(1s) + Where(drop heartbeat) + Buffer(16,BP) + Throttle(1, 2s) + Sink.Ignore. Assert stream alive after 3s.

## 2. GrpcConsumer KeepAlive Delivery Test

- [x] 2.1 Add `Entity_stream_delivers_keepalives_through_sourceref` to `src/Njord.Grpc.Tests/GrpcConsumerStreamTests.cs`: start GrpcConsumer, get SourceRef, filter for ConsumerKeepAlive with Sink.First, assert keepalive arrives within 45s.

## 3. Production Code: Producer Pipeline

- [x] 3.1 In `src/Njord.Grpc/GrpcConsumer.cs` `HandleEntityStreamRequest`: remove `.Buffer(StreamClientBufferSize, OverflowStrategy.Backpressure)` and `.IdleTimeout(_clientIdleTimeout)` from the stream. Remove unused `StreamClientBufferSize` and `_clientIdleTimeout` constants. Result: `.KeepAlive(interval).RunWith(SourceRef)`.

## 4. Production Code: Consumer Pipeline

- [x] 4.1 In `src/Njord.Grpc/NjordGrpcService.cs` `Subscribe`: insert `.IdleTimeout(TimeSpan.FromSeconds(90))` immediately after `sourceRef.Source`, before the existing `.Where(not ConsumerKeepAlive)`.
- [x] 4.2 Insert `.Buffer(128, OverflowStrategy.Backpressure)` after `.Where(static e => e is not ConsumerKeepAlive)`, before `.Select(static e => e.ToProto())`. Add `using Njord.Grpc;` if needed for ConsumerKeepAlive visibility (already in namespace).

## 5. Validation

- [x] 5.1 Run `dotnet build Njord.slnx` from `src/` -- must compile
- [x] 5.2 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` from `src/` -- all 144 tests pass
- [x] 5.3 Run all test projects sequentially from `src/` -- 1,786 tests pass, zero failures
- [x] 5.4 Run `dotnet format Njord.slnx --verify-no-changes` from `src/` -- formatting clean
