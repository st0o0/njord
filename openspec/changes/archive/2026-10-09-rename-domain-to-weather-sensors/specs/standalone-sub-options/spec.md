## MODIFIED Requirements

### Requirement: GrpcOptions and SensorOptions are not nested inside NjordOptions
`GrpcOptions` and `SensorOptions` SHALL NOT be properties of `NjordOptions`. `SensorOptions` SHALL be registered and validated by `Njord.SensorHub`'s setup container, not the host.

#### Scenario: NjordOptions has no Grpc or Sensors property
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for `Grpc` or `Sensors`

#### Scenario: SensorHubActor uses IOptions of SensorOptions
- **WHEN** `SensorHubActor` is constructed
- **THEN** it SHALL receive `IOptions<SensorOptions>` for sensor fallback configuration
