## 1. File-per-type: Domain/Analysis splits

- [x] 1.1 `AlertEvaluator.cs` → extract `AlertType.cs`, `AlertSeverity.cs`, `Alert.cs`, `AlertTypeExtensions.cs` in `src/Njord/Domain/Analysis/`
- [x] 1.2 `TrendResult.cs` → extract `ParameterTrend.cs`, `PrecipTimingInfo.cs`, `ExtremaTimingInfo.cs`, `StabilityInfo.cs`, `DecayInfo.cs`
- [x] 1.3 `ConsensusResult.cs` → extract `OutlierInfo.cs`, `ConfidenceIntervalInfo.cs`, `HorizonConsensus.cs`, `ParameterConsensus.cs`
- [x] 1.4 `ConsensusSnapshot.cs` → extract `HourlyConsensus.cs`, `DailyConsensus.cs`
- [x] 1.5 `DerivedResult.cs` → extract `HorizonDerived.cs`, `ScalarDerived.cs`
- [x] 1.6 `TrendAnalyzer.cs` → extract `WeatherChangeResult.cs`
- [x] 1.7 `ForecastHistory.cs` → extract `ForecastRecord.cs`
- [x] 1.8 `IndexResult.cs` (contains ScoreEnvelope, FrostProtectionInfo, VpdInfo) → extract `ScoreEnvelope.cs`, `FrostProtectionInfo.cs`, `VpdInfo.cs`, then delete `IndexResult.cs`
- [x] 1.9 `DaySliceIndexResult.cs` (contains DayScoreSet + IndexResult) → extract `DayScoreSet.cs`, rename `DaySliceIndexResult.cs` → `IndexResult.cs`

## 2. File-per-type: Mqtt splits

- [x] 2.1 `MqttMessages.cs` → extract `MqttEgressTuning.cs`, `RequestMqttSink.cs`, `MqttSinkResponse.cs` in `src/Njord/Mqtt/`, then delete `MqttMessages.cs`

## 3. Build check after file splits

- [x] 3.1 `dotnet build Njord.slnx` from `src/` — verify zero errors after all splits

## 4. Extract Compute → Computer services

- [x] 4.1 Create `IndexComputer.cs` in `src/Njord/Domain/Analysis/` — move `IndexResult.Compute` and all its private helpers out of `IndexResult` record. Constructor takes `ResolvedParameterSet`, `TimeProvider`. Public method: `IndexResult Compute(ConsensusSnapshot, IReadOnlyDictionary<(string,string),ResolvedPreferences>)`
- [x] 4.2 Create `ConsensusSnapshotFactory.cs` in `src/Njord/Domain/Analysis/` — move `ConsensusSnapshot.Compute` and private helpers. Constructor takes `ResolvedParameterSet`, `TimeProvider`. Public method: `ConsensusSnapshot Create(ModelSnapshot, string, double trimPercent = 0.1, double agreementTolerance = 2.0)`
- [x] 4.3 Create `TrendComputer.cs` in `src/Njord/Domain/Analysis/` — move `TrendResult.Compute` and private helpers. Stateless (no constructor dependencies). Public method: `TrendResult Compute(ConsensusSnapshot, ConsensusSnapshot?)`
- [x] 4.4 Create `DerivedResultComputer.cs` in `src/Njord/Domain/Analysis/` — move `DerivedResult.Compute` and private helpers. Constructor takes `ResolvedParameterSet`. Public method: `DerivedResult Compute(ConsensusSnapshot, IReadOnlyList<int> horizons)`
- [x] 4.5 Create `HistoryComputer.cs` in `src/Njord/Domain/Analysis/` — move `HistoryResult.Compute` and private helpers. Stateless. Public method: `HistoryResult Compute(ForecastHistory, ModelSnapshot, string location, ResolvedParameterSet, TimeProvider, HistoryOptions)`

## 5. Update enrichment features to use Computer services

- [x] 5.1 Update `IndexEnrichment` in `src/Njord/Enrichment/Features/IndexEnrichment.cs` — inject `IndexComputer`, call `_indexComputer.Compute(...)` instead of `IndexResult.Compute(...)`
- [x] 5.2 Update `DerivedEnrichment` in `src/Njord/Enrichment/Features/DerivedEnrichment.cs` — inject `DerivedResultComputer`, call `_computer.Compute(...)` instead of `DerivedResult.Compute(...)`
- [x] 5.3 Update `TrendEnrichment` in `src/Njord/Enrichment/Features/TrendEnrichment.cs` — inject `TrendComputer`, call `_computer.Compute(...)` instead of `TrendResult.Compute(...)`
- [x] 5.4 Update `HistoryEnrichment` in `src/Njord/Enrichment/Features/HistoryEnrichment.cs` — inject `HistoryComputer`, call `_computer.Compute(...)` instead of `HistoryResult.Compute(...)`
- [x] 5.5 Update `EnrichmentActor` in `src/Njord/Enrichment/EnrichmentActor.cs` — inject `ConsensusSnapshotFactory`, call `_factory.Create(...)` instead of `ConsensusSnapshot.Compute(...)`

## 6. DI registration

- [x] 6.1 Register all 5 computer services as singletons in `src/Njord/Configuration/NjordServiceSetup.cs`

## 7. Update tests

- [x] 7.1 Update all tests that call `IndexResult.Compute(...)` directly — instantiate `IndexComputer` and call through it
- [x] 7.2 Update all tests that call `ConsensusSnapshot.Compute(...)` directly — instantiate `ConsensusSnapshotFactory` and call through it
- [x] 7.3 Update all tests that call `TrendResult.Compute(...)`, `DerivedResult.Compute(...)`, `HistoryResult.Compute(...)` directly
- [x] 7.4 Update enrichment feature tests that now need to inject the computer into the feature constructor

## 8. Validation

- [x] 8.1 Build succeeds: `dotnet build Njord.slnx` from `src/`
- [x] 8.2 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 8.3 Run `dotnet slopwatch` from repo root
- [x] 8.4 Run `dotnet format --verify-no-changes` from `src/`
