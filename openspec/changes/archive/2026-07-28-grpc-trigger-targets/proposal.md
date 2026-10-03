## Why

The current gRPC API requires two sequential calls (`GetLocations` → `GetModels`) to discover
which location/model pairs can be triggered, and a third call (`GetStatus`) to see their poll
state. A single RPC that returns all configured targets with their poll status makes the
trigger workflow a one-call discovery + one-call trigger pattern.

## What Changes

- Add `GetTriggerTargets` RPC to `ConfigService` that returns a flat list of all
  configured location/model pairs with their current poll state (phase, next poll,
  last change, miss count, cycle duration).
- Use `google.protobuf.Timestamp` for temporal fields in the new message type
  instead of raw `int64` unix seconds.

## Non-goals

- Migrating existing `ModelStatus` in `GetStatus` to `Timestamp` types (separate
  concern, would be a breaking change to an established message).
- Adding model metadata (display name, provider, region) — that stays in
  `ForecastService.GetModels`.
- Streaming variant of trigger targets.

## Capabilities

### New Capabilities

- `trigger-targets-rpc`: Flat RPC returning all location/model pairs with poll state for trigger discovery.

### Modified Capabilities

- `grpc-config-service`: Adding the new RPC to the existing ConfigService proto definition.

## Impact

- **Proto**: `protos/njord/v1/config_service.proto` — new RPC + messages, new import for `google/protobuf/timestamp.proto`.
- **Service**: `ConfigGrpcService` — new handler method, asks `SchedulerActor` for `PollStatesSnapshot` (same path as `GetStatus`).
- **Tests**: New spec for the RPC in `Njord.Tests`.
- **Budget**: No polling impact — read-only RPC over existing in-memory state.
