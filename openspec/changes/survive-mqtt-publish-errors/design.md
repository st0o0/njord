## Context

See proposal.md - Why. Verified behavior before the change (reproduced in
`MqttPublishFailureSpec` with a scripted `IMqttTransport`):

- `MqttStatePublisher.MaterializeGraph` runs `SourceRef.Source -> KillSwitch -> SelectMany(map + dedup)
  -> SelectAsync(1, SendAsync) -> Sink.Ignore`, supervised by `StreamSupervision.LoggingDecider`.
- A faulted `SendAsync` task reaches the decider as an `AggregateException`, which `Classify` maps to
  `Stop` (even a wrapped `TimeoutException` would). The graph fails, the SourceRef is cancelled and
  the `ForecastPipeline` BroadcastHub drops this consumer. The materialized completion task is
  discarded and `StreamConsumerActor` only rewires on `Terminated` of a watched dependency, so the
  publisher stays in `Ready` with a dead graph: MQTT is silent until restart.
- `MqttNetPublisher.SendAsync` throws `MqttClientNotConnectedException` immediately while the broker
  is disconnected, so every reconnect window was a likely trigger.
- The dedup dictionary stores the payload hash in the `SelectMany` step, before the send. A failed
  payload would be skipped as unchanged on every later cycle.
- The pipeline itself was not stalled by a *failure* (the hub just loses a consumer), but a *hung*
  QoS 1 publish would back-pressure the BroadcastHub (buffer 2) - and with it the gRPC read model -
  for as long as MQTTnet's own client timeout.

## Goals / Non-Goals

**Goals:**
- Per-message failure isolation in the publish step, with Warning log and failure counter.
- Dedup state reflects what the broker actually received.
- Bounded publish time so MQTT cannot stall the shared pipeline output.

**Non-Goals:**
- Generic stream-failure recovery in `StreamConsumerActor` (stages other than the publish step keep
  the existing decider semantics).

## Decisions

### Catch inside the publish step (chosen)
The `SelectAsync(1)` lambda wraps `SendAsync` in try/catch and returns normally on failure. Only the
publish is guarded, so the outcome is known *in* the step that owns the dedup state - which is
needed anyway to update the cache after success.

Alternatives considered:
- **Resume decider on the `SelectAsync` stage**: would keep the stream alive, but the decider only
  sees the exception, not the message, so the dedup entry could not be rolled back and the failure
  could not be counted per location. It also requires unwrapping `AggregateException` and a
  stage-local decider that differs from the shared one.
- **`RestartSource.WithBackoff` around the graph**: rematerializes the SourceRef subscription
  (a new `RequestPipelineOutput` round-trip) and resets the dedup cache on every transient broker
  error, re-publishing everything; events emitted during the backoff are lost to the hub.
- **Rewire in `StreamConsumerActor` on graph failure**: correct as a last line of defence for all
  consumers, but heavyweight for an expected, per-message condition and out of scope here; it
  would still need the dedup fix.

### Dedup moves into the publish step
`SelectMany` only maps events to `(MqttMessage, location)`; the `SelectAsync(1)` step checks the hash,
sends, and records the hash after success. With parallelism 1 the dictionary is accessed strictly
sequentially, and since a failed send leaves the previous hash untouched, the next cycle re-sends.
The `published` dedup decision is counted after the successful send; failures go to
`njord_mqtt_publish_failures_total{location}` (bounded cardinality, same `location` label).

### Bounded publish timeout
Each send gets a `CancellationTokenSource(MqttEgressTuning.PublishTimeout)` passed to `SendAsync`
and awaited with `WaitAsync(token)`, so even a transport ignoring the token is abandoned. Default
10 s: far above a healthy QoS 1 round trip on the LAN broker, far below a 60 min poll interval.
`MqttEgressTuning` is already a DI singleton and is injected into `MqttStatePublisher`.

## Risks / Trade-offs

- [A timed-out publish may still reach the broker] -> Re-publishing the same retained state on the
  next cycle is idempotent for Home Assistant.
- [While the broker is down each cycle logs one Warning per message] -> Bounded by the static
  entity set per cycle; the counter makes the rate visible. Acceptable for an outage condition.
- [Failed messages are not retried until the next cycle] -> State is refreshed every poll; HA marks
  entities unavailable via `expire_after` if outages last longer.
