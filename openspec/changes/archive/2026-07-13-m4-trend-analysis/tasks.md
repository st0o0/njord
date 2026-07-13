## 1. TrendAnalyzer — Pure Computation Functions

- [x] 1.1 Create `src/Njord/Enrichment/TrendAnalyzer.cs` with `TrendDirection(double? prevMedian, double? currMedian, double threshold)` returning `(string Direction, double Delta)?`; "rising"/"falling"/"stable" based on delta vs threshold; null if either input null
- [x] 1.2 Add `WeatherChange(int? prevCode, int? currCode)` returning `WeatherChangeResult?` record with FromCategory/ToCategory/Description; WMO codes grouped into categories (clear, fog, drizzle, rain, snow, showers, thunderstorm); null if same category or either null
- [x] 1.3 Add `PrecipitationTiming(ForecastSeries series, ParameterDef precipParam, DateTimeOffset now)` returning `(int? StartsInHours, int? EndsInHours)`; scans next 24h for first/last non-zero precipitation
- [x] 1.4 Add `ExtremaTiming(ForecastSeries series, ParameterDef tempParam, DateTimeOffset now)` returning `(int? MaxInHours, int? MinInHours)`; finds hour of max/min temperature in next 24h; null if < 2 data points
- [x] 1.5 Add `ConsensusStability(double? prevIqr, double? currIqr)` returning `(string Label, double Ratio)?`; "converging" (< 0.8), "diverging" (> 1.2), "stable"; null if either null or prev is 0
- [x] 1.6 Add `PredictabilityDecay(IReadOnlyList<(int HorizonHours, double? Spread)> spreads, double spreadThreshold = 3.0)` returning `(double DecayRate, int? ReliableHours)?`; linear regression slope of spread vs hours; ReliableHours = first horizon where spread > threshold; null if < 2 data points
- [x] 1.7 Create `src/Njord.Tests/Enrichment/TrendAnalyzerSpec.cs` with tests for all six functions covering spec scenarios

## 2. TrendResult — Result Record and Serialization

- [x] 2.1 Create `src/Njord/Enrichment/TrendResult.cs` with records: `ParameterTrend(string Direction, double Delta)`, `WeatherChangeResult(string FromCategory, string ToCategory, string Description)`, and `TrendResult(string Location, ...)` holding all trend fields
- [x] 2.2 Add static `TrendResult.Compute(ModelSnapshot current, ModelSnapshot? previous, string location, IReadOnlyList<int> horizons, ResolvedParameterSet parameters, TimeProvider)` that computes consensus on both snapshots and calls each TrendAnalyzer function
- [x] 2.3 Add `ToMqttMessages(string baseTopic)` producing one `MqttMessage` on topic `{baseTopic}/{location}/trends` with flat JSON payload containing all trend data; null values serialize as JSON null
- [x] 2.4 Create `src/Njord.Tests/Enrichment/TrendResultSpec.cs` testing Compute orchestration and ToMqttMessages serialization

## 3. Topic Scheme and Discovery

- [x] 3.1 Add `TrendDeviceId(string location)` and `TrendTopic(string baseTopic, string location)` to `src/Njord/Egress/TopicScheme.cs`
- [x] 3.2 Add `BuildTrends(string location, MqttOptions mqtt, TimeSpan pollInterval, string version)` to `src/Njord/Egress/DiscoveryPayloadBuilder.cs` producing a device payload with text sensors for trends, numeric sensors for timing/decay
- [x] 3.3 Extend `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` with tests for trend device discovery payload

## 4. Configuration

- [x] 4.1 Add `TrendOptions` class (with `Enabled` bool, default `false`) to `src/Njord/Configuration/EnrichmentOptions.cs` and wire into `EnrichmentOptions.Trends`

## 5. EnrichmentActor Consumer Stream

- [x] 5.1 Add trend consumer stream materialization in `src/Njord/Enrichment/EnrichmentActor.cs`: subscribe to BroadcastHub, use Scan to pair current/previous snapshots, call `TrendResult.Compute`, delta-publish via SinkRef, gated by `TrendOptions.Enabled`
- [x] 5.2 Extend `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` with tests verifying trend consumer is materialized when enabled and skipped when disabled

## 6. Discovery Integration

- [x] 6.1 Wire `BuildTrends` into the discovery publish flow in `MqttEgressActor` (alongside consensus, alert, and derived discovery), gated by `Trends.Enabled`

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass
- [x] 7.2 Run `dotnet slopwatch` from repo root — no regressions flagged
- [x] 7.3 Verify build: `dotnet build Njord.slnx` from `src/` — no warnings
