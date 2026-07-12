# mqtt-egress Specification

## Purpose

MQTT egress to Home Assistant: an actor-owned broker connection lifecycle with a Last Will availability topic, device-based MQTT Discovery for the static config-derived entity grid, one retained telemetry state per device per poll cycle, and declarative mapping of missing values to `unavailable` so entities never disappear or go stale.

## Requirements

### Requirement: The connection actor owns the broker lifecycle
An actor SHALL own the MQTT connection: connect at startup, reconnect with
exponential backoff, register a Last Will that publishes `offline` (retained)
to the service availability topic, and publish `online` (retained) after every
successful (re)connect. Egress failures MUST NOT crash the host process or
the poll pipeline.

#### Scenario: Last Will announces service death
- **WHEN** njord's connection dies without a clean disconnect
- **THEN** the broker publishes retained `offline` on the service
  availability topic

#### Scenario: Reconnect restores availability
- **WHEN** the broker becomes reachable again after an outage
- **THEN** the actor reconnects with backoff and publishes retained `online`

### Requirement: Device-based discovery for a static entity grid
For every configured (location, model) pair the system SHALL publish one
retained device-based discovery payload
(`<prefix>/device/njord_<location>_<model>/config`) containing the device
block, an origin block, shared state and availability options, and one sensor
component per configured (parameter, horizon) pair with a stable
`unique_id` (`njord_<location>_<model>_<parameter>_h<horizon>`). The grid is
derived from configuration only — never from fetched data. Discovery SHALL be
published at startup and re-published when Home Assistant announces `online`
on `homeassistant/status`.

#### Scenario: Grid size follows configuration
- **WHEN** 1 location, 8 models, 9 parameters, and horizons 3/6/12/24/48/72
  are configured
- **THEN** exactly 8 retained discovery payloads are published, each carrying
  54 sensor components

#### Scenario: HA birth triggers re-discovery
- **WHEN** `homeassistant/status` receives `online`
- **THEN** all discovery payloads are published again

#### Scenario: Removed devices are tombstoned
- **WHEN** a model is removed from the configuration and njord restarts
- **THEN** an empty retained payload is published to that device's config
  topic

### Requirement: Telemetry publishes one retained state per device per cycle
For every cycle the system SHALL publish one retained state JSON per
(location, model) device that delivered a forecast, keyed by horizon and
carrying the parameter values plus the anchored `valid_at` timestamp. The
horizon anchor SHALL be the next full grid hour at or after tick + horizon.
Devices whose fetch failed or never answered SHALL NOT be re-published that
cycle.

#### Scenario: Horizon values are anchored forward
- **WHEN** the cycle tick is 19:31 and the +24 h horizon is selected
- **THEN** the published value is the grid point of the next full hour ≥
  19:31 + 24 h and its `valid_at` is part of the payload

#### Scenario: Failed models keep their last retained state
- **WHEN** a model's fetch fails in a cycle
- **THEN** no state is published for that device in that cycle

### Requirement: Missing values surface as unavailable, never as stale data
Every component SHALL become unavailable when (a) the service availability
topic reads `offline`, or (b) its value is absent in the state JSON (e.g.
beyond the model's horizon), or (c) no state update arrived within twice the
poll interval (`expire_after`). Entities MUST NOT disappear from HA because a
value is missing.

#### Scenario: Beyond-horizon component is unavailable
- **WHEN** `meteoswiss_icon_ch1` provides no +72 h values
- **THEN** its `+72 h` components are unavailable while its near-horizon
  components stay available

#### Scenario: Silent model expires
- **WHEN** a device receives no state update for two poll intervals
- **THEN** its components become unavailable without any explicit publish
