## ADDED Requirements

### Requirement: PollWorker fetches all enabled domains per location
On each poll tick, PollWorker SHALL fetch all enabled domains (Weather, AirQuality, Flood, Solar, Marine) for its location in parallel. Each domain fetch SHALL be independently budget-checked. A failed domain fetch SHALL NOT prevent other domains from completing.

#### Scenario: Parallel domain fetching
- **WHEN** a poll tick fires for location "Lucerne" with Weather and AirQuality enabled
- **THEN** both fetches SHALL be initiated in parallel

#### Scenario: One domain fails, others succeed
- **WHEN** Weather fetch succeeds but AirQuality fetch fails
- **THEN** Weather data SHALL be emitted to the pipeline and AirQuality SHALL be retried next cycle
