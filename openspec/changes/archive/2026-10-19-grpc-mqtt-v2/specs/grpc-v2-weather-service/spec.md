## MODIFIED Requirements

### Requirement: GetForecast returns ensemble data
GetForecast SHALL return ensemble mean values with standard deviation instead of per-model consensus. Optional per-model forecasts MAY be included if individual models are configured.

#### Scenario: Ensemble forecast response
- **WHEN** a client calls GetForecast for location "Lucerne"
- **THEN** the response SHALL contain ensemble mean and std dev for each parameter and horizon

### Requirement: GetEnrichments returns domain-aware enrichments
GetEnrichments SHALL return alerts from all domains (weather alerts, air quality alerts, flood alerts) instead of only weather-derived enrichments.

#### Scenario: Cross-domain enrichments
- **WHEN** a client calls GetEnrichments for location "Lucerne"
- **THEN** the response SHALL include weather alerts AND air quality alerts if both domains are enabled
