## ADDED Requirements

### Requirement: Pollen alert fires on high pollen count
The PollenAlert rule SHALL evaluate pollen values from AirQualityPoint and raise an alert when any pollen type exceeds a configured threshold.

#### Scenario: High birch pollen triggers alert
- **WHEN** BirchPollen exceeds the threshold in the next 24 hours
- **THEN** an AlertRaised event SHALL be emitted with the pollen type and peak value

### Requirement: PM2.5 alert fires above WHO guideline
The Pm25Alert rule SHALL raise an alert when PM2.5 exceeds 15 µg/m³ (WHO guideline).

#### Scenario: Poor air quality triggers alert
- **WHEN** PM2.5 exceeds 15 µg/m³ in the forecast
- **THEN** an AlertRaised event SHALL be emitted with severity based on concentration level
