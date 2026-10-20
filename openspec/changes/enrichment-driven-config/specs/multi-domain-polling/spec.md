## MODIFIED Requirements

### Requirement: PollWorker fetches resolved domains
PollWorker SHALL fetch only the domains resolved from enabled enrichments, not a hardcoded or config-driven domain list. The resolved set SHALL be injected at construction.

#### Scenario: Only 2 domains resolved
- **WHEN** the resolved set is {Weather, AirQuality}
- **THEN** PollWorker SHALL send FetchDomain for Weather and AirQuality only, not Flood or Marine
