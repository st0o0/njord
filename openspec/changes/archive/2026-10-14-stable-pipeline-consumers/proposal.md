## Why

Change `stable-pipeline-input` made the `ForecastPipeline` hubs stable: they are built once per pipeline incarnation
and only a pipeline restart (a bug) replaces them. The consumers still carry the machinery of the time when the
pipeline rebuilt its output hub on every gateway restart: a two-phase state machine with stash, dead-ref detection,
generation-tagged graph completions and a `MergeHub` in the read model that only existed to survive rebuilds. They
also watch the registry entry (the singleton proxy, which never terminates) instead of the pipeline incarnation, the
MQTT publisher back-pressures the pipeline hub while the broker is slow, and a restarted pipeline analyzes with the
startup settings and an empty sensor map until the next change or expiry tick.

## What Changes

- `StreamConsumerActor` becomes a small pipeline-output consumer base: resolve the pipeline, request a
  `SourceRef<PipelineEvent>` (reply matched by request id, reply timeout, capped backoff), materialize, and re-attach
  only when the pipeline incarnation that answered terminates or the consumer's own graph completes or fails. No
  waiting phase, no stash, no dead-ref detection, no subclass handshake hooks. The safety net for a consumer's own
  graph failing (a mapping bug) stays, including the backoff that grows on repeated losses and
  `njord_stream_consumer_rewires_total`, which now counts every re-attach after a lost subscription.
- `ForecastReadModel` builds its streaming hub once when it starts, so `RequestReadModelStream` is answered from the
  start and streaming clients keep their streams across a re-attach; the permanent drain and the per-client
  `Buffer(256, DropHead)` stay.
- `MqttStatePublisher` runs its private stream as pipeline events → bounded drop-oldest buffer → payload mapping →
  `Sink.ForEachAsync(1, dedup + publish)`. A slow or stuck broker drops the oldest buffered events instead of
  back-pressuring the pipeline hub. Publish failures and the publish timeout behave as today; the dedup cache resets
  per subscription.
- `ForecastPipeline` asks the `ConfigManager` for the current analysis settings (`QueryAnalysisSettings`, new in
  `Njord.Messages/Config`) and asks the `SensorHub` to push its current map (`RequestSensorSnapshots`, new in
  `Njord.Messages/Sensors`) when it starts. A missing actor leaves the startup settings and the empty map in place.
- **BREAKING** (internal contract): `Ack` (`Njord.Messages/Common`) is removed; it has no users since change
  `stable-pipeline-input`.
- `AGENTS.md` describes the final consumer shape.

## Non-goals

- No permanent drain on the pipeline's output hub: every consumer now drains fast, and without consumers the
  hub keeps back-pressuring, which holds the startup forecasts until the read model attaches (see design.md).
- No replay of events a consumer missed while it re-attached; the next poll cycle refills it.
- gRPC streaming clients may still end when the read model restarts; ha-njord reconnects.
- Poll workers per target (change `poll-workers-per-target`).

## API budget

None: polling, request weights and the request count are unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `stream-consumer-actor`: the attach / re-attach model replaces the state machine, dependency tracking and
  dead-ref retries.
- `consumer-model`: consumers attach only on their own start, pipeline termination or their own graph ending; the
  read model's streaming hub exists from its start.
- `mqtt-actor-topology`: the publisher's bounded drop-oldest buffer and `Sink.ForEachAsync` publish stage.
- `delta-publishing`: the dedup cache lives per pipeline subscription.
- `stream-composition`: kill switch and handshake-failure handling move to the consumer base.
- `pipeline-composition`: the pipeline fetches the current settings and sensor map when it starts.
- `stream-actor-conventions`: the pipeline's handler list gains the settings reply.
- `message-conventions`: `Common/` and `Ack` disappear; the two new messages are listed.
- `test-assertion-hygiene-2`: the vacuous-assertion rule no longer uses the removed `Ack` as its example.
- `prometheus-metrics`: the consumer re-attach counter counts every lost subscription.
- `actor-resolve-failure-handling`: the consumer retries a failed or unanswered attach with backoff.
- `structured-logging`: the consumer base logs the received SourceRef.

## Impact

- Code: `src/Njord.Core/Actors/StreamConsumerActor.cs`, `src/Njord.Pipeline/ForecastReadModel.cs`,
  `src/Njord.Pipeline/ForecastPipeline.cs`, `src/Njord.Mqtt/MqttStatePublisher.cs`,
  `src/Njord.Mqtt/MqttEgressTuning.cs`, `src/Njord.SensorHub/SensorHub.cs`,
  `src/Njord/Configuration/ConfigManager.cs`, `src/Njord.Messages/{Config,Sensors,Common}/`.
- Tests: `src/Njord.Core.Tests/Actors/`, `src/Njord.Pipeline.Tests/`, `src/Njord.Mqtt.Tests/`,
  `src/Njord.SensorHub.Tests/`, `src/Njord.Tests/`, `src/Njord.Tests.Shared/FakePipelineOutput.cs`.
- No configuration, wire-format, proto or persistence change.
