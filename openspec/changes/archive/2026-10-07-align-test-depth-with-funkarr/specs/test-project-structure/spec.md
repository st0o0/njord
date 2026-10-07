## MODIFIED Requirements

### Requirement: All test projects in the solution
The `Njord.slnx` solution file SHALL include all test projects (`Njord.Architecture.Tests`, `Njord.Compute.Tests`, `Njord.Core.Tests`, `Njord.Domain.Tests`, `Njord.Egress.Tests`, `Njord.Enrichment.Tests`, `Njord.Grpc.Tests`, `Njord.Ingest.Tests`, `Njord.Mqtt.Tests`, `Njord.Persistence.Tests`, `Njord.Pipeline.Tests`, `Njord.Sensors.Tests`) and `Njord.Tests.Shared`, so `dotnet build Njord.slnx` compiles everything. There is no `Njord.IntegrationTests` or `Njord.E2E.Tests` project — see the requirements below for where that coverage lives instead.

#### Scenario: Solution builds all test projects
- **WHEN** `dotnet build Njord.slnx` is executed
- **THEN** every `Njord.*Tests` project and `Njord.Tests.Shared` SHALL compile successfully

### Requirement: Persistence roundtrip tests live alongside their Verify shape tests
`Njord.Persistence.Tests` SHALL contain `PersistenceRoundtripHelper.cs` (a static `AssertRoundtrip<T>(T dto)` helper) and a `*DtoRoundtripSpec.cs` file per persistence DTO, alongside the existing `*DtoSerializationSpec.cs` (Verify shape) files. The helper is NOT in `Njord.Tests.Shared`, because it is used only by `Njord.Persistence.Tests` and the shared-infrastructure rule below requires at least two consumers.

#### Scenario: Roundtrip spec sits next to its shape spec
- **WHEN** `Njord.Persistence.Tests` is inspected
- **THEN** every persistence DTO has both a `*DtoSerializationSpec.cs` (Verify shape) and a `*DtoRoundtripSpec.cs` (field-level roundtrip) file

### Requirement: End-to-end stack verification is a skill, not a dotnet test project
Full-stack, cross-repo (njord + ha-njord + Home Assistant) end-to-end verification SHALL be the `e2e-test` Claude Code skill (`.claude/skills/e2e-test/SKILL.md` + `e2e/E2E-TEST-PLAN.md`), driving a real Docker Compose stack and browser automation — not a `Njord.E2E.Tests` dotnet test project. The solution SHALL NOT contain a `Njord.E2E.Tests` project.

#### Scenario: No E2E dotnet test project exists
- **WHEN** `Njord.slnx` is inspected
- **THEN** it SHALL NOT list a `Njord.E2E.Tests` project

#### Scenario: E2E verification is invoked as a skill
- **WHEN** a contributor wants to verify the full stack end-to-end
- **THEN** they invoke the `e2e-test` skill, which requires Docker and a Chrome browser, not `dotnet run`
