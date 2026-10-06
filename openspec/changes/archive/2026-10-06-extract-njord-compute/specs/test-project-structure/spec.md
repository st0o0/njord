## MODIFIED Requirements

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Compute.Tests`, `Njord.Core.Tests`, `Njord.Domain.Tests`, `Njord.Egress.Tests`, `Njord.Enrichment.Tests`, `Njord.Grpc.Tests`, `Njord.Ingest.Tests`, `Njord.IntegrationTests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.Sensors.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully
