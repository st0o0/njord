## ADDED Requirements

### Requirement: Protobuf oneof with typed feature messages
The proto contract SHALL define a `oneof feature` in the enrichment event message, with one typed message per feature. Each feature message SHALL embed a base message per `OutputKind` (`AlertData`, `ScoreData`, `ConditionData`, `TrendData`, `ForecastData`) plus feature-specific fields.

#### Scenario: Alert feature has typed proto message
- **WHEN** `FrostAlert` produces a result
- **THEN** the gRPC layer SHALL emit a `FrostAlert` proto message containing `AlertData` (severity, starts_at, ends_at) plus `min_temperature` and `dew_point`

#### Scenario: Score feature has typed proto message
- **WHEN** `BbqScore` produces a result
- **THEN** the gRPC layer SHALL emit a `BbqScore` proto message containing `ScoreData` (score, best_start, best_hours)

### Requirement: No string identifier on the wire
The proto contract SHALL NOT carry `type_name`, `feature_name`, or any string identifier for the feature. The proto field name within the `oneof` IS the wire identity.

#### Scenario: EnrichmentEvent has no type_name field
- **WHEN** an enrichment event is serialized
- **THEN** the proto message SHALL NOT contain a `type_name` string field

#### Scenario: Consumer identifies feature by oneof field
- **WHEN** a consumer receives an enrichment event
- **THEN** it SHALL identify the feature by which `oneof` field is set (e.g. `frost`, `bbq`, `forecast_drift`)

### Requirement: gRPC mapping via pattern match
The gRPC mapping layer SHALL convert `FeatureResult` subtypes to their corresponding proto messages via C# pattern matching on the concrete result type. No string dispatch, no enum dispatch.

#### Scenario: FrostAlertResult maps to FrostAlert proto
- **WHEN** `FrostAlertResult` is produced by the engine
- **THEN** the mapping layer SHALL pattern-match it as `FrostAlertResult` and construct the `FrostAlert` proto message

### Requirement: Unknown features ignored by old consumers
New feature messages added to the `oneof` SHALL be silently ignored by consumers compiled against an older proto version. This is guaranteed by protobuf's forward-compatible `oneof` semantics.

#### Scenario: Old ha-njord ignores new feature
- **WHEN** njord adds a new feature proto message to the `oneof`
- **THEN** an older ha-njord build SHALL deserialize the event without error, with the new field absent
