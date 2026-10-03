## Why

Two production bugs degrade the gRPC interface. (1) SchedulerActor crashes on startup due to a circular `GetActor` dependency with PipelineActor, causing every `GetStatus` call to hit dead letters and return empty budget/poll data after a 5s timeout. (2) The `StreamConfig` server-streaming RPC disconnects every ~7-17 minutes because Kestrel's default `MinResponseDataRate` closes idle response streams, forcing the client (ha-njord) into a full re-setup cycle (~15 calls + 5s block) each time.

## What Changes

- Fix SchedulerActor startup to use async actor resolution (`GetActorAsync<PipelineActor>().PipeTo(Self)`) instead of synchronous `GetActor<PipelineActor>()` in `PreStart`, breaking the circular startup dependency.
- Apply the same async resolution pattern in `OnTerminated` to handle PipelineActor restarts safely.
- Disable Kestrel's `MinResponseDataRate` on the gRPC HTTP/2 endpoint so long-lived server streams remain open indefinitely.
- Add tests that reproduce the production startup order (SchedulerActor registered before PipelineActor in the same `WithResolvableActors` block).

## Non-goals

- Changing the actor registration order or architecture — the async resolution is sufficient.
- Adding application-level keepalive pings on the StreamConfig stream — disabling the rate limit is the correct fix since gRPC has native HTTP/2 PING keepalives.
- Modifying polling behaviour or budget logic.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `poll-scheduler`: SchedulerActor startup changes from synchronous to async PipelineActor resolution; `OnTerminated` reconnection also becomes async.
- `kestrel-dual-port`: gRPC endpoint gains `MinResponseDataRate = null` to support long-lived streams.

## Impact

- **Code**: `SchedulerActor.cs` (PreStart, OnTerminated, new message type), `Program.cs` (Kestrel limits), test files.
- **Behaviour**: GetStatus returns poll states immediately instead of timing out. StreamConfig stays connected until config changes or client disconnects.
- **API budget**: No change — no polling logic modified.
