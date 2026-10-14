## 1. Failing specs

- [x] 1.1 Add `src/Njord.Pipeline.Tests/ForecastPipelineConsensusToggleSpec.cs` (sealed, `Spec` suffix, `[Fact(Timeout = TestTimeouts.Hosted)]`, BDD names): with consensus disabled and all other analyzers enabled, no `ConsensusComputed` is emitted while alert, derived and index events arrive for every forecast and a trend event for every forecast after the first
- [x] 1.2 Same spec: with the default options a `ConsensusComputed` is emitted for every forecast
- [x] 1.3 Same spec: an `AnalysisSettingsChanged` at runtime does not bring consensus output back while it is disabled at startup
- [x] 1.4 Add a `src/Njord.Tests/Configuration/ConfigManagerSpec.cs` case: toggling consensus is applied and reported as `Enrichment.Consensus.Enabled` restart-required; extend `src/Njord.Core.Tests/Configuration/ConfigReloadPolicySpec.cs` `Feature_toggles_require_a_restart` to all five toggles
- [x] 1.5 Run the new specs and record the failures

## 2. Implementation

- [x] 2.1 `src/Njord.Pipeline/ForecastPipeline.cs`: take `IOptions<ConsensusOptions>`, capture `Enabled`, emit `ConsensusComputed` only when enabled; keep building the snapshot and updating `previous`
- [x] 2.2 Update the direct constructions in `src/Njord.Pipeline.Tests/ForecastPipelineSettingsSpec.cs` and `ForecastPipelineSensorSpec.cs`

## 3. Validation

- [x] 3.1 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj` 3× green
- [x] 3.2 `dotnet run --project Njord.Tests/Njord.Tests.csproj` and `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` green
- [x] 3.3 `dotnet build Njord.slnx` with 0 warnings, full test loop from `AGENTS.md` green, no `*.verified.*` change
- [x] 3.4 Update the test total in `AGENTS.md`
