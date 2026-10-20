## Why

FunkArr actors are thin shells: all domain logic lives in sealed state records + extension methods. Several njord actors violate this: GrpcConsumer has no state record (mutable dicts as fields), MqttConnection scatters state across 5 mutable fields, PollWorker mixes immutable `_state` with mutable `_roundId`/`_domainResults`. GrpcConsumer also silently drops AQ/Flood alerts (bug).

## What Changes

- Extract `GrpcConsumerState` sealed record + Extensions from GrpcConsumer. Fix AQ/Flood alert handling.
- Extract `MqttConnectionState` sealed record + Extensions from MqttConnection mutable fields.
- Move PollWorker's `_roundId`, `_domainResults`, `_pendingDomains` into `PollWorkerState` (or a separate `PollRoundState`).
- Make `ProjectionEngine.Evaluate` a testable static method instead of a closure.
- Add pure state-level unit tests (no Akka TestKit needed).

## Non-goals

- Changing actor behavior. Same logic, better structure.

## Capabilities

### New Capabilities
- `actor-state-pattern`: All actors follow FunkArr thin-shell pattern

### Modified Capabilities
- `consumer-model`: GrpcConsumer properly handles all alert types

## Impact

- **Njord.Grpc**: GrpcConsumer refactored, bug fixed
- **Njord.Mqtt**: MqttConnection refactored
- **Njord.Pipeline**: PollWorker state consolidated, ProjectionEngine extracted
- **Tests**: New pure state tests, fewer actor-level tests needed
