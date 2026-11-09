# Tasks

## 1. AirQuality observations (6 features)

- [x] 1.1 Create 6 AirQuality observation features in `src/Njord.Features/Observations/` following the Pm25Observation pattern. Each needs `<Name>Observation.cs` (FeatureRule) + `<Name>ObservationConfig.cs` (IConfigSection) in its own folder. Input type: `AqInput`. Project: `forecast.AirQuality`. Evaluate: `ForecastQueries.ValueAt(ctx.Current.AirQuality, ctx.Now) is { <Field>: { } value }`. All use `FeatureCategory.AirQuality`. All disabled by default.

| Name | Param | Field | Unit | DeviceClass |
|------|-------|-------|------|-------------|
| Pm10Observation | AirQualityParams.Pm10 | Pm10 | µg/m³ | pm10 |
| OzoneObservation | AirQualityParams.Ozone | Ozone | µg/m³ | ozone |
| NitrogenDioxideObservation | AirQualityParams.NitrogenDioxide | NitrogenDioxide | µg/m³ | nitrogen_dioxide |
| EuropeanAqiObservation | AirQualityParams.EuropeanAqi | EuropeanAqi | (none) | aqi |
| CarbonMonoxideObservation | AirQualityParams.CarbonMonoxide | CarbonMonoxide | µg/m³ | carbon_monoxide |
| SulphurDioxideObservation | AirQualityParams.SulphurDioxide | SulphurDioxide | µg/m³ | sulphur_dioxide |

For EuropeanAqiObservation: Unit is empty string or omitted, DeviceClass "aqi". The AirQualityPoint field is `EuropeanAqi` (type `double?`).

- [x] 1.2 Create tests for the 6 AirQuality observations in `src/Njord.Features.Tests/Observations/<Name>/`. Each test class: `Current_value_is_reported` + `Missing_value_yields_null`. Build `LocationForecast` with the relevant field set on `AirQualityPoint`.

## 2. Marine observations (4 features)

- [x] 2.1 Create 4 Marine observation features following the WaveHeightObservation pattern. Input type: `MarineInput`. Project: `forecast.Marine`. Evaluate: `ForecastQueries.ValueAt(ctx.Current.Marine, ctx.Now) is { <Field>: { } value }`. All use `FeatureCategory.Marine`. All disabled by default.

| Name | Param | Field | Unit | DeviceClass |
|------|-------|-------|------|-------------|
| WaterTemperatureObservation | MarineParams.WaterTemperature | WaterTemperature | °C | temperature |
| WavePeriodObservation | MarineParams.WavePeriod | WavePeriod | s | duration |
| WaveDirectionObservation | MarineParams.WaveDirection | WaveDirection | ° | (none) |
| OceanCurrentVelocityObservation | MarineParams.OceanCurrentVelocity | OceanCurrentVelocity | m/s | speed |

- [x] 2.2 Create tests for the 4 Marine observations in `src/Njord.Features.Tests/Observations/<Name>/`.

## 3. Forecast observations (8 features)

- [x] 3.1 Create 8 Forecast observation features following the UvIndexObservation pattern. Input type: `ForecastInput`. Project: `forecast.Forecast`. Evaluate: `ForecastQueries.ValueAt(ctx.Current.Forecast, ctx.Now) is { <Field>: { } value }`. All use `FeatureCategory.Forecast`. All disabled by default.

| Name | Param | Field | Unit | DeviceClass |
|------|-------|-------|------|-------------|
| DewPointObservation | ForecastParams.DewPoint | DewPoint | °C | temperature |
| WindGustsObservation | ForecastParams.WindGusts | WindGusts | m/s | wind_speed |
| SoilTemperatureObservation | ForecastParams.SoilTemperature | SoilTemperature | °C | temperature |
| SoilMoistureObservation | ForecastParams.SoilMoisture0To1Cm | SoilMoisture0To1Cm | m³/m³ | moisture |
| SnowDepthObservation | ForecastParams.SnowDepth | SnowDepth | m | distance |
| EvapotranspirationObservation | ForecastParams.Evapotranspiration | Evapotranspiration | mm | precipitation |
| ShortwaveRadiationObservation | ForecastParams.ShortwaveRadiation | ShortwaveRadiation | W/m² | irradiance |
| VisibilityObservation | ForecastParams.Visibility | Visibility | m | distance |

- [x] 3.2 Create tests for the 8 Forecast observations in `src/Njord.Features.Tests/Observations/<Name>/`.

## 4. Flood observations (1 feature)

- [x] 4.1 Create DischargeMeanObservation following FloodDischargeObservation pattern. Input type: `FloodInput`. Param: `FloodParams.DischargeMean`. Field: `DischargeMean`. Unit: `m³/s`. DeviceClass: `volume_flow_rate`. Category: `FeatureCategory.Flood`. Disabled by default.

- [x] 4.2 Create test for DischargeMeanObservation.

## 5. Registration and docs

- [x] 5.1 Register all 21 new features in `src/Njord.Features/ServiceCollectionExtensions.cs` under the `// Observations` section with `services.AddFeature<T, TConfig>()`. Add the required `using` directives.

- [x] 5.2 Add 21 entries to `docs/data/features.json` for the new observations. Follow the existing Pm25Observation entry pattern.

## 6. Validation

Build and run all test projects:

```powershell
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

Then run all remaining test projects to catch cross-project breakage.
