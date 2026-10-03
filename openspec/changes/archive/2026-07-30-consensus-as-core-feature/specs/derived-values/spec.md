## MODIFIED Requirements

### Requirement: DerivedResult aggregates all derived values and serializes to MQTT

`DerivedResult.Compute` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. Beaufort, wind chill, dew-point comfort, WMO description, diurnal amplitude, sunshine percentage, and inversion detection SHALL all be computed from consensus median values at configured horizons.

#### Scenario: Horizon message content
- **WHEN** derived values are computed from `ConsensusSnapshot.Hourly` medians at horizon h6
- **THEN** the MQTT message for h6 contains beaufort, wind_chill, dewpoint_comfort, and wmo_description

#### Scenario: Meta message content
- **WHEN** derived scalar values are computed from `ConsensusSnapshot.Hourly`
- **THEN** the meta MQTT message contains diurnal_amplitude, sunshine_pct, and inversion

#### Scenario: Null values serialize as JSON null
- **WHEN** a consensus median is null for a parameter at a horizon
- **THEN** the derived value for that parameter is null in the JSON payload

### Requirement: DerivedResult serialization with pinned wire names

`DerivedResult` SHALL serialize and deserialize with pinned `[JsonProperty]` wire names that MUST NOT change.

#### Scenario: DerivedResult round-trips through JSON
- **WHEN** a `DerivedResult` is serialized and deserialized
- **THEN** all fields are preserved with their pinned wire names
