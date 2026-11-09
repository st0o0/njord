# v3-proto Specification

## Purpose
The njord proto package with unified NjordService, generic value messages, and entity-first structure.

## Requirements

### Requirement: Proto package with unified NjordService
The system SHALL define a `njord` proto package (file at `protos/njord/njord.proto`) containing a single `NjordService` with four RPCs: `GetManifest`, `Subscribe`, `GetStatus`, and `TriggerPoll`. The `csharp_namespace` SHALL be `Njord.Grpc.Proto`.

#### Scenario: NjordService exposes all RPCs
- **WHEN** a client connects to the NjordService
- **THEN** it SHALL be able to call GetManifest (unary), Subscribe (server-streaming), GetStatus (unary), and TriggerPoll (unary)

#### Scenario: Proto package is unversioned
- **WHEN** the proto file is compiled
- **THEN** the generated C# types SHALL live in the `Njord.Grpc.Proto` namespace

#### Scenario: Proto file location is unversioned
- **WHEN** the proto file is referenced
- **THEN** it SHALL be at `protos/njord/njord.proto`, not under a versioned subdirectory

### Requirement: Generic value types per OutputKind
The proto SHALL define one value message per `OutputKind` rather than one per feature. The value types SHALL be: `WeatherValue`, `AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `ObservationValue`. Adding a new feature with an existing OutputKind SHALL NOT require a proto change.

#### Scenario: New alert feature needs no proto change
- **WHEN** a new HailAlert feature is added with `OutputKind.Alert`
- **THEN** its results SHALL be transported as an `AlertValue` without modifying the proto definition

#### Scenario: AlertValue carries severity and trigger
- **WHEN** an alert result is mapped to proto
- **THEN** `AlertValue` SHALL contain at minimum `severity` (enum), `trigger` (human-readable string), and `peak_value` (float)

#### Scenario: ScoreValue carries numeric value and label
- **WHEN** a score result is mapped to proto
- **THEN** `ScoreValue` SHALL contain at minimum `value` (float) and `label` (string, e.g. "Good", "Poor")

#### Scenario: ObservationValue carries raw numeric
- **WHEN** an observation result is mapped to proto
- **THEN** `ObservationValue` SHALL contain `value` (double)
