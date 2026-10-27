## ADDED Requirements

### Requirement: SmogRisk triggers on low boundary layer combined with poor air quality
The system SHALL provide a `SmogRisk` alert in `AirQuality` that fires when `boundary_layer_height` drops below a threshold AND `PM2.5` exceeds a threshold, indicating pollutant trapping.

#### Scenario: Smog conditions
- **WHEN** `boundary_layer_height` < 500 m AND `PM2.5` > 25 ug/m3
- **THEN** the alert SHALL fire with severity based on how low the boundary layer is and how high PM2.5 is

#### Scenario: Low boundary layer but clean air
- **WHEN** `boundary_layer_height` < 500 m but `PM2.5` < 25 ug/m3
- **THEN** the alert SHALL not fire

#### Scenario: High PM2.5 but good mixing
- **WHEN** `PM2.5` > 25 ug/m3 but `boundary_layer_height` > 500 m
- **THEN** the alert SHALL not fire

### Requirement: SmogRisk is a cross-domain feature
SmogRisk SHALL declare requirements for both Weather (`BoundaryLayerHeight`) and AirQuality (`Pm25`) domains. It SHALL not require Ensemble (local phenomenon, confidence always Medium).

#### Scenario: Cross-domain requirements
- **WHEN** SmogRisk is enabled
- **THEN** both Weather and AirQuality domains SHALL be polled

### Requirement: SmogRisk config
`SmogRiskConfig` SHALL have `Enabled` (default false), `BoundaryLayerThresholdM` (default 500), `Pm25ThresholdUgm3` (default 25.0).

#### Scenario: Custom thresholds
- **WHEN** the operator sets `BoundaryLayerThresholdM=300`
- **THEN** smog risk SHALL trigger only below 300 m boundary layer height
