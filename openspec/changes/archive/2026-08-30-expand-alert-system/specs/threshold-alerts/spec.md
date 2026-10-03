## MODIFIED Requirements

### Requirement: Alert is a typed record with confidence and severity
An `Alert` SHALL be a record carrying `AlertType` (enum), `Severity` (enum: None/Yellow/Orange/Red), `Confidence` (0.0-1.0, fraction of models agreeing the threshold is crossed), and an `IReadOnlyDictionary<string, object?>` of diagnostic attributes (e.g. expected value, earliest time, worst model). `AlertType` SHALL enumerate: Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity.

#### Scenario: Alert carries all fields
- **WHEN** a frost alert is created with severity Yellow, confidence 0.75, and attributes {expected_low: -2.1, earliest: "2026-07-14T04:00Z"}
- **THEN** the record exposes Type=Frost, Severity=Yellow, Confidence=0.75, and the attributes dictionary

#### Scenario: New alert type carries all fields
- **WHEN** an ice alert is created with severity Orange, confidence 0.9, and attributes {expected_low: -1.0, rain_hours: 4, soil_frozen: true}
- **THEN** the record exposes Type=Ice, Severity=Orange, Confidence=0.9, and the attributes dictionary

### Requirement: AlertResult aggregates all alerts for a location

`AlertEvaluator.EvaluateAll` SHALL accept a `ConsensusSnapshot` instead of `ModelSnapshot`. The location is taken from `ConsensusSnapshot.Location`. The result SHALL contain exactly 14 alerts (one per AlertType).

#### Scenario: Serialization to MQTT messages
- **WHEN** alerts are serialized
- **THEN** each alert produces one MQTT message on its sub-topic

#### Scenario: None severity still publishes
- **WHEN** no threshold is exceeded for an alert type
- **THEN** a "none" severity alert is published

#### Scenario: All 14 alert types present
- **WHEN** `EvaluateAll` is called
- **THEN** the result contains exactly 14 alerts, one for each AlertType value

### Requirement: Alert thresholds are configurable
All alert thresholds SHALL be configurable via `AlertOptions` bound from `NjordOptions.Enrichment.Alerts`. Defaults: frost [0, -5, -15] C, heat [30, 35, 40] C, storm [17, 25, 33] m/s, heavy rain hourly 10 mm / daily 25 mm, pressure drop 5 / 10 hPa, ice 2.0 C, wind chill [-10, -20, -30] C, visibility [1000, 200, 50] m, tropical night [20, 23, 25] C, humidity [16, 21, 24] C, fog persistent hours 4. An `Enabled` flag (default `true`) SHALL gate the entire alert consumer.

#### Scenario: Custom frost thresholds
- **WHEN** `AlertOptions.FrostThresholds` is set to [2, 0, -10]
- **THEN** the frost evaluator uses those thresholds for tiered severity

#### Scenario: Alerts disabled
- **WHEN** `AlertOptions.Enabled` is `false`
- **THEN** no alert consumer stream is materialized

### Requirement: Discovery payload for the alerts device
When `DiscoveryEnabled` is `true` and alerts are enabled, one retained device-based discovery payload SHALL be published per location for the alerts device. Each alert type SHALL be a `binary_sensor` component (on when severity > None) with JSON attributes for severity, confidence, and diagnostics.

#### Scenario: Alert discovery component count
- **WHEN** the alerts discovery payload for a location is built
- **THEN** it contains 14 binary_sensor components (frost, heat, storm, heavy_rain, uv, fog, snow, pressure_drop, thunderstorm, ice, wind_chill, visibility, tropical_night, humidity)

#### Scenario: Binary sensor is on when alert is active
- **WHEN** the frost alert has severity Yellow
- **THEN** the binary_sensor value template evaluates to "ON"

### Requirement: Alert record serialization
`AlertResult` and `Alert` records SHALL have `[property: JsonProperty("...")]` on all positional parameters producing camelCase wire names. The `Alert` record's `Attributes` dictionary property SHALL retain its `IReadOnlyDictionary<string, object?>` type with a pinned wire name. New AlertType enum values SHALL serialize as their string names.

#### Scenario: AlertResult round-trips through JSON with pinned wire names
- **WHEN** an `AlertResult` with all 14 alerts is serialized to JSON and deserialized back
- **THEN** all properties round-trip correctly with camelCase wire names including new alert types

#### Scenario: Old snapshot without new alert types
- **WHEN** a persisted snapshot contains only 9 alert types (pre-expansion)
- **THEN** deserialization succeeds and the missing 5 types are filled with `Alert.None`
