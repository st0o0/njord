## MODIFIED Requirements

### Requirement: SensorService proto definition
The system SHALL define a `SensorService` gRPC service in `protos/njord/v2/sensor.proto` with two RPCs: `Push` (unary, accepts a single `SensorReading`, returns `PushResponse`) and `StreamPush` (client-streaming, accepts a stream of `SensorReading`, returns `PushResponse`).

#### Scenario: Proto defines SensorKind enum
- **WHEN** the proto file is compiled
- **THEN** a `SensorKind` enum SHALL exist with values: `SENSOR_KIND_UNSPECIFIED (0)`, `SENSOR_KIND_INDOOR_TEMPERATURE (1)`, `SENSOR_KIND_INDOOR_HUMIDITY (2)` and no others, matching the closed domain `SensorKind`

#### Scenario: Proto defines SensorReading message
- **WHEN** the proto file is compiled
- **THEN** a `SensorReading` message SHALL exist with fields: `kind` (SensorKind), `location` (string), `source` (string), `value` (double), `measured_at` (google.protobuf.Timestamp)
