## REMOVED Requirements

### Requirement: No catch-all host test project
**Reason**: The capability contradicts the current layout: `src/Njord.Tests` is a deliberate host test project (ConfigManager and options-binding specs), and there is no `Njord.IntegrationTests` project, no `NjordFixture` and no `NjordServiceSetup`. Specs such as `PollPipelineSpec` and `ForecastHistoryDtoSerializationSpec` no longer exist.
**Migration**: The test layout is defined by `test-project-structure` (per-library test projects plus the host project `Njord.Tests`).

### Requirement: Integration tests use the IntegrationTests project
**Reason**: The capability contradicts the current layout: `src/Njord.Tests` is a deliberate host test project (ConfigManager and options-binding specs), and there is no `Njord.IntegrationTests` project, no `NjordFixture` and no `NjordServiceSetup`. Specs such as `PollPipelineSpec` and `ForecastHistoryDtoSerializationSpec` no longer exist.
**Migration**: The test layout is defined by `test-project-structure` (per-library test projects plus the host project `Njord.Tests`).

### Requirement: IntegrationTests project naming matches FunkArr
**Reason**: The capability contradicts the current layout: `src/Njord.Tests` is a deliberate host test project (ConfigManager and options-binding specs), and there is no `Njord.IntegrationTests` project, no `NjordFixture` and no `NjordServiceSetup`. Specs such as `PollPipelineSpec` and `ForecastHistoryDtoSerializationSpec` no longer exist.
**Migration**: The test layout is defined by `test-project-structure` (per-library test projects plus the host project `Njord.Tests`).
