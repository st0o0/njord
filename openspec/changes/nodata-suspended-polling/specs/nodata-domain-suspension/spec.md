## ADDED Requirements

### Requirement: The gateway classifies empty API responses as NoData
When a domain client returns a successful HTTP response that maps to no usable forecast data (`FetchEmpty`), the gateway SHALL report the failure with reason `NoData` instead of `MalformedPayload`. The `MalformedPayload` reason SHALL be reserved for responses that cannot be pattern-matched to any known result type.

#### Scenario: Flood API returns all-null values
- **WHEN** the Flood API responds with HTTP 200 and all discharge values are null
- **THEN** the gateway SHALL send a `FetchDomainFailed` with reason `NoData` to the poll worker

#### Scenario: Genuinely unparseable response
- **WHEN** a domain client returns a result that is neither `FetchSuccess`, `FetchError`, nor `FetchEmpty`
- **THEN** the gateway SHALL send a `FetchDomainFailed` with reason `MalformedPayload`

### Requirement: NoData suspends the domain with a 24-hour recheck
When a poll worker receives a `NoData` failure for a domain, it SHALL transition that domain to the `Suspended` phase and schedule the next poll 24 hours later. The consecutive-failure count SHALL NOT be incremented.

#### Scenario: First NoData for a domain
- **WHEN** a domain that was in Discovery phase receives a `NoData` failure
- **THEN** the domain SHALL enter the `Suspended` phase
- **AND** the next poll SHALL be scheduled 24 hours later
- **AND** the consecutive-failure count SHALL remain at zero

#### Scenario: Repeated NoData keeps the domain suspended
- **WHEN** a suspended domain's 24-hour recheck again receives `NoData`
- **THEN** the domain SHALL remain in the `Suspended` phase
- **AND** the next poll SHALL be scheduled 24 hours later

#### Scenario: NoData does not affect other domains
- **WHEN** Flood is suspended for a location
- **THEN** the Forecast, Ensemble, AirQuality, Marine, and Seasonal poll schedules for that location SHALL be unchanged

### Requirement: A suspended domain recovers when data becomes available
If a 24-hour recheck returns data (the fetch succeeds), the domain SHALL leave the `Suspended` phase and resume its normal Discovery cycle to learn the update interval from scratch.

#### Scenario: Recheck returns data
- **WHEN** a suspended domain's 24-hour recheck returns a successful fetch with changed data
- **THEN** the domain SHALL transition to the Discovery phase
- **AND** the poll worker SHALL schedule the next poll at the discovery interval

### Requirement: NoData is logged at Info level
The poll worker SHALL log `NoData` outcomes at Info level, not Warning. The log message SHALL include the location, domain, and indicate that no data is available.

#### Scenario: NoData log output
- **WHEN** the poll worker receives a `NoData` failure for Barcelona/Flood
- **THEN** the log entry SHALL be at Info level
- **AND** the message SHALL indicate that the domain has no data for this location

### Requirement: The suspended state survives restarts
The `Suspended` phase and the `NoData` outcome SHALL be persisted so that a recovered poll worker does not reset a suspended domain back to Discovery and immediately retry.

#### Scenario: Recovery of a suspended domain
- **WHEN** a poll worker recovers and a domain was persisted as `Suspended` with a `NextPollAt` 20 hours in the future
- **THEN** the domain SHALL remain `Suspended`
- **AND** the next poll SHALL fire at the persisted time (not immediately)

#### Scenario: Old records without Suspended phase
- **WHEN** a poll worker recovers a domain state record that predates this change
- **THEN** the domain SHALL recover in its original phase (Discovery or Steady) with no change in behavior
