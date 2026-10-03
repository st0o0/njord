## Context

`ModelStateActor` (in `Njord.Egress`) has two concerns: producing `EgressEvent.PerModelUpdate` for the egress hub, and tracking model capabilities to notify `DiscoveryActor` (in `Njord.Mqtt`). The second concern creates a hard dependency from the egress layer into the MQTT layer — `using Njord.Mqtt`, `Context.GetActor<DiscoveryActor>()`, and the `_mqttEnabled` guard added in the `optional-mqtt` change.

The `EgressActor` already operates as a MergeHub→BroadcastHub fan-out. Multiple consumers subscribe independently: `MqttEgressActor` for MQTT publishing, `GrpcSnapshotConsumerActor` for gRPC streaming. Capability data fits the same pattern.

## Goals / Non-Goals

**Goals:**
- Remove all `Njord.Mqtt` references from `ModelStateActor`.
- Route capability data through the existing `EgressActor` hub.
- Make `DiscoveryActor` a hub consumer — same pattern as `MqttEgressActor`.
- Simplify `ModelStateActor` tests (no fake DiscoveryActor, no MQTT config awareness).

**Non-Goals:**
- Changing capability-tracking logic (parameter extraction, horizon capping, dedup).
- Changing the `DiscoveryActor`'s internal state machine or payload building.
- Removing `ModelCapabilityLearned` as a concept — it becomes an `EgressEvent` variant with the same fields.

## Decisions

### D1: `EgressEvent.CapabilityLearned` as new variant

Add to `EgressEvent`:
```csharp
public sealed record CapabilityLearned(
    string Location,
    WeatherModel Model,
    IReadOnlySet<ParameterDef> SupportedParameters,
    IReadOnlyList<int> ApplicableHorizons,
    IReadOnlyList<int> ApplicableDayOffsets) : EgressEvent;
```

Same fields as `ModelCapabilityLearned`. The standalone `ModelCapabilityLearned` record in `EgressMessages.cs` is deleted.

**Alternative**: Keep `ModelCapabilityLearned` and wrap it in an `EgressEvent` — rejected because it adds indirection with no benefit.

### D2: `ModelStateActor` emits `CapabilityLearned` into the egress sink

In `MaterializeGraph`, replace `discoveryActor.Tell(new ModelCapabilityLearned(...))` with emitting `new EgressEvent.CapabilityLearned(...)` into the same sink as `PerModelUpdate`. The `SelectMany` lambda returns both events when a capability change is detected:

```
return [
    new EgressEvent.CapabilityLearned(...),
    new EgressEvent.PerModelUpdate(...)
];
```

This removes: `_discoveryActor`, `_mqttEnabled`, `using Njord.Mqtt`, `SendCapabilityLearned` method, and all null guards.

### D3: `DiscoveryActor` subscribes to the EgressActor BroadcastHub

`DiscoveryActor.PreStart` adds `RequestEgressSource` to `EgressActor` alongside the existing `RequestMqttSink` to `MqttConnectionActor`. Once both refs arrive, it materializes a stream that filters for `EgressEvent.CapabilityLearned` and pipes them into the existing `OnCapabilityLearned`/`OnCapabilityUpdate` handlers via `Self.Tell`.

The `DiscoveryActor` already has a `WaitingForSink` → `WaitingForCapabilities` → `Ready` state machine. The new flow:
1. `PreStart`: request both `MqttSink` and `EgressSource`.
2. `WaitingForRefs`: collect both refs, then transition.
3. Once both arrive: materialize the egress source stream (filter → Self.Tell) and the sink queue, then enter `WaitingForCapabilities`.

This keeps the actor's internal state machine intact — only the source of `CapabilityLearned` messages changes from external `Tell` to self-piped stream events.

### D4: Internal `CapabilityReceived` message for self-piping

The stream stage that filters `EgressEvent.CapabilityLearned` sends a private `CapabilityReceived` message to `Self` rather than casting the `EgressEvent` directly. This keeps the actor's message protocol clean and avoids the need to handle raw `EgressEvent` in the actor's receive blocks.

## Risks / Trade-offs

- **[Capability events visible to all hub consumers]** → `GrpcSnapshotConsumerActor` and future consumers see `CapabilityLearned` events. They simply ignore unknown variants (pattern match on what they care about). Minimal overhead — capability events are rare (once per model on startup, occasionally on expansion).
- **[DiscoveryActor needs two refs before starting]** → Slightly more complex startup (wait for both `MqttSink` and `EgressSource`). Same pattern as `MqttEgressActor` which already waits for both egress source and MQTT sink.
- **[Ordering]** → `CapabilityLearned` and `PerModelUpdate` flow through the same hub. `DiscoveryActor` may see a `PerModelUpdate` before the corresponding `CapabilityLearned` if they're emitted in the same batch. This is fine — `DiscoveryActor` ignores `PerModelUpdate` entirely, and `MqttEgressActor` ignores `CapabilityLearned`.
