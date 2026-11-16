# content-hash-change-detection Specification

## ADDED Requirements

### Requirement: Domain forecast change detection uses a deterministic content hash
The system SHALL detect data changes between polls using a content-based hash that is deterministic for identical data. The hash SHALL include the domain kind, time resolution, and all forecast points. The hash SHALL NOT include the model run timestamp or any reference-based fields.

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

### Requirement: ModelRun reflects the forecast data origin, not the fetch time
Each domain client SHALL set `ModelRun` to a value derived from the forecast data itself (the first point's `ValidFrom` truncated to the hour) rather than the current wall-clock time. This makes the field meaningful for diagnostics without affecting change detection.

#### Scenario: ModelRun is derived from forecast data
- **WHEN** a forecast response contains hourly points starting at 2026-11-16T18:00:00Z
- **THEN** the `DomainForecast.ModelRun` SHALL be 2026-11-16T18:00:00Z (not the fetch timestamp)

#### Scenario: ModelRun changes only when the API delivers a new model run
- **WHEN** two consecutive polls return data starting at the same hour
- **THEN** both `DomainForecast.ModelRun` values SHALL be identical
