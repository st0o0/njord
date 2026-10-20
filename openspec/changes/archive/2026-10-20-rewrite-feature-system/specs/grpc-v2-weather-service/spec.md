## MODIFIED Requirements

### Requirement: StreamEnrichments streams typed feature events
`WeatherService.StreamEnrichments` SHALL be a server-streaming RPC. Each event SHALL carry `string location`, `google.protobuf.Timestamp updated_at`, and a `oneof feature` with one typed message per feature (e.g. `FrostAlert frost`, `BbqScore bbq`). The `type_name` string field and the generic `AlertUpdate`/`LifestyleUpdate` payloads SHALL be removed.

#### Scenario: Stream emits typed frost alert
- **WHEN** the frost feature triggers for location "Zurich"
- **THEN** the event SHALL have `location = "Zurich"`, `updated_at` set, and `frost` field populated with `AlertData` plus `min_temperature` and `dew_point`

#### Scenario: Stream emits typed score
- **WHEN** the BBQ score updates for location "Zurich"
- **THEN** the event SHALL have `bbq` field populated with `ScoreData` containing `score`, `best_start`, `best_hours`

### Requirement: GetEnrichments returns typed feature results
`WeatherService.GetEnrichments` SHALL return enrichment data using the same typed feature messages as the stream. The response SHALL carry `string location` and a `oneof feature` per result (or repeated messages). The generic `AlertUpdate` and `repeated LifestyleUpdate` fields SHALL be removed.

#### Scenario: GetEnrichments returns typed results
- **WHEN** a client calls `GetEnrichments` for a location with frost alert active and BBQ score computed
- **THEN** the response SHALL contain typed `FrostAlert` and `BbqScore` messages
