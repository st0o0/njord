# Capability: grpc-v2-common

## Purpose

Shared proto types for the v2 gRPC API — LocationInfo, ModelInfo, CoverageTier, forecast points with Timestamp fields, and all enrichment payload messages.

## Requirements

### Requirement: common.proto defines shared types in njord.v2 package
`protos/njord/v2/common.proto` SHALL define all shared message types used across services. The package SHALL be `njord.v2` with `csharp_namespace = "Njord.Grpc.V2"`. It SHALL import `google/protobuf/timestamp.proto`.

#### Scenario: Proto compiles independently
- **WHEN** `common.proto` is compiled
- **THEN** all shared types SHALL be generated in namespace `Njord.Grpc.V2` without errors

### Requirement: LocationInfo message
`common.proto` SHALL define a `LocationInfo` message with fields: `string name`, `double latitude`, `double longitude`, `repeated string models`.

#### Scenario: LocationInfo carries resolved models
- **WHEN** a location has per-location model overrides merged with defaults
- **THEN** `LocationInfo.models` SHALL contain the resolved deduplicated model list

### Requirement: ModelInfo message
`common.proto` SHALL define a `ModelInfo` message with fields: `string id`, `string display_name`, `string provider`, `string region`, `CoverageTier coverage_tier`, `optional int32 max_forecast_hours`, `optional double resolution_km`, `optional string description`.

#### Scenario: ModelInfo provides static metadata
- **WHEN** a model has coverage registry data
- **THEN** `ModelInfo` SHALL contain display name, provider, region, and coverage tier

### Requirement: CoverageTier enum
`common.proto` SHALL define a `CoverageTier` enum with values `COVERAGE_TIER_UNSPECIFIED = 0`, `COVERAGE_TIER_GLOBAL = 1`, `COVERAGE_TIER_CONTINENTAL = 2`, `COVERAGE_TIER_REGIONAL = 3`.

#### Scenario: Enum values match v1
- **WHEN** proto is compiled
- **THEN** the three named coverage tiers SHALL have the same numeric values as v1

### Requirement: HourlyForecast uses Timestamp for valid_at
`common.proto` SHALL define `HourlyForecast` with `google.protobuf.Timestamp valid_at` (replacing v1's `int64 timestamp`). All other fields (temperature, apparent_temperature, precipitation, humidity, wind_speed, wind_bearing, cloud_cover, weather_code, is_day, rain, wind_gusts, pressure_msl, repeated ParameterValue extra) SHALL be preserved.

#### Scenario: valid_at round-trips as Timestamp
- **WHEN** the server sets `valid_at` via `Timestamp.FromDateTimeOffset()`
- **THEN** the client SHALL receive the same instant via `.ToDateTimeOffset()`

### Requirement: DailyForecast keeps string date
`common.proto` SHALL define `DailyForecast` with `string date` (ISO format "2026-07-28") for date-only values. All other fields (temperature_max, temperature_min, precipitation_sum, wind_speed_max, wind_gusts_max, sunrise, sunset, weather_code, repeated ParameterValue extra) SHALL be preserved.

#### Scenario: date is a calendar date string
- **WHEN** a daily forecast point is serialized
- **THEN** `date` SHALL be an ISO date string without time component

### Requirement: ParameterValue oneof message
`common.proto` SHALL define `ParameterValue` with `string name` and a `oneof value` containing `double numeric`, `string text`, `bool flag`.

#### Scenario: Extra parameters use oneof
- **WHEN** a forecast has non-fixed parameters
- **THEN** they SHALL be serialized as `ParameterValue` entries in the `extra` field

### Requirement: DayScoreSet message for per-day index scores

`common.proto` SHALL define a `DayScoreSet` message with fields: `int32 day_offset`, `int32 laundry`, `int32 outdoor`, `int32 running`, `int32 cycling`, `int32 bbq`, `int32 irrigation`, `int32 solar`, `int32 night_ventilation`, `int32 hours_included`, and optional `ScoreEnvelope` fields for each score (`laundry_envelope`, `outdoor_envelope`, `running_envelope`, `cycling_envelope`, `bbq_envelope`, `irrigation_envelope`, `solar_envelope`, `night_ventilation_envelope`).

#### Scenario: DayScoreSet carries all scores for one day

- **WHEN** a `DayScoreSet` with `day_offset = 1` is serialized
- **THEN** it SHALL contain all 8 score fields, `hours_included`, and up to 8 optional envelope fields

### Requirement: ScoreEnvelope sub-message

`common.proto` SHALL define a `ScoreEnvelope` message with fields: `int32 min`, `int32 max`, `double confidence`.

#### Scenario: ScoreEnvelope round-trips

- **WHEN** a `ScoreEnvelope` with min=65, max=80, confidence=0.85 is serialized and deserialized
- **THEN** all three fields SHALL round-trip correctly

### Requirement: FrostInfo sub-message

`common.proto` SHALL define a `FrostInfo` message with fields: `int32 hours_until_frost`, `double confidence`.

#### Scenario: FrostInfo carries countdown

- **WHEN** frost is detected 14 hours out with 0.75 confidence
- **THEN** `FrostInfo` SHALL have `hours_until_frost = 14` and `confidence = 0.75`

### Requirement: VpdInfo sub-message

`common.proto` SHALL define a `VpdInfo` message with fields: `double kpa`, `string category`.

#### Scenario: VpdInfo carries category and value

- **WHEN** VPD is 1.27 kPa (category "high")
- **THEN** `VpdInfo` SHALL have `kpa = 1.27` and `category = "high"`

### Requirement: Enrichment payload messages
`common.proto` SHALL define all enrichment payload messages: `AlertUpdate`, `Alert`, `AlertType`, `AlertSeverity`, `IndexUpdate`, `DayScoreSet`, `ScoreEnvelope`, `FrostInfo`, `VpdInfo`, `ParameterTrend`, `TrendUpdate`, `HorizonDerived`, `ScalarDerived`, `DerivedUpdate`, `ModelMetrics`, `HistoryUpdate`, `HorizonConsensus`, `ParameterConsensus`, `ConsensusUpdate`.

`IndexUpdate` SHALL contain: `repeated DayScoreSet days`, `optional FrostInfo frost`, `optional VpdInfo vpd`. It SHALL NOT contain flat score fields (`laundry`, `outdoor`, etc.), `ventilation`, `hdd`, `cdd`, `frost_hours`, `frost_confidence`, `vpd_kpa`, `vpd_category`, or any `reserved` statements.

#### Scenario: IndexUpdate contains daily slices

- **WHEN** an `IndexUpdate` is populated with 3 day slices
- **THEN** `days` SHALL contain 3 `DayScoreSet` entries with `day_offset` 0, 1, 2

#### Scenario: IndexUpdate without frost

- **WHEN** no frost is detected
- **THEN** `frost` field SHALL be absent (not set)

#### Scenario: No reserved fields in any message

- **WHEN** `common.proto` is inspected
- **THEN** it SHALL contain zero `reserved` statements and zero energy-removal comments
