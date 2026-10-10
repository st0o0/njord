# mqtt-egress Specification

## Purpose

MQTT egress to Home Assistant: an actor-owned broker connection lifecycle with a Last Will availability topic, device-based MQTT Discovery (gated by `DiscoveryEnabled`) for the static config-derived entity grid, per-horizon retained telemetry state topics with flat JSON payloads, and declarative mapping of missing values to `unavailable` so entities never disappear or go stale.
## Requirements
### Requirement: Missing values surface as unavailable, never as stale data
Every component SHALL become unavailable when (a) the service availability
topic reads `offline`, or (b) its value is absent in the state JSON, or
(c) no state update arrived within twice the poll interval (`expire_after`).
With discovery removed, the MQTT consumer SHALL publish `unavailable` state
when a feature produces no result for a poll cycle.

#### Scenario: Unsupported parameter has no sensor
- **WHEN** `icon_d2` does not support `precipitation_probability`
- **THEN** no sensor component for `precipitation_probability` exists in the state payload for icon_d2

#### Scenario: Out-of-range horizon has no sensor
- **WHEN** `icon_d2` has MaxForecastHours=48
- **THEN** no sensor components for horizon h72 exist in the state payload for icon_d2

#### Scenario: Silent model expires
- **WHEN** a device receives no state update for two poll intervals
- **THEN** its components become unavailable without any explicit publish

#### Scenario: Feature produces no result
- **WHEN** a poll cycle completes but a feature produces no result (e.g. trends
  on the first cycle)
- **THEN** the MQTT consumer publishes an `unavailable` state for that feature's
  entities

#### Scenario: Stale state expiry
- **WHEN** no poll cycle completes within 2× the poll interval
- **THEN** Home Assistant marks the entities as unavailable via `expire_after`

