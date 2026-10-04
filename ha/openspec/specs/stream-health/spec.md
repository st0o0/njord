## Purpose

Makes gRPC stream connection state visible to users via binary sensors and HA repair issues, enabling monitoring and automation on stream health.

## Requirements

### Requirement: Binary sensors track stream connection state
The integration SHALL create one `BinarySensorEntity` per stream type (`forecast_stream`, `enrichment_stream`, `config_stream`) with `device_class=BinarySensorDeviceClass.CONNECTIVITY` and `entity_category=EntityCategory.DIAGNOSTIC`. The sensor SHALL be `on` when the stream is connected, `off` when disconnected.

#### Scenario: Stream connected
- **WHEN** the forecast stream is actively receiving data
- **THEN** `binary_sensor.njord_forecast_stream` state is `on`

#### Scenario: Stream disconnected
- **WHEN** the enrichment stream has disconnected and is in reconnect backoff
- **THEN** `binary_sensor.njord_enrichment_stream` state is `off`

#### Scenario: Stream reconnects
- **WHEN** a disconnected stream successfully reconnects
- **THEN** the corresponding binary sensor transitions from `off` to `on`

### Requirement: Stream binary sensors are diagnostic entities
All stream connection binary sensors SHALL have `entity_category = EntityCategory.DIAGNOSTIC` and SHALL be grouped under the server device.

#### Scenario: Entity category is diagnostic
- **WHEN** the stream binary sensors are registered
- **THEN** each has `entity_category` set to `DIAGNOSTIC`

### Requirement: HA repair issue on prolonged stream disconnect
When a stream remains disconnected for more than 60 seconds, the integration SHALL create an HA repair issue (`homeassistant.helpers.issue_registry`). The issue SHALL be dismissed automatically when the stream reconnects.

#### Scenario: Stream disconnected over 60 seconds
- **WHEN** the forecast stream has been disconnected for 65 seconds
- **THEN** an HA repair issue is created with a descriptive message including the stream name

#### Scenario: Stream reconnects within grace period
- **WHEN** the forecast stream disconnects and reconnects within 30 seconds
- **THEN** no HA repair issue is created

#### Scenario: Repair issue dismissed on reconnect
- **WHEN** a repair issue exists for the enrichment stream and the stream reconnects
- **THEN** the repair issue is automatically dismissed

### Requirement: Stream state is exposed on the coordinator
The coordinator SHALL maintain a `stream_states: dict[str, bool]` mapping stream names to connection state. This dict SHALL be updated by the stream wrapper on connect and disconnect events.

#### Scenario: Initial state before streams start
- **WHEN** the coordinator is created but streams have not started
- **THEN** `stream_states` has all three keys set to `False`

#### Scenario: Stream wrapper updates state on connect
- **WHEN** a stream successfully establishes its gRPC connection
- **THEN** `stream_states[stream_name]` is set to `True` and `async_set_updated_data` is called

### Requirement: Stream reconnect wrapper tracks connection state
The `_stream_with_reconnect` method SHALL track an internal `connected` state and only invoke `on_reconnect` / `on_disconnect` callbacks on **actual state transitions**. A normal stream end (iterator exhaustion without error) followed by a successful reconnect SHALL NOT toggle the connection state. Only gRPC errors SHALL trigger a disconnect transition.

#### Scenario: First connect fires on_reconnect
- **WHEN** `_stream_with_reconnect` starts and the first gRPC call succeeds
- **THEN** `on_reconnect` is called once and `stream_states[name]` becomes `True`

#### Scenario: Normal stream end does not fire on_disconnect
- **WHEN** a stream iterator exhausts normally (server closed the stream after sending data) and the reconnect succeeds
- **THEN** neither `on_disconnect` nor `on_reconnect` is called — `stream_states[name]` remains `True`

#### Scenario: gRPC error fires on_disconnect
- **WHEN** a stream raises `grpc.aio.AioRpcError` (not `CANCELLED`)
- **THEN** `on_disconnect` is called and `stream_states[name]` becomes `False`

#### Scenario: Reconnect after error fires on_reconnect
- **WHEN** a stream was disconnected due to a gRPC error and the next call succeeds
- **THEN** `on_reconnect` is called and `stream_states[name]` becomes `True`

#### Scenario: Repeated errors do not spam on_disconnect
- **WHEN** a stream fails with a gRPC error multiple times in a row (backoff cycle)
- **THEN** `on_disconnect` is called only on the first error — subsequent errors while already disconnected do not call `on_disconnect` again

#### Scenario: Cancelled stream does not fire callbacks
- **WHEN** a stream raises `grpc.aio.AioRpcError` with `StatusCode.CANCELLED`
- **THEN** the method returns without calling `on_disconnect`
