## 1. EnergyForecaster — Pure Computation Functions

- [x] 1.1 Create `src/Njord/Enrichment/EnergyForecaster.cs` with `HeatingDemand(double? temp, double? wind, double? cloudCover, double heatingBase = 18)` returning `int` (0–100)
- [x] 1.2 Add `CopEstimate(double? outdoorTemp, double flowTemp = 35, double carnotEfficiency = 0.45)` returning `double?`; Carnot-based COP; null when outdoor ≥ flow or null
- [x] 1.3 Add `CopOptimalHours(ForecastSeries series, ParameterDef tempParam, double flowTemp, double carnotEfficiency, int count, DateTimeOffset now)` returning `IReadOnlyList<(int HoursFromNow, double Cop)>` — top N hours by COP in next 24h
- [x] 1.4 Add `ShadingScore(double? radiation, double? isDay, double? temp)` returning `int` (0–100)
- [x] 1.5 Add `BatteryStrategy(int solarYield, double? isDay)` returning `string` ("charge"/"hold"/"discharge")
- [x] 1.6 Add `NightCoolingPotential(ForecastSeries series, ParameterDef tempParam, ParameterDef humidityParam, ParameterDef windParam, ParameterDef rainProbParam, double indoorTemp, DateTimeOffset now)` returning `int` (0–100) evaluating overnight hours 22:00–06:00
- [x] 1.7 Create `src/Njord.Tests/Enrichment/EnergyForecasterSpec.cs` with tests for all six functions

## 2. EnergyResult — Result Record and Serialization

- [x] 2.1 Create `src/Njord/Enrichment/EnergyResult.cs` with `EnergyResult` record holding all energy values
- [x] 2.2 Add static `EnergyResult.Compute(ModelSnapshot, string location, ResolvedParameterSet, TimeProvider, EnergyOptions)`
- [x] 2.3 Add `ToMqttMessages(string baseTopic)` producing one `MqttMessage` on `{baseTopic}/{location}/energy`; cop_optimal as JSON array
- [x] 2.4 Create `src/Njord.Tests/Enrichment/EnergyResultSpec.cs`

## 3. Topic Scheme and Discovery

- [x] 3.1 Add `EnergyDeviceId(string location)` and `EnergyTopic(string baseTopic, string location)` to `src/Njord/Egress/TopicScheme.cs`
- [x] 3.2 Add `BuildEnergy(string location, MqttOptions mqtt, TimeSpan pollInterval, string version)` to `src/Njord/Egress/DiscoveryPayloadBuilder.cs`
- [x] 3.3 Extend `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` with energy device tests

## 4. Configuration

- [x] 4.1 Add `EnergyOptions` class (Enabled=false, FlowTemp=35, CarnotEfficiency=0.45, HeatingBaseTemp=18, CopOptimalHours=3, IndoorTemp=22) to `src/Njord/Configuration/EnrichmentOptions.cs`

## 5. EnrichmentActor Consumer Stream

- [x] 5.1 Add energy consumer stream in `src/Njord/Enrichment/EnrichmentActor.cs`, gated by `EnergyOptions.Enabled`
- [x] 5.2 Extend `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` with energy consumer tests

## 6. Discovery Integration

- [x] 6.1 Wire `BuildEnergy` into discovery in `MqttEgressActor`, gated by `Energy.Enabled`

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass
- [x] 7.2 Run `dotnet slopwatch` from repo root — no regressions flagged
- [x] 7.3 Verify build: `dotnet build Njord.slnx` from `src/` — no warnings
