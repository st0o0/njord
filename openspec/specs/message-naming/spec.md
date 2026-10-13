# message-naming Specification

## Purpose

Consistent naming convention for request/response message records — failure responses match their command name.

## Requirements

### Requirement: Failure responses follow Request*Failed naming
Failure response records SHALL be named `Request<Noun>Failed` when they
respond to a `Request<Noun>` command, matching the established
`Request<Noun>Response` / `Request<Noun>Failed` convention.

#### Scenario: ForecastStream failure follows naming convention
- **GIVEN** a `RequestForecastStream` command fails
- **WHEN** the failure is communicated back to the requester
- **THEN** the failure record is `RequestForecastStreamFailed` (not `ForecastStreamFailed`)
