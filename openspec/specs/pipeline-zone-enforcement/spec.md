## Purpose

Defines how Njord.Pipeline is covered by zone architecture tests that allow only the analysis libraries as feature-library dependencies.

## Requirements

### Requirement: Pipeline is covered by zone architecture tests

`Njord.Pipeline` SHALL be included in zone-level lateral-independence
architecture tests. Because Pipeline orchestrates the four analysis
libraries, the test MUST use a custom variant that allows
`Njord.Alerts`, `Njord.Derived`, `Njord.Trends`, `Njord.Indices` as
permitted dependencies while still catching imports of other feature
libraries (`Njord.Mqtt`, `Njord.Grpc`, `Njord.OpenMeteo`,
`Njord.Budget`, `Njord.SensorHub`).

#### Scenario: Pipeline imports only allowed feature libraries
- **WHEN** `Njord.Pipeline` references another feature library
- **THEN** the architecture test SHALL pass only if the referenced library is one of: Alerts, Derived, Trends, Indices

#### Scenario: Pipeline imports a non-analysis feature library
- **WHEN** `Njord.Pipeline` adds a reference to `Njord.Mqtt`, `Njord.Grpc`, `Njord.OpenMeteo`, `Njord.Budget`, or `Njord.SensorHub`
- **THEN** the architecture test SHALL fail
