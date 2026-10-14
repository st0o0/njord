## Why

`MqttConnection` awaits the retained `online` publish inside a `ReceiveAsync` handler without a
`try`, so a failing or hung publish either crashes the actor or blocks its mailbox. A crash makes
the supervisor restart it: `PostStop` publishes a retained `offline` while the broker connection
is still up, `PreStart` connects again on the shared, still-connected MQTTnet client (MQTTnet
rejects that: "It is not allowed to connect with a server after the connection is established"),
and the actor then loops on rejected reconnects while Home Assistant sees njord as `offline`,
`/healthz` still reports MQTT as connected, and inbound listeners are lost. A broker that refuses
the CONNECT (e.g. wrong credentials) is worse: MQTTnet returns a non-success result without
throwing, the actor treats it as connected, the `online` publish throws and the actor restarts in
a tight loop without backoff. Failed connects additionally schedule two reconnects (MQTTnet also
raises its disconnected event), and every failed attempt resets the disconnect timestamp, so the
health check can never reach `Unhealthy` while reconnects keep failing.

## What Changes

- `MqttConnection` becomes an explicit state machine (connecting, waiting to reconnect,
  connected). Every transport call (connect, status-topic subscribe, `online` publish) runs via
  `PipeTo` with a project-owned failure message; no transport failure can crash or block the actor.
- A failed or timed-out `online` publish logs a Warning, keeps the connection and is retried on the
  same connection with capped exponential backoff until it succeeds or the connection is lost.
- The `online` publish is bounded by `MqttEgressTuning.PublishTimeout`.
- A failed status-topic subscribe logs a Warning and keeps the connection; it is retried with the
  next connect.
- At most one reconnect is pending at a time; a disconnect signal while no connection is
  established is ignored.
- The health snapshot records the disconnect time once, when an established connection is lost,
  and keeps it across failed reconnect attempts.
- `MqttNetPublisher` treats a non-success CONNACK as a failed connect and reports only the loss of
  an established connection as a disconnect.
- The `offline` publish on stop is best effort: a failure is logged and never escapes unobserved.

## Non-goals

- No change to the actor's supervisor strategy or to the singleton registration.
- No change to `MqttStatePublisher`, the state payloads or topics, or the health check thresholds
  (`Degraded` before the first connect stays as is).
- No change to the Last Will, the reconnect delay formula or the defaults in `MqttEgressTuning`.
- No new consumer of inbound messages (Home Assistant birth handling stays a forwarded message).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `mqtt-actor-topology`: `MqttConnection` handles every transport failure as a message, retries a
  failed `online` publish on the live connection, keeps a single pending reconnect and the original
  disconnect timestamp, and treats a refused CONNECT as a failed connect.

## Impact

- Code: `src/Njord.Mqtt/MqttConnection.cs`, `src/Njord.Mqtt/Transport/MqttNetPublisher.cs`.
- Tests: `src/Njord.Mqtt.Tests/MqttConnectionLifecycleSpec.cs` (new),
  `src/Njord.Mqtt.Tests/MqttNetPublisherSpec.cs` (new).
- MQTT wire format, topics, metrics and gRPC: unchanged. No golden master changes.
- API budget: none - polling is untouched (0 additional Open-Meteo requests/month).
