## 1. Persistence Events and State

- [x] 1.1 Create `src/Njord/Enrichment/ForecastHistory.cs` with: `ForecastRecord` (timestamp, location, per-model values dict, consensus values dict), `ForecastHistory` (IReadOnlyList<ForecastRecord>, retention-aware Add/Query), persistence events `ForecastRecorded`, and messages `RecordSnapshot`, `QueryHistory`, `HistoryResponse`
- [x] 1.2 Create `src/Njord/Enrichment/ForecastHistoryActor.cs` as `ReceivePersistentActor` with PersistenceId `"forecast-history-{location}"`, Recover/Command handlers for `ForecastRecorded`, snapshot every 100 events, responds to `QueryHistory` with current state

## 2. HistoryAnalyzer — Pure Computation Functions

- [x] 2.1 Create `src/Njord/Enrichment/HistoryAnalyzer.cs` with `ModelAccuracy(ForecastHistory, ParameterDef, int windowDays)` returning `Dictionary<WeatherModel, double?>` of MAE per model; null if < 48 pairs
- [x] 2.2 Add `ModelWeights(Dictionary<WeatherModel, double?> maeByModel)` returning `Dictionary<WeatherModel, double>` with normalized inverse-MAE weights; equal weights if all null
- [x] 2.3 Add `WeightedConsensus(IReadOnlyList<(WeatherModel, double?)> modelValues, Dictionary<WeatherModel, double> weights)` returning `double?` weighted mean
- [x] 2.4 Add `ForecastDrift(ForecastHistory, WeatherModel, ParameterDef, int runCount = 5)` returning `double?` standard deviation across last N runs for same target hour; null if < 2
- [x] 2.5 Add `SeasonalPreference(ForecastHistory, ParameterDef, DateTimeOffset now)` returning `WeatherModel?` with lowest seasonal MAE; null if insufficient data
- [x] 2.6 Add `AnomalyDetection(ForecastHistory, ParameterDef, double currentValue, int hourOfDay)` returning `(bool IsAnomaly, double DeviationSigma)?`; anomaly when > 2σ; null if < 30 records
- [x] 2.7 Create `src/Njord.Tests/Enrichment/HistoryAnalyzerSpec.cs` with tests for all six functions

## 3. HistoryResult — Result Record and Serialization

- [x] 3.1 Create `src/Njord/Enrichment/HistoryResult.cs` with `HistoryResult` record holding per-model MAE/weights/drift, seasonal best, anomaly, weighted consensus values
- [x] 3.2 Add static `HistoryResult.Compute(ForecastHistory, ModelSnapshot current, string location, ResolvedParameterSet, TimeProvider, HistoryOptions)`
- [x] 3.3 Add `ToMqttMessages(string baseTopic)` producing one `MqttMessage` on `{baseTopic}/{location}/history`
- [x] 3.4 Create `src/Njord.Tests/Enrichment/HistoryResultSpec.cs`

## 4. Topic Scheme and Discovery

- [x] 4.1 Add `HistoryDeviceId(string location)` and `HistoryTopic(string baseTopic, string location)` to `src/Njord/Egress/TopicScheme.cs`
- [x] 4.2 Add `BuildHistory(string location, IReadOnlyList<string> modelIds, MqttOptions mqtt, TimeSpan pollInterval, string version)` to `src/Njord/Egress/DiscoveryPayloadBuilder.cs`
- [x] 4.3 Extend `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` with history device tests

## 5. Configuration

- [x] 5.1 Add `HistoryOptions` class (Enabled=false, RetentionDays=30, MinSampleSize=48, SnapshotInterval=100) to `src/Njord/Configuration/EnrichmentOptions.cs`

## 6. EnrichmentActor Consumer Stream

- [x] 6.1 Add history consumer in `src/Njord/Enrichment/EnrichmentActor.cs`: create per-location ForecastHistoryActor children, forward snapshots, query history, compute HistoryResult, delta-publish, gated by `HistoryOptions.Enabled`
- [x] 6.2 Extend `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` with history consumer tests

## 7. Discovery Integration

- [x] 7.1 Wire `BuildHistory` into discovery in `MqttEgressActor`, gated by `History.Enabled`

## 8. Persistence Tests

- [x] 8.1 Create `src/Njord.Tests/Enrichment/ForecastHistoryActorSpec.cs` with Akka TestKit tests: persist and recover, snapshot taken, query responds with state

## 9. Validation

- [x] 9.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass
- [x] 9.2 Run `dotnet slopwatch` from repo root — no regressions flagged
- [x] 9.3 Verify build: `dotnet build Njord.slnx` from `src/` — no warnings
