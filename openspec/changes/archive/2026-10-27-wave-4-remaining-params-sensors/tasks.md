## 1. Weather Params Infrastructure

- [x] 1.1 Add `WeatherParams` flags: `SurfacePressure`, `WindSpeed80m`, `WindDirection80m`, `WindSpeed120m`, `WindDirection120m`, `Temperature80m`, `Temperature120m` in `src/Njord.Forecast/Weather/WeatherParams.cs`
- [x] 1.2 Add corresponding fields to `WeatherPoint` in `src/Njord.Forecast/Weather/WeatherPoint.cs` and `HasFields` switch cases
- [x] 1.3 Add `WeatherApiVariable` enum values with `[JsonStringEnumMemberName]`: `surface_pressure`, `wind_speed_80m`, `wind_direction_80m`, `wind_speed_120m`, `wind_direction_120m`, `temperature_80m`, `temperature_120m` in `src/Njord.OpenMeteo/Weather/WeatherApiVariable.cs`
- [x] 1.4 Add mapping in `src/Njord.OpenMeteo/Weather/WeatherApiVariableMappingExtensions.cs` (both directions)
- [x] 1.5 Add properties to `WeatherHourlyData` in `src/Njord.OpenMeteo/Weather/WeatherResponse.cs` with correct `[JsonPropertyName]`
- [x] 1.6 Add to `WeatherPoint` construction in `src/Njord.OpenMeteo/Weather/WeatherResponseMappingExtensions.cs`
- [x] 1.7 Tests: update `src/Njord.Forecast.Tests/Weather/WeatherPointTests.cs`, update `src/Njord.OpenMeteo.Tests/Mapping/ParamsMappingsTests.cs`

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 2. AirQuality Params Infrastructure

- [x] 2.1 Add `AirQualityParams` flags: `CarbonDioxide`, `Methane`, `UsAqiPm25`, `UsAqiPm10`, `UsAqiNitrogenDioxide`, `UsAqiOzone`, `UsAqiSulphurDioxide`, `UsAqiCarbonMonoxide` in `src/Njord.Forecast/AirQuality/AirQualityParams.cs`
- [x] 2.2 Add corresponding fields to `AirQualityPoint` in `src/Njord.Forecast/AirQuality/AirQualityPoint.cs` and `HasFields` switch cases
- [x] 2.3 Add `AirQualityApiVariable` enum values: `carbon_dioxide`, `methane`, `us_aqi_pm2_5`, `us_aqi_pm10`, `us_aqi_nitrogen_dioxide`, `us_aqi_ozone`, `us_aqi_sulphur_dioxide`, `us_aqi_carbon_monoxide` in `src/Njord.OpenMeteo/AirQuality/AirQualityApiVariable.cs`
- [x] 2.4 Add mapping in `src/Njord.OpenMeteo/AirQuality/AirQualityApiVariableMappingExtensions.cs`
- [x] 2.5 Add properties to `AirQualityHourlyData` in `src/Njord.OpenMeteo/AirQuality/AirQualityResponse.cs` with correct `[JsonPropertyName]`
- [x] 2.6 Add to `AirQualityPoint` construction in `src/Njord.OpenMeteo/AirQuality/AirQualityResponseMappingExtensions.cs`
- [x] 2.7 Tests: update AQ point and mapping tests

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 3. Marine Params Infrastructure

- [x] 3.1 Add `MarineParams` flags: `SecondarySwellWaveHeight`, `SecondarySwellWaveDirection`, `SecondarySwellWavePeriod`, `TertiarySwellWaveHeight`, `TertiarySwellWaveDirection`, `TertiarySwellWavePeriod`, `InvertBarometerHeight` in `src/Njord.Forecast/Marine/MarineParams.cs`
- [x] 3.2 Add corresponding fields to `MarinePoint` in `src/Njord.Forecast/Marine/MarinePoint.cs` and `HasFields` switch cases
- [x] 3.3 Add `MarineApiVariable` enum values: `secondary_swell_wave_height`, `secondary_swell_wave_direction`, `secondary_swell_wave_period`, `tertiary_swell_wave_height`, `tertiary_swell_wave_direction`, `tertiary_swell_wave_period`, `invert_barometer_height` in `src/Njord.OpenMeteo/Marine/MarineApiVariable.cs`
- [x] 3.4 Add mapping in `src/Njord.OpenMeteo/Marine/MarineApiVariableMappingExtensions.cs`
- [x] 3.5 Add properties to `MarineHourlyData` in `src/Njord.OpenMeteo/Marine/MarineResponse.cs` with correct `[JsonPropertyName]`
- [x] 3.6 Add to `MarinePoint` construction in `src/Njord.OpenMeteo/Marine/MarineResponseMappingExtensions.cs`
- [x] 3.7 Tests: update Marine point and mapping tests

### Validation
```
dotnet run --project Njord.Forecast.Tests/Njord.Forecast.Tests.csproj
dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
```

## 4. gRPC Proto and Mapping

- [x] 4.1 Update `protos/njord/v2/weather.proto`: add new fields to `WeatherSample`, `AirQualitySample`, `MarineSample`
- [x] 4.2 Update `src/Njord.Grpc/Mapping/DomainDataProtoMappingExtensions.cs` for new weather/AQ/marine fields
- [x] 4.3 Tests: update `src/Njord.Grpc.Tests/` for new proto fields

### Validation
```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 5. Elevated Wind Sensors Feature

- [x] 5.1 Create `ElevatedWindConfig` in `src/Njord.Features/Conditions/ElevatedWind/ElevatedWindConfig.cs` with `Enabled` (default false), section `Njord:Features:Energy:ElevatedWind`
- [x] 5.2 Create `ElevatedWind` feature rule in `src/Njord.Features/Conditions/ElevatedWind/ElevatedWind.cs` requiring `WeatherParams.WindSpeed80m | WindDirection80m | WindSpeed120m | WindDirection120m`
- [x] 5.3 Register in `src/Njord.Features/ServiceCollectionExtensions.cs`
- [x] 5.4 Tests: `ElevatedWindTests` in `src/Njord.Features.Tests/Conditions/ElevatedWind/`

### Validation
```
dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
```

## 6. Integration and Documentation

- [x] 6.1 Update `docs/data/features.json` with ElevatedWind entry
- [ ] 6.2 Update Config Builder types/budget if needed
- [ ] 6.3 Update `AGENTS.md` with new variable counts

### Validation
```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
```

Full test suite:
```bash
for p in Njord.*Tests; do
  [ "$p" = Njord.Tests.Shared ] && continue
  dotnet run --project "$p/$p.csproj" --no-build
done
```
