## 1. Query-key constants and azimuth helper

- [x] 1.1 Add constants to `src/Njord.OpenMeteo/Http/ClientRequest.cs`: `ForecastDays`, `ForecastHours`, `PastHours`, `Tilt`, `Azimuth`, `WindSpeedUnit`, `MetersPerSecond`, `Ensemble`, `True`
- [x] 1.2 Add `static int ToApiAzimuth(int configAzimuth)` method on `ClientRequest`
- [x] 1.3 Update `src/Njord.OpenMeteo/Weather/WeatherClient.cs` to use the new constants and `ToApiAzimuth`
- [x] 1.4 Update `src/Njord.OpenMeteo/Satellite/SatelliteRadiationClient.cs` to use the new constants and `ToApiAzimuth`
- [x] 1.5 Update `src/Njord.OpenMeteo/AirQuality/AirQualityClient.cs`, `Flood/FloodClient.cs`, `Marine/MarineClient.cs` to use `ClientRequest.ForecastDays`
- [x] 1.6 Replace `WeatherClient.GlobalTiltedIrradiance` const with `WeatherApiVariable.GlobalTiltedIrradiance.ToApiName()`

## 2. Remove WeatherResponseConverter and simplify WeatherResponse

- [x] 2.1 Change `src/Njord.OpenMeteo/Weather/WeatherResponse.cs`: remove `[JsonConverter]` attribute, change `Hourly` to `Dictionary<string, JsonElement>?` with `[JsonPropertyName("hourly")]`, remove `Models` property
- [x] 2.2 Delete `src/Njord.OpenMeteo/Weather/WeatherResponseConverter.cs`

## 3. Move multi-model split into the mapping layer

- [x] 3.1 Add a private key-splitting helper in `src/Njord.OpenMeteo/Weather/WeatherResponseMappingExtensions.cs` that uses `ApiVariableNames.For<WeatherApiVariable>().Values` sorted by length descending, skips `time`, filters `member` prefixed suffixes, and groups by (variable, model)
- [x] 3.2 Update `ToModelForecasts` to: extract `time` array from the dictionary, run the key splitter, reconstruct a `JsonObject` per model (time + variable arrays), deserialize each as `WeatherHourlyData` via `OpenMeteoJsonContext.Default.WeatherHourlyData`, then continue with the existing `ToForecast` path

## 4. Update tests

- [x] 4.1 Update `src/Njord.OpenMeteo.Tests/WeatherModelResponseSpec.cs`: replace `WeatherResponseConverter.ApiNames` references with `ApiVariableNames.For<WeatherApiVariable>().Values`, verify single-model and multi-model deserialization+mapping still works
- [x] 4.2 Verify existing mapping tests (`WeatherResponseMappingExtensions` tests) pass with the dictionary-based response
- [x] 4.3 Add a test for member-series filtering (key with `member` suffix is dropped)
- [x] 4.4 Add a test for unknown key filtering (key not matching any known variable is dropped)

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx` from `src/`
- [x] 5.2 `dotnet format Njord.slnx --verify-no-changes` from `src/`
- [x] 5.3 Run all test projects from `src/`
