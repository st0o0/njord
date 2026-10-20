# slow-domain Specification

## Purpose
TBD - created by archiving change climate-anomaly. Update Purpose after archive.
## Requirements
### Requirement: Slow-fetch data sources cache independently of the poll cycle
Data sources that update infrequently (Climate, future: Seasonal) SHALL fetch on their own schedule (startup + daily refresh) independent of the PollWorker cycle. Their data SHALL be cached and available to handlers that trigger on fast domains.

#### Scenario: Climate data available without being a polled domain
- **WHEN** the Climate baseline is cached and Weather data arrives
- **THEN** ClimateAnomalyHandler SHALL access the baseline without a Climate domain poll

