## REMOVED Requirements

### Requirement: Persistence records use DateTimeOffset for timestamps
**Reason**: Two scenarios named the removed `PersistedDataChanged` and `PersistedModelPollEntry`; the rule is restated with the poll worker's records.
**Migration**: See "Persistence records use DateTimeOffset and TimeSpan".

## ADDED Requirements

### Requirement: Persistence records use DateTimeOffset and TimeSpan
Persistence records SHALL use `DateTimeOffset` for timestamp fields and
`TimeSpan` for duration fields, not `long` ticks. Newtonsoft serializes
both types natively.

#### Scenario: Event timestamp is DateTimeOffset
- **GIVEN** a `ForecastChanged` event
- **WHEN** its `Utc` field is inspected
- **THEN** it is `DateTimeOffset` (not `long`)

#### Scenario: Poll worker snapshot timestamps are DateTimeOffset
- **GIVEN** a `PersistedPollWorkerState` record
- **WHEN** its change-tracking fields are inspected
- **THEN** `LastChangeUtc` and `PrevChangeUtc` are `DateTimeOffset?`, `NextPollUtc` is `DateTimeOffset`, and `Cycle` (`TimeSpan?`) and `DiscoveryInterval` (`TimeSpan`) are durations

#### Scenario: Serialized timestamp is ISO-8601
- **GIVEN** an `ApiCallRecorded` event with `Utc = 2026-07-15T10:00:00Z`
- **WHEN** serialized by Newtonsoft
- **THEN** the JSON contains an ISO-8601 string, not a numeric ticks value
