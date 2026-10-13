## Purpose

Defines test coverage gaps to close: IndexAnalyzer sensor-override tests with real sensor data, and the requirement that tests pass reliably without flakiness.

## Requirements

### Requirement: IndexAnalyzer sensor-override is tested with real sensor data

`IndexAnalyzer.OverrideIndoorTemp` SHALL be tested with non-null sensor
snapshots to verify the sensor fallback chain (live reading → config →
hardcoded default).

#### Scenario: Sensor override applies indoor temperature
- **WHEN** IndexAnalyzer runs with a sensor snapshot containing an indoor temperature reading
- **THEN** the indoor temperature in the computation SHALL use the sensor value instead of the config default

#### Scenario: Sensor override filters by location
- **WHEN** IndexAnalyzer runs with sensor data for a different location
- **THEN** the sensor override SHALL NOT apply to the current location

### Requirement: No flaky tests

All tests SHALL pass reliably on repeated runs. A test that intermittently
fails SHALL be identified and stabilized.

#### Scenario: Core.Tests reliability
- **WHEN** `Njord.Core.Tests` is run 3 times consecutively
- **THEN** all runs SHALL produce identical pass/fail results with zero failures
