## 1. Weather Variable Infrastructure

- [x] 1.1 Add 11 new flags to `WeatherParams` in `src/Njord.Forecast/Weather/WeatherParams.cs`: `Showers`, `SoilMoisture0To1cm`, `SoilMoisture1To3cm`, `Evapotranspiration`, `VapourPressureDeficit`, `BoundaryLayerHeight`, `CloudCoverLow`, `CloudCoverMid`, `CloudCoverHigh`, `ConvectiveInhibition`, `LiftedIndex`
- [x] 1.2 Add 11 corresponding nullable double fields to `WeatherPoint` in `src/Njord.Forecast/Weather/WeatherPoint.cs` with `HasFields` cases
- [x] 1.3 Add 11 entries to `WeatherApiVariable` in `src/Njord.OpenMeteo/Weather/WeatherApiVariable.cs` with `[JsonStringEnumMemberName]` attributes: `showers`, `soil_moisture_0_to_1cm`, `soil_moisture_1_to_3cm`, `et0_fao_evapotranspiration`, `vapour_pressure_deficit`, `boundary_layer_height`, `cloud_cover_low`, `cloud_cover_mid`, `cloud_cover_high`, `convective_inhibition`, `lifted_index`
- [x] 1.4 Add 11 fields to `WeatherHourlyData` in `src/Njord.OpenMeteo/Weather/WeatherResponse.cs` with matching `[JsonPropertyName]`
- [x] 1.5 Update `WeatherApiVariableMappingExtensions` in `src/Njord.OpenMeteo/Weather/WeatherApiVariableMappingExtensions.cs` with bidirectional mappings
- [x] 1.6 Update `WeatherResponseMappingExtensions.ToForecast` in `src/Njord.OpenMeteo/Weather/WeatherResponseMappingExtensions.cs` to include new fields in `WeatherPoint` construction
- [x] 1.7 Tests: update `src/Njord.Forecast.Tests/Weather/WeatherPointTests.cs` and `src/Njord.OpenMeteo.Tests/Mapping/ParamsMappingsTests.cs` for new params

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 2. Air Quality Variable Infrastructure

- [x] 2.1 Add 7 new flags to `AirQualityParams` in `src/Njord.Forecast/AirQuality/AirQualityParams.cs`: `RagweedPollen`, `EuropeanAqiPm25`, `EuropeanAqiPm10`, `EuropeanAqiNitrogenDioxide`, `EuropeanAqiOzone`, `EuropeanAqiSulphurDioxide`, `AerosolOpticalDepth`
- [x] 2.2 Add 7 corresponding nullable double fields to `AirQualityPoint` in `src/Njord.Forecast/AirQuality/AirQualityPoint.cs` with `HasFields` cases
- [x] 2.3 Add 7 entries to `AirQualityApiVariable` in `src/Njord.OpenMeteo/AirQuality/AirQualityApiVariable.cs` with `[JsonStringEnumMemberName]`
- [x] 2.4 Add 7 fields to `AirQualityHourlyData` in `src/Njord.OpenMeteo/AirQuality/AirQualityResponse.cs` with `[JsonPropertyName]`
- [x] 2.5 Update AQ mapping extensions for bidirectional variable mappings and point construction
- [x] 2.6 Tests: update AQ param mapping tests and point tests

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 3. Marine Variable Infrastructure

- [x] 3.1 Add 2 new flags to `MarineParams` in `src/Njord.Forecast/Marine/MarineParams.cs`: `OceanCurrentVelocity`, `OceanCurrentDirection`
- [x] 3.2 Add 2 corresponding nullable double fields to `MarinePoint` in `src/Njord.Forecast/Marine/MarinePoint.cs` with `HasFields` cases
- [x] 3.3 Add 2 entries to `MarineApiVariable` in `src/Njord.OpenMeteo/Marine/MarineApiVariable.cs`
- [x] 3.4 Add 2 fields to `MarineHourlyData` in `src/Njord.OpenMeteo/Marine/MarineResponse.cs`
- [x] 3.5 Update Marine mapping extensions and point construction
- [x] 3.6 Tests: update Marine param mapping and point tests

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 4. ShowerAlert Feature

- [x] 4.1 Create `ShowerConfig` in `src/Njord.Features/Alerts/Shower/ShowerConfig.cs` with `Enabled` (default false), `ThresholdMmPerHour` (default 5.0), and severity thresholds
- [x] 4.2 Create `ShowerAlert : FeatureRule<WeatherEnsembleInput>` in `src/Njord.Features/Alerts/Shower/ShowerAlert.cs` with requirements `Weather: Showers`, `Ensemble: TemperatureSpread`
- [x] 4.3 Add `ShowerAlertResult` to `src/Njord.Messages/Alerts/`
- [x] 4.4 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 4.5 Tests: `ShowerAlertTests` in `src/Njord.Features.Tests/Alerts/Shower/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 5. SoilMoisture Feature

- [x] 5.1 Create `SoilMoistureConfig` in `src/Njord.Features/Conditions/SoilMoisture/SoilMoistureConfig.cs` with `Enabled` (default false), `DryThreshold` (default 0.15), `WetThreshold` (default 0.35)
- [x] 5.2 Create `SoilMoisture : FeatureRule<WeatherInput>` in `src/Njord.Features/Conditions/SoilMoisture/SoilMoisture.cs` with requirements `Weather: SoilMoisture0To1cm`
- [x] 5.3 Add `SoilMoistureResult` to `src/Njord.Messages/`
- [x] 5.4 Register in `ServiceCollectionExtensions.cs`
- [x] 5.5 Tests: `SoilMoistureTests` in `src/Njord.Features.Tests/Conditions/SoilMoisture/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 6. Evapotranspiration Feature

- [x] 6.1 Create `EvapotranspirationConfig` in `src/Njord.Features/Conditions/Evapotranspiration/EvapotranspirationConfig.cs` with `Enabled` (default false), `LowMm` (default 2.0), `HighMm` (default 5.0)
- [x] 6.2 Create `Evapotranspiration : FeatureRule<WeatherInput>` in `src/Njord.Features/Conditions/Evapotranspiration/Evapotranspiration.cs` with requirements `Weather: Evapotranspiration`
- [x] 6.3 Add `EvapotranspirationResult` to `src/Njord.Messages/`
- [x] 6.4 Register in `ServiceCollectionExtensions.cs`
- [x] 6.5 Tests: `EvapotranspirationTests` in `src/Njord.Features.Tests/Conditions/Evapotranspiration/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 7. SmogRisk Feature

- [x] 7.1 Create `SmogRiskConfig` in `src/Njord.Features/Alerts/SmogRisk/SmogRiskConfig.cs` with `Enabled` (default false), `BoundaryLayerThresholdM` (default 500), `Pm25ThresholdUgm3` (default 25.0)
- [x] 7.2 Create a cross-domain input type `WeatherAqSmogInput` in `src/Njord.Core/Domain/FeatureInputs.cs` if no suitable type exists, or reuse `WeatherAqInput` if it carries the needed fields
- [x] 7.3 Create `SmogRisk : FeatureRule<WeatherAqInput>` in `src/Njord.Features/Alerts/SmogRisk/SmogRisk.cs` with requirements `Weather: BoundaryLayerHeight`, `AirQuality: Pm25`. Confidence always Medium.
- [x] 7.4 Add `SmogRiskResult` to `src/Njord.Messages/Alerts/`
- [x] 7.5 Register in `ServiceCollectionExtensions.cs`
- [x] 7.6 Tests: `SmogRiskTests` in `src/Njord.Features.Tests/Alerts/SmogRisk/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 8. RagweedPollen and GranularAqi Features

- [x] 8.1 Create `RagweedPollenConfig` in `src/Njord.Features/Conditions/RagweedPollen/RagweedPollenConfig.cs` (default disabled)
- [x] 8.2 Create `RagweedPollen` feature rule following the existing pollen sensor pattern (Birch/Grass/Alder/Mugwort in `src/Njord.Features/`)
- [x] 8.3 Create `GranularAqiConfig` in `src/Njord.Features/Conditions/GranularAqi/GranularAqiConfig.cs` (default disabled)
- [x] 8.4 Create `GranularAqi` feature rule that produces 5 sub-index sensor values
- [x] 8.5 Add result records to `src/Njord.Messages/`
- [x] 8.6 Register both in `ServiceCollectionExtensions.cs`
- [x] 8.7 Tests: `RagweedPollenTests` and `GranularAqiTests` in `src/Njord.Features.Tests/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 9. OceanCurrents Feature

- [x] 9.1 Create `OceanCurrentsConfig` in `src/Njord.Features/Conditions/OceanCurrents/OceanCurrentsConfig.cs` with `Enabled` (default false), `StrongCurrentThresholdKmh` (default 3.0)
- [x] 9.2 Create `OceanCurrents : FeatureRule<MarineWeatherInput>` in `src/Njord.Features/Conditions/OceanCurrents/OceanCurrents.cs` with requirements `Marine: OceanCurrentVelocity | OceanCurrentDirection`
- [x] 9.3 Add `OceanCurrentsResult` to `src/Njord.Messages/`
- [x] 9.4 Register in `ServiceCollectionExtensions.cs`
- [x] 9.5 Tests: `OceanCurrentsTests` in `src/Njord.Features.Tests/Conditions/OceanCurrents/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 10. gRPC, Proto, and Integration

- [x] 10.1 Update `protos/njord/v2/weather.proto`: add new fields to `WeatherSample`, `AirQualitySample`, `MarineSample`. Add new enrichment event oneof cases for each new feature
- [x] 10.2 Update `DomainDataProtoMappingExtensions` in `src/Njord.Grpc/Mapping/` for new point fields
- [x] 10.3 Update enrichment proto mappings for new feature results
- [x] 10.4 Update `docs/data/features.json` with all 7 new feature entries
- [x] 10.5 Update Config Builder (`docs/.vitepress/theme/builder/`) for new features and domain requirements
- [x] 10.6 Update ha-njord proto stubs, models, and grpc_client for new fields and features
- [x] 10.7 Update `AGENTS.md` feature counts and descriptions

### Validation
```
dotnet build Njord.slnx
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 11. Full Validation

- [x] 11.1 Run `dotnet format Njord.slnx --verify-no-changes`
- [x] 11.2 Run all test suites sequentially

```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
