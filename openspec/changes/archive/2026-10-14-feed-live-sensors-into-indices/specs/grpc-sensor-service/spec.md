## MODIFIED Requirements

### Requirement: Push RPC accepts single reading
The `Push` RPC SHALL validate the reading (known SensorKind, known location, plausible value) and forward it to the SensorHub actor. The location SHALL be matched against the configured locations case-insensitively, and the forwarded reading SHALL carry the configured spelling of the location name, so readings and forecasts of one location always share the same name. It SHALL return a `PushResponse` with `accepted = true` on success or `accepted = false` with `rejection_reason` on failure.

#### Scenario: Valid reading accepted
- **WHEN** a `Push` request with `kind=INDOOR_TEMPERATURE, location="Luzern", source="wohnzimmer", value=23.5` is received
- **AND** "Luzern" is a configured location
- **THEN** the response SHALL have `accepted = true`

#### Scenario: Unknown SensorKind rejected
- **WHEN** a `Push` request with `kind=SENSOR_KIND_UNSPECIFIED` is received
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate unknown sensor kind

#### Scenario: Unknown location rejected
- **WHEN** a `Push` request with `location="Atlantis"` is received
- **AND** "Atlantis" is not a configured location
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate unknown location

#### Scenario: Value outside plausibility range rejected
- **WHEN** a `Push` request with `kind=INDOOR_TEMPERATURE, value=85.0` is received
- **THEN** the response SHALL have `accepted = false` and `rejection_reason` SHALL indicate the valid range

#### Scenario: Location stored under its configured spelling
- **WHEN** a `Push` request with `location="luzern"` is received
- **AND** "Luzern" is a configured location
- **THEN** the response SHALL have `accepted = true` and the reading forwarded to the SensorHub SHALL carry the location "Luzern"
