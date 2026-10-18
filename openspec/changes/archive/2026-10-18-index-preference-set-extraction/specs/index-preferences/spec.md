## MODIFIED Requirements

### Requirement: Index config updates use wrapped preferences

`IndexConfigUpdate` SHALL be `(bool? Enabled, IndexPreferenceValues? Preferences)`. A null `Preferences` means "no preference changes". The existing `IndexPreferenceValues` record (all 11 `double?` fields) serves as the override type — no separate overrides record needed.

#### Scenario: Full config update with preference changes
- **WHEN** a config update carries `Enabled = true` and `Preferences` with `IdealOutdoorTemp = 22.0`
- **THEN** both the enabled flag and the ideal outdoor temp SHALL be applied

#### Scenario: Config update with only Enabled change
- **WHEN** a config update carries `Enabled = false` and `Preferences = null`
- **THEN** only the enabled flag SHALL change; preferences remain at their current values

#### Scenario: Partial preference update
- **WHEN** a config update carries `Preferences` with `HeatSensitivity = 0.8` and all other fields null
- **THEN** only the heat sensitivity SHALL change; other preferences remain unchanged

#### Scenario: Wire format unchanged
- **WHEN** an index config update is sent via gRPC
- **THEN** the proto message shape SHALL be identical to the current format
