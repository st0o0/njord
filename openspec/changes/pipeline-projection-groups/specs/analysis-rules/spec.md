## MODIFIED Requirements

### Requirement: Alert rules work on DomainForecast instead of LocationConsensus
All alert rules SHALL accept DomainForecast<WeatherPoint> as input instead of LocationConsensus. Rules SHALL use ForecastQueries.Window to scan the alert time window and ForecastQueries.ValueAt for point-in-time checks.

#### Scenario: Frost rule scans weather forecast window
- **WHEN** FrostAlert evaluates a DomainForecast<WeatherPoint>
- **THEN** it SHALL scan the next 24h of WeatherPoints for temperature below the configured threshold
