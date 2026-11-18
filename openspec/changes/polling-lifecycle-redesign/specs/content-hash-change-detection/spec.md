## MODIFIED Requirements

### Requirement: Domain forecast change detection uses a deterministic content hash
The system SHALL detect data changes between polls using a content-based hash that is deterministic for identical data. The hash SHALL include the domain kind, time resolution, and all forecast points. The hash SHALL NOT include the model run timestamp or any reference-based fields. ContentHash drives both pipeline dispatch (changed data enters the pipeline, unchanged data is skipped) and adaptive drift observations.

#### Scenario: Identical data across two polls produces the same hash
- **WHEN** a domain client fetches the same API data twice (same hourly timestamps, same values)
- **THEN** both `DomainForecast<T>` instances SHALL produce the same content hash

#### Scenario: Changed data produces a different hash
- **WHEN** a domain client fetches data where at least one point value differs from the previous poll
- **THEN** the content hash SHALL differ from the previous hash

#### Scenario: Different ModelRun timestamps do not affect the hash
- **WHEN** two `DomainForecast<T>` instances contain identical points but different `ModelRun` values
- **THEN** the content hash SHALL be identical

#### Scenario: The Gateway uses content hash for change detection
- **WHEN** `OpenMeteoGateway` builds a `FetchDomainCompleted` response
- **THEN** it SHALL use the content hash (not `GetHashCode()`) as the hash value
