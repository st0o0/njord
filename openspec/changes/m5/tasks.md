## 1. IndexScorer — Pure Computation Functions

- [x] 1.1 Create `src/Njord/Enrichment/IndexScorer.cs` with helper `Clamp(double, 0, 100)` and sub-score functions: `TempScore`, `HumidityScore`, `WindScore`, `RainScore`, `SunshineScore`, `CloudScore`, `TempComfort` (bell curve at 22 °C), `RunTempScore` (bell curve 5–20 °C)
- [x] 1.2 Add `LaundryDrying(double? temp, double? humidity, double? wind, double? rainProb, double? sunshinePct)` returning `int` (0–100) with weights 0.3/0.25/0.2/0.15/0.1
- [x] 1.3 Add `OutdoorScore(double? temp, double? rainProb, double? wind, double? cloudCover)` returning `int` (0–100) with weights 0.35/0.25/0.2/0.2
- [x] 1.4 Add `RunningComfort(double? temp, double? humidity, double? wind, double? rainProb)` returning `int` (0–100)
- [x] 1.5 Add `CyclingComfort(double? temp, double? humidity, double? wind, double? rainProb)` returning `int` (0–100) with wind weighted 0.3
- [x] 1.6 Add `BbqWeather(double? temp, double? humidity, double? wind, double? rainProb)` returning `int` (0–100) with rain weighted 0.35
- [x] 1.7 Add `IrrigationNeed(double? rainProb, double? temp, double? humidity, double? et)` returning `int` (0–100)
- [x] 1.8 Add `HeatingDegreeDays(double meanTemp, double baseTemp = 18)` and `CoolingDegreeDays(double meanTemp, double baseTemp = 24)` returning `double`
- [x] 1.9 Add `SolarYield(double? radiation, double? cloudCover, double? temp)` returning `int` (0–100) with temp efficiency penalty above 25 °C
- [x] 1.10 Add `Ventilation(double? outdoorTemp, double indoorTemp, double? humidity, double? wind, double? rainProb)` returning `int` (0–100)
- [x] 1.11 Add `FrostProtection(ForecastSeries series, ParameterDef tempParam, DateTimeOffset now)` returning `(int HoursUntilFrost, double Confidence)?`; scans next 48h
- [x] 1.12 Add `VpdCategory(double? temp, double? humidity)` returning `(string Category, double Vpd)?` using Magnus formula
- [x] 1.13 Create `src/Njord.Tests/Enrichment/IndexScorerSpec.cs` with tests for all index functions covering spec scenarios

## 2. IndexResult — Result Record and Serialization

- [x] 2.1 Create `src/Njord/Enrichment/IndexResult.cs` with `IndexResult` record holding all index values
- [x] 2.2 Add static `IndexResult.Compute(ModelSnapshot, string location, ResolvedParameterSet, TimeProvider, IndexOptions)` that extracts 24h-mean values via consensus median and calls each IndexScorer function
- [x] 2.3 Add `ToMqttMessages(string baseTopic)` producing one `MqttMessage` on topic `{baseTopic}/{location}/indices` with flat JSON
- [x] 2.4 Create `src/Njord.Tests/Enrichment/IndexResultSpec.cs` testing Compute orchestration and ToMqttMessages

## 3. Topic Scheme and Discovery

- [x] 3.1 Add `IndexDeviceId(string location)` and `IndexTopic(string baseTopic, string location)` to `src/Njord/Egress/TopicScheme.cs`
- [x] 3.2 Add `BuildIndices(string location, MqttOptions mqtt, TimeSpan pollInterval, string version)` to `src/Njord/Egress/DiscoveryPayloadBuilder.cs`
- [x] 3.3 Extend `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` with tests for index device

## 4. Configuration

- [x] 4.1 Add `IndexOptions` class (Enabled=false, HeatingBaseTemp=18, CoolingBaseTemp=24, IndoorTemp=22) to `src/Njord/Configuration/EnrichmentOptions.cs` and wire into `EnrichmentOptions.Indices`

## 5. EnrichmentActor Consumer Stream

- [x] 5.1 Add index consumer stream in `src/Njord/Enrichment/EnrichmentActor.cs`, gated by `IndexOptions.Enabled`
- [x] 5.2 Extend `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` with index consumer tests

## 6. Discovery Integration

- [x] 6.1 Wire `BuildIndices` into discovery in `MqttEgressActor`, gated by `Indices.Enabled`

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass
- [x] 7.2 Run `dotnet slopwatch` from repo root — no regressions flagged
- [x] 7.3 Verify build: `dotnet build Njord.slnx` from `src/` — no warnings
