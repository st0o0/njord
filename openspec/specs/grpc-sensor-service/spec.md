# grpc-sensor-service Specification

## Purpose

gRPC service for ingesting external sensor readings into the SensorHub. Defines the SensorService proto with Push (unary) and StreamPush (client-streaming) RPCs, input validation, and source defaulting.

## Requirements

### Requirement: SensorService proto definition
The system SHALL define a `SensorService` gRPC service in `protos/njord/v2/sensor.proto` with two RPCs: `Push` (unary, accepts a single `SensorReading`, returns `PushResponse`) and `StreamPush` (client-streaming, accepts a stream of `SensorReading`, returns `PushResponse`).

#### Scenario: Proto defines SensorKind enum
- **WHEN** the proto file is compiled
- **THEN** a `SensorKind` enum SHALL exist with values: `SENSOR_KIND_UNSPECIFIED (0)`, `SENSOR_KIND_INDOOR_TEMPERATURE (1)`, `SENSOR_KIND_INDOOR_HUMIDITY (2)`, `SENSOR_KIND_HEAT_PUMP_FLOW_TEMP (3)`, `SENSOR_KIND_SOLAR_PANEL_POWER (4)`, `SENSOR_KIND_BATTERY_STATE_OF_CHARGE (5)`, `SENSOR_KIND_HEAT_PUMP_POWER (6)`

#### Scenario: Proto defines SensorReading message
- **WHEN** the proto file is compiled
- **THEN** a `SensorReading` message SHALL exist with fields: `kind` (SensorKind), `location` (string), `source` (string), `value` (double), `measured_at` (google.protobuf.Timestamp)

### Requirement: Push RPC accepts single reading
The `Push` RPC SHALL validate the reading (known SensorKind, known location, plausible value) and forward it to the SensorHub actor. It SHALL return a `PushResponse` with `accepted = true` on success or `accepted = false` with `rejection_reason` on failure.

#### Scenario: Valid reading accepted
- **WHEN** a `Push` request with `kind=INDOOR_TEMPERATURE, location="Luzern", source="wohnzimmer", value=23.5` is received
- **AND** "Luzern" is a configured location
- **THEN** the response SHALL have `accepted = true`

#### Scenario: Unknown SensorKind rejected
- **WHEN** a `Push` request with `kind=SENSOR_KIND_UNSPECIFIED` is received
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate unknown sensor kind

#### Scenario: Unknown location rejected
- **WHEN** a `Push` request with `location="Atlantis"` is received
- **AND** "Atlantis" is not a configured location
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate unknown location

#### Scenario: Value outside plausibility range rejected
- **WHEN** a `Push` request with `kind=INDOOR_TEMPERATURE, value=85.0` is received
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate the valid range

### Requirement: StreamPush RPC accepts reading stream
The `StreamPush` RPC SHALL accept a client-streaming sequence of `SensorReading` messages, validate and forward each to the SensorHub actor, and return a single `PushResponse` when the stream completes. The response SHALL indicate whether all readings were accepted.

#### Scenario: All readings accepted
- **WHEN** a stream of 3 valid readings is sent
- **THEN** the response SHALL have `accepted = true`

#### Scenario: Some readings rejected
- **WHEN** a stream contains 2 valid and 1 invalid reading
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate the count of rejected readings

### Requirement: Empty source defaults to "default"
The `SensorGrpcService` SHALL treat an empty or whitespace-only `source` field as `"default"`.

#### Scenario: Empty source normalized
- **WHEN** a reading with `source=""` is received
- **THEN** it SHALL be stored with source `"default"`
