### Requirement: Parse alert trigger values from protobuf

The gRPC client SHALL parse `trigger_value`, `threshold`, `peak_value`, `hours_until`, and `duration_hours` from the `Alert` protobuf message into `AlertData`.

- `trigger_value` and `threshold` SHALL always be populated (double, defaults to 0.0)
- `peak_value`, `hours_until`, `duration_hours` SHALL be `None` when not set in the proto

#### Scenario: Alert with all fields set

- **WHEN** njord returns an Alert with `trigger_value=8.5, threshold=6.0, peak_value=9.2, hours_until=2, duration_hours=4`
- **THEN** `AlertData` SHALL have `trigger_value=8.5, threshold=6.0, peak_value=9.2, hours_until=2, duration_hours=4`

#### Scenario: Alert with only required fields

- **WHEN** njord returns an Alert with `trigger_value=38.2, threshold=35.0` and no optional fields set
- **THEN** `AlertData` SHALL have `trigger_value=38.2, threshold=35.0, peak_value=None, hours_until=None, duration_hours=None`

#### Scenario: Inactive alert (severity=none)

- **WHEN** njord returns an Alert with severity NONE and default trigger_value/threshold (0.0)
- **THEN** `AlertData` SHALL have `trigger_value=0.0, threshold=0.0`

### Requirement: Expose trigger values as binary sensor attributes

The `NjordAlertEntity.extra_state_attributes` SHALL include `trigger_value` and `threshold` always, and `peak_value`, `hours_until`, `duration_hours` only when they have a value (not None).

#### Scenario: Active alert with all values

- **WHEN** the alert has `severity="orange", trigger_value=8.5, threshold=6.0, peak_value=9.2, hours_until=2, duration_hours=4`
- **THEN** `extra_state_attributes` SHALL be `{"severity": "orange", "confidence": ..., "trigger_value": 8.5, "threshold": 6.0, "peak_value": 9.2, "hours_until": 2, "duration_hours": 4}`

#### Scenario: Active alert without optional values

- **WHEN** the alert has `severity="yellow", trigger_value=38.2, threshold=35.0, peak_value=None, hours_until=None`
- **THEN** `extra_state_attributes` SHALL be `{"severity": "yellow", "confidence": ..., "trigger_value": 38.2, "threshold": 35.0}` (no peak_value or hours_until keys)

#### Scenario: No alert data

- **WHEN** no alert exists for this type
- **THEN** `extra_state_attributes` SHALL return `None`

### Requirement: Alert data model

The `AlertData` dataclass SHALL include the following fields:
- `type: str`
- `severity: str` (default "none")
- `confidence: float` (default 0.0)
- `trigger_value: float` (default 0.0)
- `threshold: float` (default 0.0)
- `peak_value: float | None` (default None)
- `hours_until: int | None` (default None)
- `duration_hours: int | None` (default None)

#### Scenario: AlertData construction with defaults

- **WHEN** `AlertData(type="frost")` is created
- **THEN** it SHALL have `severity="none", confidence=0.0, trigger_value=0.0, threshold=0.0, peak_value=None, hours_until=None, duration_hours=None`
