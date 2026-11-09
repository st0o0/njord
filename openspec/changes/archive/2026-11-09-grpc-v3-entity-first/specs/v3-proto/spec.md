## ADDED Requirements

### Requirement: v3 proto package with unified NjordService
The system SHALL define a `njord.v3` proto package containing a single `NjordService` with four RPCs: `GetManifest`, `Subscribe`, `GetStatus`, and `TriggerPoll`. This replaces the v2 split of `WeatherService` and `OpsService`.

#### Scenario: v3 service exposes all RPCs
- **WHEN** a client connects to the v3 NjordService
- **THEN** it SHALL be able to call GetManifest (unary), Subscribe (server-streaming), GetStatus (unary), and TriggerPoll (unary)

### Requirement: v3 runs alongside v2
The v3 proto package SHALL coexist with v2. Both services SHALL be registered on the same Kestrel endpoint. v2 SHALL remain fully functional until explicitly removed in a future change.

#### Scenario: Both versions serve simultaneously
- **WHEN** njord starts with both v2 and v3 registered
- **THEN** a v2 client calling `WeatherService.GetCatalog` and a v3 client calling `NjordService.GetManifest` SHALL both succeed

### Requirement: Generic value types per OutputKind
The v3 proto SHALL define one value message per `OutputKind` rather than one per feature. The value types SHALL be: `WeatherValue`, `AlertValue`, `ScoreValue`, `ConditionValue`, `TrendValue`, `ForecastValue`, `ObservationValue`. Adding a new feature with an existing OutputKind SHALL NOT require a proto change.

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

### Requirement: GetStatus and TriggerPoll preserve v2 semantics
`GetStatus` SHALL return the same information as v2 OpsService.GetStatus (version, uptime, budget, poll models, active enrichments). `TriggerPoll` SHALL accept an optional location and trigger an immediate poll, same as v2.

#### Scenario: v3 GetStatus matches v2 content
- **WHEN** both v2 `OpsService.GetStatus` and v3 `NjordService.GetStatus` are called
- **THEN** they SHALL return semantically equivalent information
