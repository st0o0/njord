Test conventions for all groups: `Spec` suffix, `sealed` class, BDD-style method names.
Pure synchronous specs use a plain `[Fact]`, and specs with async or DI-host setup use
`[Fact(Timeout = 5000)]`. After every group, the MQTT and gRPC golden masters from
`typed-analysis-models` must pass without re-approval.

## 1. Query vocabulary in Compute

- [ ] 1.1 Add `src/Njord.Compute/Querying/ConsensusQueries.cs` (`Find`, `Within`, `Extreme`, `AgreementAbove`, `AverageAgreement`, `MaxModelCount`, `ModelsAt`) and `HorizonWindow`/`HorizonHit`, with `src/Njord.Compute.Tests/Querying/ConsensusQueriesSpec.cs` (port the `HorizonExtremeScannerSpec` cases)
- [ ] 1.2 Rename `ConsensusSnapshotFactory` → `ConsensusBuilder` and `ConsensusComputer` → `ConsensusStatistics`, move them to `src/Njord.Compute/Consensus/`; rename their specs; update `src/Njord.Pipeline/ForecastPipeline.cs` and DI registration
- [ ] 1.3 Move result and settings records to `src/Njord.Compute/Contracts/` (namespaces updated across Mqtt, Grpc, Core, Pipeline)

## 2. Alerts: one rule per alert type

- [ ] 2.1 Add `IAlertRule` and the completeness check plus ordered output in `src/Njord.Alerts/AlertAnalyzer.cs`; spec `src/Njord.Alerts.Tests/AlertAnalyzerSpec.cs` (missing rule fails fast; reverse registration keeps order)
- [ ] 2.2 Extract `FrostRule`, `HeatRule`, `StormRule`, `HeavyRainRule`, `UvRule` into `src/Njord.Alerts/Rules/` using the query vocabulary; port their cases from `AlertEvaluatorSpec` into `src/Njord.Alerts.Tests/Rules/<Type>RuleSpec.cs`
- [ ] 2.3 Extract `FogRule`, `SnowRule`, `PressureDropRule`, `ThunderstormRule`, `IceRule` the same way, with specs
- [ ] 2.4 Extract `WindChillRule`, `VisibilityRule`, `TropicalNightRule`, `HumidityRule` the same way, with specs
- [ ] 2.5 Delete `AlertEvaluator.cs`, `AlertEvaluatorSpec.cs`, `HorizonExtremeScanner`, `ThresholdClassifier` (if fully replaced by `SeverityThresholds`) and the `InternalsVisibleTo("Njord.Alerts")` in `src/Njord.Compute/Njord.Compute.csproj`; register all rules in `src/Njord.Alerts/ServiceCollectionExtensions.cs`

## 3. Indices: one score per index kind

- [ ] 3.1 Move `TimeSliceAggregator`/`DaySlice` to `src/Njord.Indices/DaySlicer.cs` (+ `DaySlice`), and the frost-protection and VPD computations out of `IndexComputer` into `FrostProtection.cs`/`VaporPressureDeficit.cs`; move their specs to `src/Njord.Indices.Tests/`
- [ ] 3.2 Add `IIndexScore` and extract the 8 scores from `IndexScorer` into `src/Njord.Indices/Scores/<Kind>Score.cs`, including the confidence-envelope logic; port `IndexScorerSpec` cases into `src/Njord.Indices.Tests/Scores/<Kind>ScoreSpec.cs`
- [ ] 3.3 Rebuild `src/Njord.Indices/IndexAnalyzer.cs` as the orchestrator (slices → scores ordered by `IndexKind` → `IndexResult`); delete `IndexComputer`/`IndexScorer`; port `IndexResultSpec` to `src/Njord.Indices.Tests/IndexAnalyzerSpec.cs`

## 4. Derived and Trends

- [ ] 4.1 Move `DerivedComputer` → `src/Njord.Derived/DerivedFormulas.cs` and the assembling part of `DerivedResultComputer` into `DerivedAnalyzer`; move `DerivedComputerSpec`/`DerivedResultSpec` to `src/Njord.Derived.Tests/`
- [ ] 4.2 Move `TrendCalculator` → `src/Njord.Trends/TrendFormulas.cs` and `TrendComputer` into `TrendAnalyzer`; move `TrendCalculatorSpec`/`TrendResultSpec` to `src/Njord.Trends.Tests/`
- [ ] 4.3 Replace every remaining private `FindParam`/`Get(means, param)` copy with the query vocabulary

## 5. Enforce the structure

- [ ] 5.1 Add to `src/Njord.Architecture.Tests/ConventionArchitectureSpec.cs`: no type name in Compute or feature libraries contains Enrichment/Computer/Calculator/Scorer/Evaluator/Factory; no `IAlertRule`/`IIndexScore` implementation in Compute
- [ ] 5.2 Add a check that `Njord.Compute.csproj` has no `InternalsVisibleTo` except `Njord.Compute.Tests`
- [ ] 5.3 Update AGENTS.md "Solution structure" (Compute contents, feature-library contents) and the `njord-enrichment-feature` skill in `.claude/` to describe rules/scores instead of presenters

## 6. Validation

- [ ] 6.1 `cd src && dotnet build Njord.slnx` with no new warnings
- [ ] 6.2 `dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj --no-build`
- [ ] 6.3 `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj --no-build`, `dotnet run --project Njord.Indices.Tests/Njord.Indices.Tests.csproj --no-build`, `dotnet run --project Njord.Derived.Tests/Njord.Derived.Tests.csproj --no-build`, `dotnet run --project Njord.Trends.Tests/Njord.Trends.Tests.csproj --no-build`
- [ ] 6.4 `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj --no-build` and `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj --no-build` (golden masters unchanged)
- [ ] 6.5 `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj --no-build` and `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj --no-build`
- [ ] 6.6 Full loop over all `Njord.*Tests` projects (AGENTS.md "Build & test")
- [ ] 6.7 Check that no file in `src/Njord.Alerts/Rules/` or `src/Njord.Indices/Scores/` exceeds ~120 lines; split or simplify any that do
- [ ] 6.8 From the repo root: `openspec validate restructure-compute --strict`
