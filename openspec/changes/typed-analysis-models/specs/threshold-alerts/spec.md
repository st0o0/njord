## MODIFIED Requirements

### Requirement: Alert is a typed record with confidence and severity
An `Alert` SHALL be a record carrying `AlertType` (enum), `Severity` (enum: None/Yellow/Orange/Red), `Confidence` (0.0-1.0, fraction of models agreeing the threshold is crossed), and typed diagnostic details specific to its alert type (e.g. a frost alert carries expected low, earliest frost horizon and models agreeing). Diagnostics SHALL NOT be a string-keyed dictionary. The snake_case attribute keys published via MQTT (the gRPC `Alert` message carries no attributes) (e.g. `expected_low`, `earliest_frost`, `models_agreeing`) SHALL be produced from the typed details by the output mappers and SHALL be identical to the keys and values published before this change. `AlertType` SHALL enumerate: Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm, Ice, WindChill, Visibility, TropicalNight, Humidity.

#### Scenario: Alert carries all fields
- **WHEN** a frost alert is created with severity Yellow, confidence 0.75, expected low -2.1 and earliest frost at +4 h
- **THEN** the record exposes Type=Frost, Severity=Yellow, Confidence=0.75 and typed frost details with ExpectedLow=-2.1 and EarliestFrost=+4 h

#### Scenario: New alert type carries all fields
- **WHEN** an ice alert is created with severity Orange, confidence 0.9, expected low -1.0, 4 rain hours and frozen soil
- **THEN** the record exposes Type=Ice, Severity=Orange, Confidence=0.9 and typed ice details with those values

#### Scenario: Published attribute keys unchanged
- **WHEN** a frost alert with details is published via MQTT
- **THEN** its attributes JSON contains `expected_low`, `earliest_frost` and `models_agreeing` with the same values and formatting as before this change

## REMOVED Requirements

### Requirement: Alert record serialization
**Reason**: Alert records are in-process values only; no production code serializes or persists them, so the `[JsonProperty]` annotations and the string-keyed `Attributes` dictionary they pinned are removed.
**Migration**: The published wire format is owned by the MQTT and gRPC mappers and pinned by `wire-format-golden-masters`.
