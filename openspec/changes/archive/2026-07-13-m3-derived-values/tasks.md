## 1. DerivedComputer — Pure Computation Functions

- [x] 1.1 Create `src/Njord/Enrichment/DerivedComputer.cs` with `Beaufort(double? windSpeedMs)` returning `int?` (0–12) using standard Beaufort scale boundaries
- [x] 1.2 Add `WindChill(double? tempC, double? windSpeedMs)` returning `double?` using the North American formula; null when T > 10 °C or V ≤ 1.33 m/s or inputs null
- [x] 1.3 Add `DewPointComfort(double? dewPointC)` returning `string?` comfort category (dry/comfortable/sticky/oppressive/dangerous)
- [x] 1.4 Add `DiurnalAmplitude(ForecastSeries series, ParameterDef tempParam, DateTimeOffset now)` returning `double?` (max − min temperature_2m in next 24h); null if < 2 non-null points
- [x] 1.5 Add `SunshinePercent(ForecastSeries series, ParameterDef sunshineDurationParam, ParameterDef isDayParam, DateTimeOffset now)` returning `double?` (0–100); null if sunshine_duration unavailable or no daylight hours
- [x] 1.6 Add `WmoDescription(int? weatherCode)` returning `string?` with static `IReadOnlyDictionary<int, string>` covering WMO 4677 codes 0–99
- [x] 1.7 Add `InversionDetected(double? pressureMsl, double? surfacePressure, double? temp2m, double? dewPoint)` returning `bool?`; true when pressure gap > 3 AND temp−dewpoint < 3
- [x] 1.8 Create `src/Njord.Tests/Enrichment/DerivedComputerSpec.cs` with tests for all seven functions covering all spec scenarios: Beaufort boundaries, wind chill edge cases, comfort categories, amplitude, sunshine, WMO codes, inversion heuristic

## 2. DerivedResult — Result Record and Serialization

- [x] 2.1 Create `src/Njord/Enrichment/DerivedResult.cs` with records: `HorizonDerived(int? Beaufort, double? WindChill, string? DewPointComfort, string? WmoDescription)`, `ScalarDerived(double? DiurnalAmplitude, double? SunshinePct, bool? Inversion)`, and `DerivedResult(string Location, IReadOnlyDictionary<string, HorizonDerived> ByHorizon, ScalarDerived Scalars)`
- [x] 2.2 Add static `DerivedResult.Compute(ModelSnapshot, string location, IReadOnlyList<int> horizons, ResolvedParameterSet parameters, TimeProvider)` that extracts median values across models at each horizon and calls each DerivedComputer function
- [x] 2.3 Add `ToMqttMessages(string baseTopic)` producing one `MqttMessage` per horizon (topic `{baseTopic}/{location}/derived/h{hours}`, JSON with beaufort/wind_chill/dewpoint_comfort/wmo_description) plus one meta message (topic `{baseTopic}/{location}/derived/meta`, JSON with diurnal_amplitude/sunshine_pct/inversion)
- [x] 2.4 Create `src/Njord.Tests/Enrichment/DerivedResultSpec.cs` testing Compute orchestration and ToMqttMessages serialization

## 3. Topic Scheme and Discovery

- [x] 3.1 Add `DerivedDeviceId(string location)`, `DerivedHorizonTopic(string baseTopic, string location, string horizon)`, and `DerivedMetaTopic(string baseTopic, string location)` to `src/Njord/Egress/TopicScheme.cs`
- [x] 3.2 Add `BuildDerived(string location, IReadOnlyList<int> horizons, MqttOptions mqtt, TimeSpan pollInterval, string version)` to `src/Njord/Egress/DiscoveryPayloadBuilder.cs` producing a device payload with sensor components for horizon-based derived values (beaufort, wind_chill, dewpoint_comfort, wmo_description per horizon) and scalar sensors (diurnal_amplitude, sunshine_pct as sensor, inversion as binary_sensor)
- [x] 3.3 Create or extend `src/Njord.Tests/Egress/DerivedDiscoverySpec.cs` with Verify snapshot tests for the derived device discovery payload

## 4. Configuration

- [x] 4.1 Add `DerivedOptions` class (with `Enabled` bool, default `true`) to `src/Njord/Configuration/EnrichmentOptions.cs` and wire into `EnrichmentOptions.Derived`

## 5. EnrichmentActor Consumer Stream

- [x] 5.1 Add derived consumer stream materialization in `src/Njord/Enrichment/EnrichmentActor.cs`: subscribe to BroadcastHub, call `DerivedResult.Compute`, delta-publish via SinkRef, gated by `DerivedOptions.Enabled`
- [x] 5.2 Extend `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` (or create if needed) with tests verifying derived consumer is materialized when enabled and skipped when disabled

## 6. Discovery Integration

- [x] 6.1 Wire `BuildDerived` into the discovery publish flow in `MqttEgressActor` (alongside consensus and alert discovery), gated by `Derived.Enabled`

## 7. Validation

- [x] 7.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass
- [x] 7.2 Run `dotnet slopwatch` from repo root — no regressions flagged
- [x] 7.3 Verify build: `dotnet build Njord.slnx` from `src/` — no warnings
