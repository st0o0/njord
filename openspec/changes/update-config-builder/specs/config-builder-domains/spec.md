## ADDED Requirements

### Requirement: Config builder uses domain-based configuration
The config builder SHALL present enabled domains as checkboxes instead of a model list. Weather SHALL always be enabled. AirQuality, Flood, and Marine SHALL be optional.

#### Scenario: Default configuration
- **WHEN** the builder is loaded with defaults
- **THEN** Weather, AirQuality, and Flood SHALL be checked, Marine unchecked

### Requirement: Budget shows domain-based projection
The builder SHALL calculate monthly API usage as `enabled_domains × locations × cycles_per_day × 30`.

#### Scenario: Default budget
- **WHEN** 1 location with Weather, AirQuality, Flood at 60min interval
- **THEN** monthly projection SHALL show ~2,160 calls (3 × 24 × 30)

### Requirement: Location entry supports name-only
The builder SHALL accept a location name without coordinates. It SHALL indicate that coordinates are resolved automatically via Geocoding.

#### Scenario: Name-only location
- **WHEN** user enters "Lucerne" without lat/lon
- **THEN** the generated config SHALL have Name: "Lucerne" without Latitude/Longitude
