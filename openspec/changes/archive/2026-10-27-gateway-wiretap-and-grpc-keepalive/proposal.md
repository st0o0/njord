## Why

Two independent stream robustness issues:

1. The gateway reply sink uses `AlsoTo` which propagates backpressure from the side sink into the pipeline data path. Today the reply sink is just `pollRegion.Tell()` (fire-and-forget) so it never backpressures, but any future change that adds processing to the reply path (metrics, logging) would silently slow the pipeline data delivery. `WireTap` drops copies instead of backpressuring, which is strictly safer.

2. A dead gRPC client (ha-njord crash, network drop) leaves its per-client SourceRef stream running forever on the GrpcConsumer's BroadcastHub. There is no liveness detection: the stream sits idle consuming a hub slot and buffer memory indefinitely. Based on Aaron Stannard's dynamic-hub-keepalive pattern, adding `KeepAlive` + `IdleTimeout` on the per-client stream detects dead clients (stream tears down on idle timeout) while keeping slow-but-alive clients safe (heartbeats reset the timeout during quiet periods between poll cycles).

## What Changes

- Replace `.AlsoToMaterialized(ReplySink, Keep.Left)` with `.WireTap(ReplySink)` in `OpenMeteoGateway.OnSinkCompleted`
- Add a `PipelineEvent.Heartbeat` sentinel event for the KeepAlive stage to inject
- Add `.KeepAlive(keepAliveInterval, () => Heartbeat)` on the per-client stream in `GrpcConsumer.HandleStreamRequest`, after the BroadcastHub source concat
- Add `.IdleTimeout(idleTimeout)` after the Buffer on the per-client stream
- Change `Buffer(256, DropHead)` to `Buffer(256, Backpressure)` on the per-client stream so slow clients do not lose data (backpressure propagates to the hub instead)
- Handle `IdleTimeout` stream failure gracefully (log, no crash)

## Non-goals

- Changing the pipeline-level BroadcastHub or MergeHub topology
- Adding KeepAlive to the pipeline SourceRef (between Pipeline and GrpcConsumer) -- that stream has its own WatchTermination
- Changing how the GrpcConsumer attaches to the pipeline
- Adding gRPC-level keepalive (HTTP/2 PING frames) -- that is a transport concern, not a stream concern

## API-budget impact

None. No change to polling frequency or API call volume.

## Capabilities

### New Capabilities

- `grpc-client-liveness`: Dead gRPC client detection via KeepAlive + IdleTimeout on per-client SourceRef streams

### Modified Capabilities

- `gateway-stream-dispatch`: Replace AlsoTo with WireTap on the reply sink
- `grpc-stream-replay`: Add KeepAlive and IdleTimeout stages to the per-client stream graph; change Buffer overflow strategy

## Impact

- `src/Njord.OpenMeteo/Gateway/OpenMeteoGateway.cs` -- WireTap change (1 line)
- `src/Njord.Grpc/GrpcConsumer.cs` -- KeepAlive, IdleTimeout, Buffer strategy change
- `src/Njord.Messages/Pipeline/PipelineEvent.cs` -- Heartbeat sentinel
- `src/Njord.Grpc.Tests/GrpcConsumerStreamReplayTests.cs` -- new tests for keepalive and idle timeout
- `src/Njord.OpenMeteo.Tests/OpenMeteoGatewayAttachTests.cs` -- verify WireTap does not change behavior
