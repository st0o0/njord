# persistence-records Specification

## Purpose

Record style and type conventions for Akka.NET persistence events and snapshots, following FunkArr patterns.

## Requirements

### Requirement: Persistence records use positional record style
All persistence event and snapshot records SHALL be positional records
(constructor parameters) without `[JsonProperty]` attributes. This matches
the FunkArr convention and eliminates init-property boilerplate.

#### Scenario: Event record is positional
- **GIVEN** the `ApiCallRecorded` persistence event
- **WHEN** its declaration is inspected
- **THEN** it is a positional record `ApiCallRecorded(int Weight, DateTimeOffset Utc)`

#### Scenario: Snapshot record is positional
- **GIVEN** the `PersistedBudgetTrackerState` snapshot
- **WHEN** its declaration is inspected
- **THEN** it is a positional record with no `[JsonProperty]` attributes

### Requirement: Persistence records use DateTimeOffset for timestamps
Persistence records SHALL use `DateTimeOffset` for timestamp fields and
`TimeSpan` for duration fields, not `long` ticks. Newtonsoft serializes
both types natively.

#### Scenario: Event timestamp is DateTimeOffset
- **GIVEN** a `PersistedDataChanged` event
- **WHEN** its `Utc` field is inspected
- **THEN** it is `DateTimeOffset` (not `long`)

#### Scenario: Poll entry timestamps are DateTimeOffset
- **GIVEN** a `PersistedModelPollEntry` record
- **WHEN** its change-tracking fields are inspected
- **THEN** `LastChangeUtc` and `PrevChangeUtc` are `DateTimeOffset?` and `Cycle` is `TimeSpan?`

#### Scenario: Serialized timestamp is ISO-8601
- **GIVEN** an `ApiCallRecorded` event with `Utc = 2026-07-15T10:00:00Z`
- **WHEN** serialized by Newtonsoft
- **THEN** the JSON contains an ISO-8601 string, not a numeric ticks value
