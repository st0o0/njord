## Why

`ModelStateActor` lives in `Njord.Egress` but directly references `DiscoveryActor` from `Njord.Mqtt` — it resolves the actor, guards on `Mqtt.Enabled`, and sends `ModelCapabilityLearned` via a direct `Tell`. This couples the egress layer to MQTT concerns that belong downstream. The fix landed in the previous `optional-mqtt` change made the coupling worse by adding `_mqttEnabled` and null guards. With capability data already flowing through the `EgressActor` hub for other consumers (gRPC snapshots), capability events should follow the same path.

## What Changes

- Add `EgressEvent.CapabilityLearned` as a new variant of the `EgressEvent` discriminated union, carrying the same data as the current `ModelCapabilityLearned` record.
- `ModelStateActor` emits `EgressEvent.CapabilityLearned` into the `EgressActor` hub instead of sending `ModelCapabilityLearned` directly to `DiscoveryActor`. Remove `using Njord.Mqtt`, `_discoveryActor`, `_mqttEnabled`, and `Context.GetActor<DiscoveryActor>()`.
- `DiscoveryActor` subscribes to the `EgressActor` BroadcastHub via `RequestEgressSource` and filters for `CapabilityLearned` events, replacing the direct-tell reception.
- Remove the `ModelCapabilityLearned` record from `EgressMessages.cs` (replaced by the `EgressEvent` variant).
- Remove the `optional-mqtt-egress` spec requirement about `ModelStateActor` skipping discovery — no longer needed when the coupling is gone.

## Non-goals

- Changing the capability-tracking logic itself (parameter extraction, horizon capping, change detection) — only the delivery mechanism changes.
- Altering the `ModelCapabilityLearned` data shape — the fields move into `EgressEvent.CapabilityLearned` unchanged.
- No API budget impact — this change does not alter polling behavior.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `egress-event`: New `CapabilityLearned` variant added to the `EgressEvent` union.
- `model-capability-tracking`: `ModelStateActor` emits capability data as `EgressEvent` through the hub instead of direct-telling `DiscoveryActor`.
- `mqtt-actor-topology`: `DiscoveryActor` subscribes to the `EgressActor` BroadcastHub for capability events.
- `optional-mqtt-egress`: Remove the "ModelStateActor skips discovery notification" requirement — no longer needed.

## Impact

- **Code**: `ModelStateActor.cs`, `EgressEvent.cs`, `EgressMessages.cs`, `DiscoveryActor.cs`.
- **Tests**: `ModelStateActorSpec.cs` (simplifies — no more fake DiscoveryActor needed), `DiscoveryActorSpec.cs` (subscribes to hub instead of receiving direct tells).
- **Dependencies**: `ModelStateActor` loses its `Njord.Mqtt` import — cleaner layer separation.
