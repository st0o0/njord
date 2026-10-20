## Purpose

Defines the conventions for actor messages: naming, response hierarchies with typed success and failure subtypes, handshake failure naming, and the organization of Njord.Messages into domain namespaces with one file per command or query.

## Requirements

### Requirement: Handshake failures follow Request*Failed naming
Failure response records SHALL be named `Request<Noun>Failed` when they respond to a `Request<Noun>` command, matching the established `Request<Noun>Response` / `Request<Noun>Completed` / `Request<Noun>Failed` convention.

#### Scenario: ForecastSink handshake failure
- **WHEN** a `RequestForecastSink` command fails
- **THEN** the failure record is `RequestForecastSinkFailed` (not `ForecastSinkFailed`)

### Requirement: Failed carries exception
Every `*Failed` response record SHALL carry the caught `Exception` as a `Cause` property.

#### Scenario: Actor encounters exception during handling
- **WHEN** an actor encounters an exception while handling a query or command
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`

