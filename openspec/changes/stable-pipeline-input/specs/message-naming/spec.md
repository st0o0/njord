## REMOVED Requirements

### Requirement: Failure responses follow Request*Failed naming
**Reason**: Its only example, `RequestForecastStream`, is removed.
**Migration**: See "Handshake failures follow Request*Failed naming".

## ADDED Requirements

### Requirement: Handshake failures follow Request*Failed naming
Failure response records SHALL be named `Request<Noun>Failed` when they respond to a `Request<Noun>` command, matching the established `Request<Noun>Response` / `Request<Noun>Failed` convention.

#### Scenario: ForecastSink failure follows naming convention
- **GIVEN** a `RequestForecastSink` command fails
- **WHEN** the failure is communicated back to the requester
- **THEN** the failure record is `RequestForecastSinkFailed` (not `ForecastSinkFailed`)
