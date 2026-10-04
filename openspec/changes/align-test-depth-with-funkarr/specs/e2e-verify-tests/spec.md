## ADDED Requirements

### Requirement: E2E tests boot the full host with Mosquitto via Testcontainers
The E2E test project SHALL use Testcontainers to start a Mosquitto MQTT broker, boot the full njord host configured to connect to it, and inject fake Open-Meteo HTTP responses via a `DelegatingHandler` registered in the `HttpClient` pipeline.

#### Scenario: Host connects to Mosquitto and publishes discovery
- **WHEN** the E2E host starts with MQTT enabled and a Testcontainers Mosquitto broker
- **THEN** the host SHALL connect to the broker and publish MQTT discovery payloads

#### Scenario: Fake Open-Meteo responses drive the pipeline
- **WHEN** a poll cycle triggers
- **THEN** the pipeline SHALL use the injected `DelegatingHandler` responses instead of calling the real API

### Requirement: Single-location single-model happy path produces verifiable MQTT payloads
A Verify-snapshot test SHALL capture all MQTT messages (discovery config + state payload) produced by one poll cycle for a single location with a single model.

#### Scenario: Discovery payload matches golden master
- **WHEN** one poll cycle completes for location "lucerne" with model "icon_d2"
- **THEN** the discovery config payload published to `homeassistant/device/njord_lucerne_icon_d2/config` SHALL match the Verify snapshot

#### Scenario: State payload matches golden master
- **WHEN** one poll cycle completes
- **THEN** the state payload published to the model's state topic SHALL match the Verify snapshot

### Requirement: Multi-model consensus produces verifiable enrichment payloads
A Verify-snapshot test SHALL capture the consensus enrichment payloads produced when multiple models return data for the same location.

#### Scenario: Consensus device discovery matches golden master
- **WHEN** a poll cycle completes with two models returning data for the same location and consensus is enabled
- **THEN** the consensus device discovery payload SHALL match the Verify snapshot

### Requirement: Enrichment pipeline produces verifiable payloads
Verify-snapshot tests SHALL cover each enrichment feature's MQTT output: alerts, derived values, trends, indices, and history.

#### Scenario: Alert enrichment payload matches golden master
- **WHEN** a poll cycle produces alert conditions (e.g., temperature exceeding threshold)
- **THEN** the alert device's discovery and state payloads SHALL match the Verify snapshot

#### Scenario: Derived enrichment payload matches golden master
- **WHEN** a poll cycle completes with sufficient data for derived computations
- **THEN** the derived device's state payload (sunshine_pct, diurnal_amplitude, etc.) SHALL match the Verify snapshot

### Requirement: HA birth message triggers re-discovery
A test SHALL verify that publishing `online` to `homeassistant/status` causes njord to re-publish all discovery payloads.

#### Scenario: Birth message triggers full re-discovery
- **WHEN** `online` is published to `homeassistant/status` after initial discovery
- **THEN** all discovery config payloads SHALL be re-published to the broker

### Requirement: Sensor push via gRPC affects enrichment
A test SHALL verify that pushing a sensor reading via gRPC is reflected in the next enrichment cycle.

#### Scenario: Indoor temperature push used in derived computation
- **WHEN** an indoor temperature reading is pushed via gRPC and a poll cycle completes
- **THEN** the enrichment output that depends on indoor temperature SHALL reflect the pushed value
