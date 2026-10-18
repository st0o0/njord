# mqtt-adapter-boundary Specification

## Purpose
TBD - created by archiving change typed-mqtt-payloads. Update Purpose after archive.
## Requirements
### Requirement: MQTT-specific code lives in Njord.Mqtt
Every type that is MQTT-specific (name or namespace contains `Mqtt`, or that exists only to serve the MQTT egress: transport, connection and publisher actors, their actor messages, payload models and mappers, topic scheme, serializer, health check, telemetry) SHALL be declared in `Njord.Mqtt`. The only exceptions SHALL be an explicit allow-list in `Njord.Core`: `MqttOptions`, `MqttOptionsValidator` and the marker interfaces `IMqttConnectionActor` and `IMqttStatePublisher`.

#### Scenario: Actor messages are internal to the adapter
- **WHEN** the solution is inspected
- **THEN** `Njord.Messages` has no `Mqtt` folder and the connection actor messages are declared in `Njord.Mqtt`

#### Scenario: Health check belongs to the adapter
- **WHEN** MQTT is enabled
- **THEN** the `mqtt-connection` health check is registered by `MqttSetupContainer`, not by the host's core setup

#### Scenario: New MQTT type in the wrong project
- **WHEN** a type with `Mqtt` in its name is added to a project other than `Njord.Mqtt` and is not on the allow-list
- **THEN** an architecture spec fails

### Requirement: Domain projects contain no wire models
`Njord.Weather`, `Njord.Consensus`, `Njord.Messages`, `Njord.Pipeline` and the analysis libraries SHALL NOT build JSON for MQTT or reference `System.Text.Json.Nodes` for payload assembly. Wire records and their mappers live in the adapter project (`Njord.Mqtt` for MQTT, `Njord.Grpc` for gRPC).

#### Scenario: Forecast projection is wire-neutral
- **WHEN** `HorizonProjection` selects the values of a horizon
- **THEN** it returns typed entries and no JSON string

### Requirement: Njord.Mqtt has a fixed internal layout
`Njord.Mqtt` SHALL organise its types in the folders `Payloads`, `Mapping`, `Serialization`, `Topics`, `Actors`, `Health`, `Transport` and `Configuration`, with namespaces following the folders.

#### Scenario: Payload records
- **WHEN** a new payload record is added
- **THEN** it is declared in `Njord.Mqtt/Payloads` with its mapper in `Njord.Mqtt/Mapping`

