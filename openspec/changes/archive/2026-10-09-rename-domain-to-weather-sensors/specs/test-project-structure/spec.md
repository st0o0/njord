## MODIFIED Requirements

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Compute.Tests`, `Njord.Core.Tests`, `Njord.Weather.Tests`, `Njord.Sensors.Tests`, `Njord.Egress.Tests`, `Njord.Enrichment.Tests`, `Njord.Grpc.Tests`, `Njord.Ingest.Tests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.SensorHub.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything. There is no `Njord.Domain.Tests`, `Njord.IntegrationTests` or `Njord.E2E.Tests` project — `Njord.Domain.Tests` is split into `Njord.Weather.Tests` and `Njord.Sensors.Tests`; see `persistence-roundtrip-tests` and `e2e-stack-verification` for where integration/E2E coverage lives instead.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully
