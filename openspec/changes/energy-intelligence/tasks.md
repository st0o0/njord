# Tasks: energy-intelligence

## 1. HeatPumpCOP — feature, config, enums, result

- [ ] Create `src/Njord.Features/Conditions/HeatPump/HeatPumpRule.cs` — `FeatureRule<ForecastInput>`, category Energy, name "HeatPump", default off, requires `ForecastParams.Temperature`, PollDepth 0. Constructor takes `IOptions<HeatPumpConfig>`. `Project` extracts `ForecastInput`. `Evaluate` computes COP per hour via `6.81 - 0.121 * (targetTemp - outdoorTemp)`, clamped [1.0, 8.0], maps to `HeatPumpRecommendation`, returns `HeatPumpCopResult`.
- [ ] Create `src/Njord.Features/Conditions/HeatPump/HeatPumpConfig.cs` — `IConfigSection` at `Njord:Features:Energy:HeatPump` with `Enabled`, `TargetTemperature` (int, default 35), `SystemType` (enum: FloorHeating/Radiator/HotWater, default FloorHeating), `ForecastHours` (int, default 24).
- [ ] Create `src/Njord.Features/Conditions/HeatPump/HeatPumpConfigValidator.cs` — `IValidateOptions<HeatPumpConfig>`: TargetTemperature 25-65, ForecastHours 1-48. If TargetTemperature is at default (35) and SystemType differs, apply SystemType default (Radiator→55, HotWater→60).
- [ ] Add `HeatPumpRecommendation` enum to `src/Njord.Messages/Pipeline/` (or a new file `src/Njord.Messages/Pipeline/EnergyEnums.cs`): `Ideal`, `Normal`, `Inefficient`, `Avoid`.
- [ ] Add `HourlyCopEntry` record and `HeatPumpCopResult : ConditionFeatureResult` to `src/Njord.Messages/Pipeline/NonAlertResults.cs`.
- [ ] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<HeatPumpConfig, HeatPumpConfigValidator>()` and `AddSingleton<IFeatureRule, HeatPumpRule>()`.

## 2. HeatPumpCOP — proto + gRPC mapping

- [ ] Add to `protos/njord/v2/weather.proto`: enum `HeatPumpRecommendation` (UNSPECIFIED=0, IDEAL=1, NORMAL=2, INEFFICIENT=3, AVOID=4), message `HourlyCop` (Timestamp time, double cop, HeatPumpRecommendation recommendation), message `HeatPumpCop` (ConditionData condition, double cop, HeatPumpRecommendation recommendation, repeated HourlyCop hourly). Add `HeatPumpCop heat_pump_cop = 76;` to `EnrichmentEvent.feature` oneof.
- [ ] Add `ToProto(this HeatPumpCopResult)` to `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs` — maps Recommendation enum, each HourlyCopEntry to HourlyCop.
- [ ] Add `HeatPumpCopResult r => new EnrichmentEvent { ... HeatPumpCop = r.ToProto() }` case to `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs`.
- [ ] Add `HeatPumpRecommendation` mapping extension in `src/Njord.Grpc/Mapping/` (domain enum → proto enum switch, one arm per member).

## 3. HeatPumpCOP — tests

- [ ] Create `src/Njord.Features.Tests/Conditions/HeatPump/HeatPumpRuleTests.cs` — test COP calculation at various temperatures (e.g., -10°C → COP ~1.4 Avoid, 0°C → ~2.6 Normal, 10°C → ~3.8 Normal, 15°C → ~4.4 Ideal), test ForecastHours limit, test disabled returns null.
- [ ] Create `src/Njord.Features.Tests/Conditions/HeatPump/HeatPumpConfigValidatorTests.cs` — test validation bounds, SystemType default application.
- [ ] Add proto mapping test case to `src/Njord.Grpc.Tests/EnrichmentEventMappingsTests.cs`.

## 4. SolarBatteryStrategy — feature, config, enums, result

- [ ] Create `src/Njord.Features/Conditions/SolarBattery/SolarBatteryRule.cs` — `FeatureRule<ForecastSolarInput>`, category Energy, name "SolarBattery", default off, requires Forecast `ShortwaveRadiation` + Satellite `GlobalTilted`, PollDepth 1. Constructor takes `IOptions<SolarBatteryConfig>` + `IOptions<SolarYieldOptions>`. `Project` extracts `ForecastSolarInput` (null if Forecast missing). `Evaluate` computes today kWh from satellite GTI, tomorrow kWh from forecast irradiance, compares ratios to determine strategy.
- [ ] Create `src/Njord.Features/Conditions/SolarBattery/SolarBatteryConfig.cs` — `IConfigSection` at `Njord:Features:Energy:SolarBattery` with `Enabled`, `BatteryCapacityKwh` (double, 10.0), `UsablePercent` (int, 90), `MinSocPercent` (int, 20), `SystemEfficiency` (double, 0.85).
- [ ] Create `src/Njord.Features/Conditions/SolarBattery/SolarBatteryConfigValidator.cs` — `IValidateOptions<SolarBatteryConfig>`: capacity > 0, usable 50-100, minSoc 0-50, efficiency 0.5-1.0. Cross-validate: check `SolarYieldOptions.PeakKw > 0` when enabled.
- [ ] Add `BatteryStrategy` enum to `src/Njord.Messages/Pipeline/EnergyEnums.cs`: `FeedIn`, `ChargeBattery`, `Conserve`, `Normal`.
- [ ] Add `HourlySolarEntry` record and `SolarBatteryStrategyResult : ConditionFeatureResult` to `src/Njord.Messages/Pipeline/NonAlertResults.cs`.
- [ ] Register in `src/Njord.Features/ServiceCollectionExtensions.cs`: `AddNjordOptions<SolarBatteryConfig, SolarBatteryConfigValidator>()` and `AddSingleton<IFeatureRule, SolarBatteryRule>()`.

## 5. SolarBatteryStrategy — proto + gRPC mapping

- [ ] Add to `protos/njord/v2/weather.proto`: enum `BatteryStrategy` (UNSPECIFIED=0, FEED_IN=1, CHARGE_BATTERY=2, CONSERVE=3, NORMAL=4), message `HourlySolar` (Timestamp time, double estimated_kwh), message `SolarBatteryStrategy` (ConditionData condition, BatteryStrategy strategy, double today_kwh, double tomorrow_kwh_estimate, int32 confidence_percent, repeated HourlySolar hourly). Add `SolarBatteryStrategy solar_battery_strategy = 77;` to `EnrichmentEvent.feature` oneof.
- [ ] Add `ToProto(this SolarBatteryStrategyResult)` to `src/Njord.Grpc/Mapping/FeatureResultMappingExtensions.cs`.
- [ ] Add `SolarBatteryStrategyResult` case to `src/Njord.Grpc/Mapping/EnrichmentEventMappingExtensions.cs`.
- [ ] Add `BatteryStrategy` mapping extension (domain → proto switch).

## 6. SolarBatteryStrategy — tests

- [ ] Create `src/Njord.Features.Tests/Conditions/SolarBattery/SolarBatteryRuleTests.cs` — test strategy determination: sunny today + sunny tomorrow → FeedIn, sunny today + cloudy tomorrow → ChargeBattery, cloudy today + sunny tomorrow → Conserve, balanced → Normal. Test with and without satellite data. Test disabled returns null.
- [ ] Create `src/Njord.Features.Tests/Conditions/SolarBattery/SolarBatteryConfigValidatorTests.cs` — test bounds, cross-validation with SolarYieldOptions.
- [ ] Add proto mapping test case to `src/Njord.Grpc.Tests/EnrichmentEventMappingsTests.cs`.

## 7. ha-njord — entity descriptions + translations

- [ ] Add `LifestyleSensorDescription` entries for `heat_pump_cop` (numeric, unit "COP") and `solar_battery_strategy` (enum with options) to `D:\GIT\ha-njord/custom_components/njord/lifestyle.py`.
- [ ] Add entity translations to `strings.json`, `translations/en.json`, `translations/de.json` at `D:\GIT\ha-njord/custom_components/njord/` per the proposal's i18n section. HeatPumpCop sensor name + recommendation state_attributes; SolarBatteryStrategy sensor name + strategy states.

## 8. docs/data/features.json + Config Builder

- [ ] Add `HeatPump` and `SolarBattery` entries to `docs/data/features.json` with category Energy, options, defaults, and requirements. Update the Config Builder if budget formula entries are needed (these features add 0-1 calls, negligible).

## 9. Validation

- [ ] `dotnet build src/Njord.slnx`
- [ ] `dotnet format src/Njord.slnx --verify-no-changes`
- [ ] Run all test projects:
  ```bash
  for p in src/Njord.*Tests; do
    [ "$p" = "src/Njord.Tests.Shared" ] && continue
    dotnet run --project "$p/$( basename $p ).csproj" --no-build
  done
  ```
