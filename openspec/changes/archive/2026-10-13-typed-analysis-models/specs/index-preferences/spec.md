## MODIFIED Requirements

### Requirement: ResolvedPreferences record for scorer consumption
A `ResolvedPreferences` record SHALL be produced per (location, index kind) pair, where the location is a typed location identity and the index kind is a typed index kind. It SHALL contain all preference properties as non-nullable doubles with fully resolved values. Scorers SHALL accept `ResolvedPreferences` instead of raw config values. No scorer or index computation SHALL look up preferences by a string score name.

#### Scenario: Scorer receives resolved values
- **WHEN** the night-ventilation scorer is called
- **THEN** it receives a `ResolvedPreferences` with `IndoorTemp`, `HeatSensitivity`, `HumiditySensitivity`, `WindSensitivity`, `RainSensitivity` fully resolved

#### Scenario: Resolution key uses NightVentilation
- **WHEN** preferences are resolved for the ventilation scorer
- **THEN** the key SHALL be (Lucerne, NightVentilation)

#### Scenario: Resolution happens once at config load
- **WHEN** configuration is loaded or reloaded
- **THEN** a preference map keyed by (location, index kind) SHALL be computed and reused for all subsequent poll cycles until the next config change
