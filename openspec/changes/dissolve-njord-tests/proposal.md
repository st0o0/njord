## Why

FunkArr has no generic `FunkArr.Tests` project — every test lives in its domain's `*.Tests` project or in `FunkArr.IntegrationTests`. njord's `Njord.Tests` is a catch-all with 12 specs spanning 5 concerns (configuration, gRPC integration, health, persistence, pipeline). This makes it unclear where new tests belong and creates file contention between unrelated test classes sharing the same project and `data/` directory.

## What Changes

- **Dissolve `Njord.Tests`** by moving each spec to its natural home:
  - `PollPipelineSpec` → `Njord.Pipeline.Tests/`
  - `ForecastHistoryDtoSerializationSpec` → `Njord.Persistence.Tests/`
  - `MqttConnectionHealthCheckSpec`, `PipelineHealthCheckSpec` → `Njord.Core.Tests/`
  - `NjordActorSystemSetupSpec`, `PersistenceBeforeActorsSpec` → `Njord.Core.Tests/` (they test host setup logic that lives in Core)
  - `NjordServiceSetupSpec`, `ActorKeyRegistrationSpec`, `StreamShutdownTaskSpec` → `Njord.IntegrationTests/` (boot a real host or actor system)
  - `HealthEndpointSpec` + gRPC integration specs + `NjordFixture` + collections → `Njord.IntegrationTests/`
- **Rename `Njord.Integration.Tests` to `Njord.IntegrationTests`** to match FunkArr's naming convention
- **Delete `Njord.Tests` project** after all specs are moved
- **Move the Testcontainers-based specs** (currently skipped) remain in `Njord.IntegrationTests`

## Non-goals

- Changing any test logic — this is a pure move/rename
- Adding new tests
- Changing polling behavior (no API-budget impact)

## Capabilities

### New Capabilities
- `dissolve-host-tests`: Migration plan for moving specs out of the catch-all host test project

### Modified Capabilities
- `test-project-structure`: Updated to reflect the dissolved Njord.Tests and renamed IntegrationTests project

## Impact

- **Deleted project**: `src/Njord.Tests/`
- **Renamed project**: `src/Njord.Integration.Tests/` → `src/Njord.IntegrationTests/`
- **Modified projects**: `Njord.Pipeline.Tests/`, `Njord.Persistence.Tests/`, `Njord.Core.Tests/` gain specs from Njord.Tests
- **Modified**: `Njord.slnx` — removed Njord.Tests, renamed Integration project
- **Modified**: `AGENTS.md` — updated solution structure and test counts
- **Modified**: `Njord.Architecture.Tests/NjordArchitecture.cs` — TestAssemblies no longer includes Njord.Tests
