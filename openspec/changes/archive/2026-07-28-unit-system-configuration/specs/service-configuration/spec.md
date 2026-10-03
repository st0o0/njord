## ADDED Requirements

### Requirement: UnitSystem configuration option
`NjordOptions` SHALL include a `UnitSystem` property of type `UnitSystem` enum (values: `Metric`, `Imperial`), defaulting to `Metric`. The value SHALL be configurable via `Njord:UnitSystem` in any configuration source (appsettings, environment variables, runtime config).

#### Scenario: Default is Metric
- **WHEN** no `Njord:UnitSystem` value is configured
- **THEN** the effective unit system is `Metric`

#### Scenario: Imperial via environment variable
- **WHEN** `Njord__UnitSystem` is set to `Imperial`
- **THEN** the effective unit system is `Imperial`

#### Scenario: Invalid value is rejected at startup
- **WHEN** `Njord:UnitSystem` is set to `CustomMetric`
- **THEN** startup validation fails naming the invalid unit system value and listing valid options
