## Why

Every Akka.Streams graph in the codebase uses `_ => Directive.Resume` as its
supervision decider, silently swallowing all exceptions — including unexpected
ones like `NullReferenceException` or serialization bugs. A stream element that
fails is permanently dropped with zero diagnostics. Users discover the problem
only when Home Assistant stops updating, with no log trail to diagnose the cause.

Additionally, `NjordServiceSetup` reads `Mqtt:Enabled` with a default of `true`
while `MqttOptions.Enabled` defaults to `false`. When no MQTT config section
exists, transport services and health checks are registered but never used —
creating a confusing health check state.

## What Changes

- **Stream supervision deciders**: Replace all 8 instances of
  `_ => Directive.Resume` with a shared decider that logs the exception at
  Warning level before deciding. Expected transient errors resume; unexpected
  errors escalate (stop the stream stage) so they become visible.
- **MQTT Enabled default**: Align `NjordServiceSetup.cs` to use the same
  default (`false`) as `MqttOptions.Enabled`.

## Non-goals

- Changing actor-level supervision strategies or adding backoff supervisors.
- Adding retry logic to stream stages.
- Any polling frequency or API budget changes (zero budget impact).

## Capabilities

### New Capabilities

- `stream-supervision`: Shared stream supervision decider with logging and
  type-aware exception handling.

### Modified Capabilities

- `pipeline-actor`: PipelineActor stream graphs use the new shared decider.
- `stream-composition`: All stream graphs across Egress, Enrichment, MQTT, and
  gRPC use the new shared decider.
- `optional-mqtt-egress`: NjordServiceSetup MQTT default aligned with
  MqttOptions.

## Impact

- **Files modified**: PipelineActor.cs, ModelStateActor.cs, EnrichmentActor.cs,
  MqttEgressActor.cs, MqttConnectionActor.cs, GrpcSnapshotConsumerActor.cs,
  HistoryEnrichment.cs, NjordServiceSetup.cs, plus a new shared decider.
- **Logging**: Warning-level log entries will appear for stream exceptions that
  were previously silent. This is the desired outcome.
- **APIs**: No public API changes.
- **Dependencies**: No new packages.
- **Risk**: Low — Resume behavior is preserved for transient errors; only truly
  unexpected exceptions change from silent-drop to escalate+log.
