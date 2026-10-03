## Context

The current `Njord.Tests` project contains 50 test files spanning unit tests, actor tests, container-integration tests, and an E2E test. All share a single csproj with dependencies on xUnit, Verify, Testcontainers, WireMock.Net.Testcontainers, MQTTnet, and Akka. The GaudiHTTP project demonstrates a clean split with a shared project for test utilities and separate projects per test type.

### Current file inventory (50 files)

**Unit tests** (no I/O, no containers): 37 files
- `Configuration/` (7): NjordOptionsSpec, NjordOptionsValidatorSpec, NjordServiceSetupSpec, NjordActorSystemSetupSpec, ParameterOptionsValidationSpec, PersistenceOptionsValidationSpec, RequestBudgetSpec
- `Domain/Analysis/` (14): AlertEvaluatorSpec, AlertResultSpec, ConsensusComputerSpec, ConsensusResultSpec, DerivedComputerSpec, DerivedResultSpec, EnergyForecasterSpec, EnergyResultSpec, HistoryAnalyzerSpec, HistoryResultSpec, IndexResultSpec, IndexScorerSpec, TrendAnalyzerSpec, TrendResultSpec
- `Domain/Weather/` (8): CycleIdSpec, ForecastDataHashSpec, ForecastSeriesSpec, ModelForecastSpec, ModelSnapshotSpec, ParameterRegistrySpec, TimeAnchorSpec, WeatherModelSpec
- `Egress/` (2 unit): HorizonProjectionSpec, TopicSlugSpec
- `Mqtt/` (3 unit): DiscoveryPayloadBuilderSpec, StatePayloadBuilderSpec, TopicSchemeSpec
- `Pipeline/` (3 unit): ModelPollStateSpec, PollPipelineSpec, WeightedTargetSpec

**Actor tests** (use ActorSystem, no containers): 8 files
- `Egress/` (2): EgressActorSpec, ModelStateActorSpec
- `Enrichment/` (2): EnrichmentActorSpec, ForecastHistoryActorSpec
- `Mqtt/` (2): DiscoveryActorSpec, MqttConnectionActorSpec
- `Pipeline/` (1): SchedulerActorSpec
- `Health/` (1): HealthEndpointSpec (uses WebApplicationFactory)

**Container-integration tests** (need Docker): 3 files
- `Ingest/OpenMeteoClientIntegrationSpec.cs` (WireMock)
- `Mqtt/MqttEgressIntegrationSpec.cs` (Mosquitto)
- `Ingest/OpenMeteoSmokeSpec.cs` (real API, gated — stays in Integration)

**E2E test** (WireMock + Mosquitto): 1 file
- `Integration/EndToEndPipelineSpec.cs`

**Infrastructure**: ModuleInitializer.cs (Verify settings)

## Goals / Non-Goals

**Goals:**
- Each test project can be run independently
- `Njord.Tests` has zero Docker/container dependencies
- Shared fixtures and test utilities live in one place
- Clear naming convention matches GaudiHTTP pattern

**Non-Goals:**
- CI pipeline changes (follow-up)
- New test creation
- Changing test logic

## Decisions

### 1. File assignment to projects

| Project | Files | Dependencies |
|---------|-------|-------------|
| `Njord.Tests.Shared` | JSON fixtures, `FakeOpenMeteoClient`, `MosquittoHelper`, `FixtureReader` | xUnit, Njord (project ref), MQTTnet |
| `Njord.Tests` | 37 unit + 8 actor + `ModuleInitializer.cs` + `OpenMeteoClientSpec.cs` | xUnit, Verify, TimeProvider.Testing, AspNetCore.Mvc.Testing, Shared (project ref) |
| `Njord.Tests.Integration` | `OpenMeteoClientIntegrationSpec`, `MqttEgressIntegrationSpec`, `OpenMeteoSmokeSpec` | xUnit, Testcontainers, WireMock.Net.Testcontainers, MQTTnet, Shared (project ref) |
| `Njord.Tests.Integration.E2E` | `EndToEndPipelineSpec` | xUnit, Testcontainers, WireMock.Net.Testcontainers, MQTTnet, Shared (project ref) |

**Rationale**: `OpenMeteoClientSpec` (the unit test with `RecordingHandler`) stays in `Njord.Tests` — it uses no containers. `OpenMeteoClientIntegrationSpec` (WireMock) moves to `Integration`. `OpenMeteoSmokeSpec` (real API, already gated) moves to `Integration` since it's an external I/O test.

### 2. Shared project is a class library, not a test project

**Choice**: `Njord.Tests.Shared` is a regular class library (`<OutputType>Library</OutputType>`, no `<IsTestProject>`). It contains no tests, just shared code and fixtures.

**Rationale**: It should never be "run" as a test project. JSON fixtures are embedded as Content items.

### 3. Extracting shared utilities

Three pieces of shared code need extraction:

- **`FakeOpenMeteoClient`**: Currently an inner class in `PollPipelineSpec.cs`. Extract to `Njord.Tests.Shared/FakeOpenMeteoClient.cs`.
- **`MosquittoHelper`**: The `CollectRetainedAsync` pattern is duplicated in `MqttEgressIntegrationSpec` and `EndToEndPipelineSpec`. Extract to `Njord.Tests.Shared/MosquittoHelper.cs`.
- **`FixtureReader`**: The `File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ...))` pattern appears in `OpenMeteoClientSpec`, `OpenMeteoClientIntegrationSpec`, and `EndToEndPipelineSpec`. Extract to `Njord.Tests.Shared/FixtureReader.cs` with fixtures as Content items.

### 4. Each test project gets its own `xunit.runner.json` and `ModuleInitializer.cs`

**Choice**: Copy `xunit.runner.json` to each test project. Only `Njord.Tests` needs `ModuleInitializer.cs` (Verify settings). Integration and E2E don't use Verify.

### 5. Solution file update

**Choice**: Add all 3 new projects to `Njord.slnx`. Remove nothing.

### 6. CLAUDE.md update

**Choice**: Update the "Build & test" section to show per-project commands:
```
dotnet run --project Njord.Tests/Njord.Tests.csproj                      # unit + actor
dotnet run --project Njord.Tests.Integration/Njord.Tests.Integration.csproj  # container
dotnet run --project Njord.Tests.Integration.E2E/Njord.Tests.Integration.E2E.csproj  # E2E
```

## Risks / Trade-offs

- **[More projects = more maintenance]** → Mitigated by central package management and `Directory.Build.props`. The 4-project structure is the sweet spot between monolith and over-splitting.
- **[Fixture duplication risk]** → Mitigated by the Shared project. Fixtures live in one place.
- **[Build time]** → Incremental builds may be slightly longer with more projects. Offset by faster targeted test runs.
