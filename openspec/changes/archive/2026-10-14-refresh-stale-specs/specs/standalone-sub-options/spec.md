## ADDED Requirements

### Requirement: Options types are not nested inside NjordOptions
No options type SHALL be a property of another options type. `NjordOptions` SHALL contain only the forecast scope: locations, default models, horizons, forecast days and the requested parameter set. `SensorOptions` SHALL be registered and validated by `Njord.SensorHub`'s setup container, not the host. There is no `GrpcOptions` type.

#### Scenario: NjordOptions has no nested sections
- **WHEN** `NjordOptions` is resolved from DI
- **THEN** it SHALL NOT contain properties for `Grpc`, `Sensors`, `Mqtt`, `Enrichment`, `Persistence`, `PollInterval` or `BudgetOverride`

#### Scenario: SensorHub uses IOptions of SensorOptions
- **WHEN** the `SensorHub` actor is constructed
- **THEN** it receives `IOptions<SensorOptions>` for its staleness configuration

## REMOVED Requirements

### Requirement: GrpcOptions and SensorOptions are not nested inside NjordOptions
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Options types are not nested inside NjordOptions" in this capability.
