## MODIFIED Requirements

### Requirement: ConfigService is a separate gRPC service
`ConfigService` SHALL be defined in `protos/njord/v1/config_service.proto`. It SHALL include read RPCs (`GetConfig`, `StreamConfig`, `GetStatus`, `GetTriggerTargets`) and mutation RPCs (`AddLocation`, `RemoveLocation`, `UpdateLocation`, `UpdateForecastSettings`, `UpdateEnrichmentConfig`, `UpdateBudget`) and operations RPCs (`TriggerPoll`).

#### Scenario: Proto compiles independently
- **WHEN** `config_service.proto` is compiled
- **THEN** it SHALL not depend on `forecast_service.proto`

#### Scenario: Proto compiles with all RPCs
- **WHEN** `dotnet build` runs
- **THEN** gRPC stubs SHALL be generated for all 10 ConfigService RPCs without errors
