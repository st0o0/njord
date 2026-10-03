## MODIFIED Requirements

### Requirement: Sensor reading timestamp fallback
`SensorGrpcService` SHALL use the injected `TimeProvider` to generate fallback timestamps when a gRPC request does not include a `MeasuredAt` value. It SHALL NOT use `DateTimeOffset.UtcNow` directly.

#### Scenario: Missing MeasuredAt uses TimeProvider
- **WHEN** a `ReportSensorReading` gRPC request arrives without a `MeasuredAt` field
- **THEN** the service SHALL use `TimeProvider.GetUtcNow()` as the fallback timestamp

#### Scenario: Provided MeasuredAt is preserved
- **WHEN** a `ReportSensorReading` gRPC request includes a `MeasuredAt` field
- **THEN** the service SHALL use the provided timestamp, ignoring `TimeProvider`

### Requirement: HistoryAnalyzer TimeProvider is non-optional
`HistoryAnalyzer.ModelAccuracy` SHALL require a non-null `TimeProvider` parameter. It SHALL NOT fall back to `TimeProvider.System` when the parameter is null.

#### Scenario: Null TimeProvider is a compile error
- **WHEN** a caller invokes `HistoryAnalyzer.ModelAccuracy` without a `TimeProvider`
- **THEN** the code SHALL fail to compile (non-nullable parameter)
