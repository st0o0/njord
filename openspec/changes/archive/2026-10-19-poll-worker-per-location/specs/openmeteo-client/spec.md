## MODIFIED Requirements

### Requirement: OpenMeteoClient supports multiple endpoints
OpenMeteoClient SHALL support building request URLs and parsing responses for: forecast (existing), ensemble (new), air-quality (new), flood (new). Solar and marine endpoints are future additions.

#### Scenario: Ensemble endpoint URL
- **WHEN** an Ensemble fetch for Lucerne (47.05, 8.31) is requested
- **THEN** the URL SHALL target /v1/ensemble with latitude=47.05&longitude=8.31 and the configured parameters
