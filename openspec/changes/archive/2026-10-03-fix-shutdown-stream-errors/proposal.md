## Why

Stopping the service with SIGTERM logs three error lines, each an
`AbruptTerminationException` ("Processor actor ... terminated abruptly"), for the
stream stages `egress-hub` (egress/StreamSupervisor) and `pipeline-fetch-in` /
`pipeline-fetch-out` (pipeline/StreamSupervisor). The behavior pre-dates the project
split. The streams are never completed: `EgressActor` and `PipelineActor` materialize
long-lived graphs with `Context.Materializer()` and nothing stops them before the
ActorSystem terminates, so the stream processor actors are killed under their
supervisor. The errors are noise, but they make real shutdown failures hard to spot.

## What Changes

- `EgressActor` and `PipelineActor` own a `UniqueKillSwitch` placed directly behind the
  `MergeHub` source of their long-lived graph.
- Both actors accept a graceful-stop command, trigger the kill switch (normal
  completion downstream, including the BroadcastHub), wait for the graph completion
  task(s), then reply with an ack.
- The host registers a CoordinatedShutdown task in `before-service-unbind` (ordering:
  Pipeline first, then Egress) that asks both actors to stop their streams with a
  bounded timeout; a timeout is logged as a warning and shutdown continues.
- Requirement text that currently forbids any manual `KillSwitch` for pipeline
  lifecycle is relaxed to allow an actor-owned shutdown switch.
- No change to normal-operation behavior, stream topology or buffer sizes.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `pipeline-actor`: the "no manual KillSwitch" rule becomes "actor-owned kill switch used
  only for graceful shutdown"; add a graceful-stop scenario.
- `stream-composition`: same relaxation in the single-materialization requirement and a
  rewritten "Clean shutdown" requirement (stop is driven by CoordinatedShutdown, not
  only by actor stop).

## Impact

- Code: `src/Njord.Egress/EgressActor.cs`, `src/Njord.Pipeline/PipelineActor.cs`,
  shutdown messages in their message files, host wiring in
  `src/Njord/Configuration/NjordActorSystemSetup.cs` (CoordinatedShutdown task).
- Tests: new specs in `src/Njord.Tests` for both actors plus a host-level wiring spec.
- API budget: 0 additional Open-Meteo requests (no change to polling).

## Non-goals

- No behavior change during normal operation.
- No change to stream topology or buffer sizes beyond shutdown wiring.
- No change to the consumer actors (`StreamConsumerActor` already has a SharedKillSwitch)
  or to gRPC per-call streams unless the smoke check shows they also log errors.
