Test conventions for all groups: `Spec` suffix, `sealed` class, BDD-style method names.
Specs that await or touch Akka carry `[Fact(Timeout = 5000)]`; pure synchronous specs
use a plain `[Fact]` (AGENTS.md: a timeout cannot be enforced there).

## 1. Wire-format golden masters (before any model change)

- [x] 1.1 Add a deterministic analysis fixture to `src/Njord.Tests.Shared/` (fixed `FakeTimeProvider`, 2 locations, 3 models, hourly + daily horizons) that triggers every alert type, every index kind, every trend/derived field and at least one null value
- [x] 1.2 Add `src/Njord.Mqtt.Tests/StatePayloadGoldenMasterSpec.cs`: one Verify snapshot per `StatePayloadBuilder.From*` (consensus, alerts, derived, trends, indices), recording topic, payload and retain flag in order; approve the snapshots against the current code
- [x] 1.3 Add `src/Njord.Grpc.Tests/ProtoMappingGoldenMasterSpec.cs`: Verify snapshots of `EnrichmentProtoMapper` output for every analysis kind and of the forecast-update mapping; approve against the current code
- [x] 1.4 Delete the orphaned `*.verified.txt` files in `src/Njord.Mqtt.Tests/` that no spec produces (the discovery and old consensus/state snapshots)
- [x] 1.5 Check fixture coverage: assert in the specs that all 14 alert types and all 8 index kinds appear in the produced messages

## 2. Typed horizon

- [x] 2.1 Add `Horizon` (`readonly record struct`, `Hours(int)`, `Days(int)`, `ToWireKey()`, `TryParseWireKey`) in `src/Njord.Weather/Horizon.cs` with `src/Njord.Weather.Tests/HorizonSpec.cs` (wire keys, equality, hourly vs. daily distinct, parse round-trip)
- [x] 2.2 Switch `src/Njord.Weather/HorizonProjection.cs` output keys to `Horizon`; update `src/Njord.Weather.Tests`
- [x] 2.3 Switch `ParameterConsensus.ByHorizon` and `ConsensusSnapshotFactory` (`src/Njord.Compute/Analysis/`) to `Horizon` keys; update `src/Njord.Compute.Tests/Analysis/ConsensusSnapshotSpec.cs`
- [x] 2.4 Switch `HorizonExtremeScanner`, `TimeSliceAggregator`, `DerivedResultComputer`, `IndexComputer`, `TrendComputer` and `HorizonDerived` keys to `Horizon`; update their specs in `src/Njord.Compute.Tests/Analysis/`
- [x] 2.5 Switch `src/Njord.Alerts/AlertEvaluator.cs` horizon lookups (`CountModelsAtPeak`, `CountModelsAtHorizon`) to `Horizon`; update `src/Njord.Alerts.Tests/AlertEvaluatorSpec.cs`
- [x] 2.6 Use `Horizon.ToWireKey()` in `src/Njord.Mqtt/StatePayloadBuilder.cs` and `src/Njord.Grpc/EnrichmentProtoMapper.cs`; golden masters stay green without re-approval

## 3. Typed categorical values

- [x] 3.1 Add `TrendDirection`, `Stability`, `WeatherCategory` enums with `ToWireName()` in `src/Njord.Compute/Analysis/`; switch `TrendCalculator`, `TrendComputer`, `ParameterTrend`, `StabilityInfo`, `WeatherChangeResult`; update `TrendCalculatorSpec`/`TrendResultSpec`
- [x] 3.2 Add `DewPointComfort` and `WeatherCondition` (with `ToWireName()`/`ToDescription()`, WMO table moved from `DerivedComputer`); switch `DerivedComputer`, `HorizonDerived`, `ScalarDerived`; update `DerivedComputerSpec`/`DerivedResultSpec`
- [x] 3.3 Format all of these at the edge in `StatePayloadBuilder` and `EnrichmentProtoMapper`; golden masters stay green

## 4. Typed index kinds and locations

- [x] 4.1 Add `IndexKind` enum with `ToWireName()` and case-insensitive `TryParse` from config keys, plus `LocationId` (case-insensitive equality) in `src/Njord.Weather/LocationId.cs`, each with a spec
- [x] 4.2 Switch `PreferenceResolver`/`ResolvedPreferences` key to `(LocationId, IndexKind)` and remove `PrefsFor("…")` literals from `IndexComputer`; update `PreferenceResolverSpec`/`IndexResultSpec`
- [x] 4.3 Reject unknown `ScoreOverrides` keys in `src/Njord.Core/Configuration/IndexOptionsValidator.cs` (global and per-location) with a spec in `src/Njord.Core.Tests/Configuration/`
- [x] 4.4 Use `LocationId` in the analysis result records and `ConsensusSnapshot`; convert to text in the mappers; update `src/Njord.Indices`, `src/Njord.Pipeline/ForecastPipeline.cs` and the affected specs

## 5. Typed alert details and thresholds

- [ ] 5.1 Add `AlertDetails` (abstract) with one sealed record per alert type plus `NoDetails`, and replace `Alert.Attributes`; spec `src/Njord.Compute.Tests/Analysis/AlertDetailsSpec.cs`
- [ ] 5.2 Add `SeverityThresholds` (advisory/warning/severe + `ScanDirection`, `Classify(value)`) replacing `ThresholdClassifier`'s array API; update `ThresholdClassifierSpec`
- [ ] 5.3 Make every `Evaluate*` in `src/Njord.Alerts/AlertEvaluator.cs` produce typed details and take `SeverityThresholds` (built from the option arrays in `EvaluateAll`); update `src/Njord.Alerts.Tests/AlertEvaluatorSpec.cs`
- [ ] 5.4 Map details to the existing attribute keys in `StatePayloadBuilder.FromAlerts` and in `EnrichmentProtoMapper`; golden masters stay green

## 6. Remove dead serialization annotations

- [ ] 6.1 Remove `[JsonProperty]` and `using Newtonsoft.Json` from all `src/Njord.Compute/Analysis/*` records and from `src/Njord.Weather/ParameterDef.cs` / `WeatherModel.cs`
- [ ] 6.2 Remove the Newtonsoft package reference from `Njord.Weather.csproj` if nothing else in the project uses it; keep it otherwise and note why

## 7. Validation

- [ ] 7.1 `cd src && dotnet build Njord.slnx` with no new warnings
- [ ] 7.2 `dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj --no-build`
- [ ] 7.3 `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build`
- [ ] 7.4 `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj --no-build`, plus `Njord.Derived.Tests`, `Njord.Trends.Tests`, `Njord.Indices.Tests`
- [ ] 7.5 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build` and `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build` (golden masters unchanged since 1.2/1.3)
- [ ] 7.6 Full loop over all `Njord.*Tests` projects (AGENTS.md "Build & test")
- [ ] 7.7 `grep -rnE '\$"[hd]\{' src --include=*.cs` finds nothing outside `Horizon.cs`
- [ ] 7.8 From the repo root: `openspec validate typed-analysis-models --strict`
