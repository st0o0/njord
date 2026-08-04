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

### Requirement: Enrichment payload messages
`common.proto` SHALL define all enrichment payload messages identical to v1: `AlertUpdate`, `Alert`, `AlertType`, `AlertSeverity`, `IndexUpdate`, `ParameterTrend`, `TrendUpdate`, `HorizonDerived`, `ScalarDerived`, `DerivedUpdate`, `ModelMetrics`, `HistoryUpdate`, `HorizonConsensus`, `ParameterConsensus`, `ConsensusUpdate`.

#### Scenario: All enrichment types compile
- **WHEN** `dotnet build` runs
- **THEN** all enrichment message types SHALL be generated in `Njord.Grpc.V2`

#### Scenario: Enrichment messages are field-identical to v1
- **WHEN** comparing v1 and v2 enrichment messages
- **THEN** every field name, number, and type SHALL match (only the namespace changes)
