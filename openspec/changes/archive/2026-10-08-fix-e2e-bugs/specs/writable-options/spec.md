## MODIFIED Requirements

### Requirement: Override file uses section-wrapped JSON
The override file SHALL wrap all persisted options under the `"Njord"` section key, matching the `NjordOptions.SectionName` binding. This ensures the persisted keys align with `IConfiguration` section binding and override environment variables (which bind to the same `Njord:*` key path). The override file configuration source SHALL have higher priority than environment variables so that array properties (e.g. `Horizons`, threshold lists) are fully replaced rather than merged by index.

#### Scenario: Section wrapper matches configuration binding
- **WHEN** `data/appsettings.Override.json` contains `{"Njord": {"Horizons": [6, 24]}}`
- **AND** environment variable `Njord__Horizons__0=3` is set
- **THEN** `IOptionsMonitor<NjordOptions>.CurrentValue.Horizons` SHALL be `[6, 24]` (file overrides env var)

#### Scenario: Array replacement on successive mutations
- **WHEN** environment variables define `Njord__Horizons__0=3, __1=6, __2=12, __3=24, __4=48, __5=72`
- **AND** a mutation sets `Horizons = [6, 24]`
- **AND** a subsequent `GetConfig` is called
- **THEN** the returned `horizons` SHALL be exactly `[6, 24]` with no duplicated entries from the env vars

#### Scenario: Non-array scalar properties still merge correctly
- **WHEN** environment variable `Njord__ForecastDays=4` is set
- **AND** a mutation sets `PollInterval = 1800s` (without touching ForecastDays)
- **THEN** `ForecastDays` SHALL remain `4` and `PollInterval` SHALL be `1800s`
