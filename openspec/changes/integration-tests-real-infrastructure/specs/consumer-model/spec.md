## MODIFIED Requirements

### Requirement: Consumer state across a rewire
When a consumer re-attaches after its pipeline subscription ended (see `stream-consumer-actor`), `ForecastReadModel` SHALL keep its in-memory latest values and SHALL keep its secondary hub, so queries answered before the re-attach are answered the same way during and after it, and gRPC streaming clients attached before the re-attach keep their stream and receive the events of the new subscription. `MqttStatePublisher` SHALL start each subscription with an empty dedup cache, and SHALL empty it whenever the broker connection is re-established (`MqttConnectionState.Session` changed), so the first event of every state topic after a re-attach or a reconnect is published even if its payload equals the last published one (a restarted broker may have lost its retained messages).

#### Scenario: Read model keeps its state across a rewire
- **WHEN** `ForecastReadModel` holds the latest alert event for "lucerne" and its pipeline subscription fails
- **THEN** `QueryLatestAnalysis(lucerne)` still answers with that alert event while and after the subscription is re-attached, and events of the new subscription update it

#### Scenario: Streaming client survives a rewire
- **WHEN** a gRPC streaming client is attached to `ForecastReadModel` and the pipeline subscription completes
- **THEN** the client's stream does not end, and it receives the events of the new subscription

#### Scenario: MQTT republishes after a rewire
- **WHEN** `MqttStatePublisher` published a state payload, its subscription failed, and the same payload arrives on the new subscription
- **THEN** the payload is published again

#### Scenario: MQTT republishes after a reconnect
- **WHEN** `MqttStatePublisher` published a state payload, the broker connection was re-established, and the same payload arrives again
- **THEN** the payload is published again

#### Scenario: MQTT does not republish on the same connection
- **WHEN** `MqttStatePublisher` published a state payload and the same payload arrives again on the same subscription and broker connection
- **THEN** the payload is not published again
