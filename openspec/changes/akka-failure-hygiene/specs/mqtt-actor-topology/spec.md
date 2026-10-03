## MODIFIED Requirements

### Requirement: MqttConnectionActor owns the broker connection and MergeHub
The `MqttConnectionActor` SHALL be registered in the actor system only when `Mqtt.Enabled` is `true`. When registered, it SHALL own the `IMqttConnection` and `IMqttTransport` instances. It SHALL materialize a MergeHub sink for outbound `MqttMessage` flow. It SHALL handle connect, reconnect with exponential backoff, LWT (online/offline on the availability topic), and disconnection recovery. It SHALL vend `SinkRef<MqttMessage>` to requestors via a `RequestMqttSink`/`MqttSinkResponse` protocol.

When SinkRef materialization fails, the actor SHALL send `MqttSinkFailed(Exception Cause)` to the requesting actor. The actor SHALL NOT send `Status.Failure` and SHALL NOT return `null` from the failure path.

A failed connect attempt SHALL be reported to the actor as a `ConnectFailed` message and scheduled for reconnect with exponential backoff; connect results SHALL be piped to the actor (not continued with `Task.ContinueWith`).

The actor SHALL use the injected `TimeProvider` for all timestamp operations (health state transitions). It SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: Connection established
- **WHEN** the actor connects to the broker
- **THEN** it publishes "online" on the availability topic

#### Scenario: Actor not registered when MQTT disabled
- **WHEN** Mqtt.Enabled is false
- **THEN** the actor is not registered in the actor system

#### Scenario: Connection lost and reconnected
- **WHEN** the broker connection is lost
- **THEN** the actor reconnects with exponential backoff

#### Scenario: Failed connect attempt schedules reconnect
- **WHEN** the connection attempt throws or is canceled
- **THEN** the actor handles a ConnectFailed message carrying the exception and schedules a reconnect with exponential backoff

#### Scenario: SinkRef vended to requestor
- **WHEN** a requestor sends RequestMqttSink and materialization succeeds
- **THEN** it receives MqttSinkResponse with a valid SinkRef

#### Scenario: SinkRef materialization failure sends MqttSinkFailed
- **WHEN** a requestor sends RequestMqttSink and materialization fails
- **THEN** the requestor receives MqttSinkFailed carrying the exception, not Status.Failure and not null

#### Scenario: Health timestamps use TimeProvider
- **WHEN** the actor records a connect or disconnect timestamp
- **THEN** it uses `TimeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow`

#### Scenario: Graceful shutdown publishes offline
- **WHEN** the actor stops
- **THEN** it publishes "offline" on the availability topic
