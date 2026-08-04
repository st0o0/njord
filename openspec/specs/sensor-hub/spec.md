# sensor-hub Specification

## Purpose

Domain model and actor for receiving, validating, aggregating, and expiring external sensor readings. Defines SensorKind metadata, plausibility ranges, aggregation strategies, staleness expiry, the SensorSnapshot query model, and SensorOptions configuration.

## Requirements

### Requirement: SensorKind domain enum
The system SHALL define a closed `SensorKind` enum representing physical quantities that enrichments can consume from external sensors. Each kind SHALL have associated metadata: unit of measurement, plausibility range (min/max), and aggregation strategy (Average, Sum, or Latest).

#### Scenario: Initial SensorKind values
- **WHEN** the system starts
- **THEN** the following SensorKind values are available: `IndoorTemperature` (°C, -10..60, Average), `IndoorHumidity` (%, 0..100, Average), `HeatPumpFlowTemp` (°C, 15..80, Latest), `SolarPanelPower` (W, 0..100000, Sum), `BatteryStateOfCharge` (%, 0..100, Average), `HeatPumpPower` (W, 0..50000, Latest)

#### Scenario: Unknown SensorKind rejected
- **WHEN** a reading with an unrecognized or unspecified SensorKind is received
- **THEN** the system SHALL reject it with an appropriate error

### Requirement: SensorHub actor stores latest readings
The SensorHub actor SHALL store the most recent `SensorReading` per unique key of (Location, SensorKind, Source). It SHALL accept `UpdateReading` messages to store new readings and `GetSnapshot` messages to return the current aggregated state for a location.

#### Scenario: Store and retrieve a single reading
- **WHEN** a reading `(Location="Luzern", Kind=IndoorTemperature, Source="wohnzimmer", Value=23.5)` is stored
- **AND** a `GetSnapshot("Luzern")` is requested
- **THEN** the snapshot SHALL contain `IndoorTemperature` with value `23.5` and source count `1`

#### Scenario: Multiple sources aggregated by Average
- **WHEN** readings `(Luzern, IndoorTemperature, "wohnzimmer", 23.5)` and `(Luzern, IndoorTemperature, "schlafzimmer", 21.0)` are stored
- **AND** a `GetSnapshot("Luzern")` is requested
- **THEN** the snapshot SHALL contain `IndoorTemperature` with value `22.25` and source count `2`

#### Scenario: Multiple sources aggregated by Sum
- **WHEN** readings `(Luzern, SolarPanelPower, "string1", 3000)` and `(Luzern, SolarPanelPower, "string2", 2500)` are stored
- **AND** a `GetSnapshot("Luzern")` is requested
- **THEN** the snapshot SHALL contain `SolarPanelPower` with value `5500` and source count `2`

#### Scenario: Latest aggregation uses most recent value
- **WHEN** readings `(Luzern, HeatPumpFlowTemp, "wp1", 32.0, T1)` and `(Luzern, HeatPumpFlowTemp, "wp2", 35.0, T2)` where T2 > T1 are stored
- **AND** a `GetSnapshot("Luzern")` is requested
- **THEN** the snapshot SHALL contain `HeatPumpFlowTemp` with value `35.0` and source count `2`

#### Scenario: GetSnapshot for location with no readings
- **WHEN** a `GetSnapshot("Unknown")` is requested
- **AND** no readings exist for that location
- **THEN** the response SHALL be a null or empty snapshot

### Requirement: Plausibility validation per SensorKind
The SensorHub SHALL reject readings whose value falls outside the plausibility range defined by the SensorKind metadata. The rejection SHALL include the reason and the valid range.

#### Scenario: Reading within plausible range accepted
- **WHEN** a reading `(IndoorTemperature, Value=23.5)` is received
- **THEN** the reading SHALL be accepted (23.5 is within -10..60)

#### Scenario: Reading outside plausible range rejected
- **WHEN** a reading `(IndoorTemperature, Value=85.0)` is received
- **THEN** the reading SHALL be rejected with reason indicating the valid range is -10..60

### Requirement: Staleness expiry
The SensorHub SHALL expire readings whose `MeasuredAt` timestamp is older than the configured staleness TTL. Expired readings SHALL be excluded from aggregation and snapshot responses. The SensorHub SHALL run a periodic cleanup timer.

#### Scenario: Fresh reading included in snapshot
- **WHEN** a reading was stored 30 seconds ago
- **AND** the staleness TTL is 7200 seconds
- **THEN** the reading SHALL be included in the snapshot

#### Scenario: Stale reading excluded from snapshot
- **WHEN** a reading was stored 8000 seconds ago
- **AND** the staleness TTL is 7200 seconds
- **THEN** the reading SHALL NOT be included in the snapshot

#### Scenario: Partial staleness with multiple sources
- **WHEN** source "wohnzimmer" has a fresh reading (23.5) and source "schlafzimmer" has an expired reading (21.0)
- **AND** the kind uses Average aggregation
- **THEN** the snapshot SHALL contain value `23.5` with source count `1`

### Requirement: SensorSnapshot domain record
The system SHALL define a `SensorSnapshot` record containing the aggregated sensor state for a single location. It SHALL provide a `Get(SensorKind)` method returning the aggregated value as `double?` (null if no readings exist for that kind).

#### Scenario: Get existing kind
- **WHEN** a snapshot contains `IndoorTemperature = 22.5`
- **AND** `Get(SensorKind.IndoorTemperature)` is called
- **THEN** the result SHALL be `22.5`

#### Scenario: Get missing kind
- **WHEN** a snapshot does not contain `HeatPumpFlowTemp`
- **AND** `Get(SensorKind.HeatPumpFlowTemp)` is called
- **THEN** the result SHALL be `null`

### Requirement: SensorOptions configuration
The system SHALL define a `SensorOptions` configuration class with `Enabled` (default: true) and `StalenessSeconds` (default: 7200). Validation SHALL ensure `StalenessSeconds` is positive.

#### Scenario: Default configuration
- **WHEN** no sensor configuration is provided
- **THEN** `Enabled` SHALL be `true` and `StalenessSeconds` SHALL be `7200`

#### Scenario: Invalid staleness rejected
- **WHEN** `StalenessSeconds` is set to `0` or negative
- **THEN** startup validation SHALL fail with an appropriate error message
