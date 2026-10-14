## Why

A single failed MQTT publish permanently silences MQTT state publishing. `MqttStatePublisher` calls
`IMqttTransport.SendAsync` inside a `SelectAsync(1)` stage; when it faults (e.g. MQTTnet's
`MqttClientNotConnectedException` while the broker is briefly disconnected), the stage sees an
`AggregateException`, the shared logging decider answers `Stop`, the graph fails and the
`SourceRef` subscription to the pipeline is cancelled. `StreamConsumerActor` only rewires when a
watched dependency terminates, so nothing rematerializes the graph: Home Assistant receives no
state until the service restarts. In addition, the dedup cache records a payload's hash *before*
it is sent, so even a recovered stream would skip the payload that never reached the broker.

## What Changes

- A failed publish is handled per message: it is logged as a Warning, counted, and the stream
  continues with the next message. Publishing resumes as soon as the transport works again.
- A publish that does not complete within a bounded publish timeout counts as failed, so a hung
  broker connection cannot back-pressure the pipeline's output hub.
- The dedup cache records a payload hash only after its publish succeeded; a failed payload is
  published again on the next cycle. The `published` dedup decision is counted after a successful send.
- New counter `njord_mqtt_publish_failures_total` (label `location`) on the `Njord.Mqtt` meter.

## Non-goals

- No change to `StreamConsumerActor` (no rewire on stream completion or failure) and no
  `RestartSource` around the MQTT graph.
- No change to the shared `StreamSupervision` decider or to the `AggregateException`
  classification it performs for other streams.
- No buffering or retry queue of failed messages inside a cycle; the next pipeline cycle is the retry.
- No change to `MqttConnection` (connect/reconnect, availability publishing) or to the discovery side.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `mqtt-actor-topology`: `MqttStatePublisher` survives failed and timed-out publishes and keeps its pipeline subscription.
- `delta-publishing`: the dedup cache is updated only after a successful publish.
- `prometheus-metrics`: egress metrics gain `njord_mqtt_publish_failures_total`; the `published` dedup decision counts successful sends.

## Impact

- Code: `src/Njord.Mqtt/MqttStatePublisher.cs`, `src/Njord.Mqtt/MqttEgressTuning.cs`
  (new `PublishTimeout`, default 10 s), `src/Njord.Mqtt/Telemetry.cs`.
- Tests: `src/Njord.Mqtt.Tests/MqttPublishFailureSpec.cs` (new), `MqttStateSpec.cs`, `TelemetrySpec.cs`.
- Metrics: one new counter; no change to existing metric names or labels.
- MQTT wire format, discovery and gRPC: unchanged. No golden master changes.
- API budget: none - polling is untouched (0 additional Open-Meteo requests/month).
