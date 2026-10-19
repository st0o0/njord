## MODIFIED Requirements

### Requirement: PollWorker entity is per location
The PollWorker shard region SHALL use location name as entity-id (not "location|model"). Messages SHALL implement IWithLocationId. One PollWorker per configured location.

#### Scenario: Single worker per location
- **WHEN** location "Lucerne" is configured with Weather and AirQuality domains enabled
- **THEN** exactly one PollWorker entity SHALL exist for "Lucerne"
