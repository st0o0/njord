## ADDED Requirements

### Requirement: Weather alerts are a ProjectionGroup on DomainForecast<WeatherPoint>
A WeatherAlertProjectionGroup SHALL project DomainForecast<WeatherPoint> from the LocationForecast and evaluate all 14 weather alert rules against it. The projection SHALL include the previous weather forecast for trend-aware alerts.

#### Scenario: Frost alert on ensemble data
- **WHEN** Weather ensemble data arrives with mean temperature below 0°C in the next 24h
- **THEN** a FrostAlert enrichment event SHALL be produced with severity and confidence

### Requirement: Alert confidence derived from Ensemble StdDev
Alert confidence SHALL be derived from the Ensemble standard deviation at the alert's peak time. Low StdDev (models agree) SHALL produce high confidence. High StdDev (models disagree) SHALL produce low confidence.

#### Scenario: High confidence when StdDev is small
- **WHEN** the temperature at the frost peak hour has StdDev < 1.0°C
- **THEN** the alert confidence SHALL be High

#### Scenario: Low confidence when StdDev is large
- **WHEN** the temperature at the frost peak hour has StdDev > 3.0°C
- **THEN** the alert confidence SHALL be Low
