All commands run from `src/`. Tests: `dotnet run --project <Project>.Tests/<Project>.Tests.csproj` (xUnit v3, `Spec` suffix, sealed, `TestContext.Current.CancellationToken`). TDD: write the completeness/parity spec first. Run `dotnet format Njord.slnx` after editing `.cs` files. Existing specs must pass unchanged throughout.

## 1. Vocabulary and generic mapper

- [ ] 1.1 Create `FieldConversion` and `FieldDefinition<TParams,TDto,TPoint>` (with `Int`/`Bool` factories) in `src/Njord.OpenMeteo/Mapping/FieldDefinition.cs`
- [ ] 1.2 Create the generic loop `FieldMapper.ToDomainForecast` in `src/Njord.OpenMeteo/Mapping/FieldMapper.cs` (null guard, `lastValued`, conversions, trim, `ImmutableArray`); reuse `SeriesAccess.At`
- [ ] 1.3 Specs in `src/Njord.OpenMeteo.Tests/Mapping/FieldMapperSpec.cs` with a tiny fake domain: trim, nothing valued, short/missing series, int and bool conversions
- [ ] 1.4 Validate: `Njord.OpenMeteo.Tests`

## 2. Weather: table, names, presence

- [ ] 2.1 Create `WeatherFieldMap` (32 rows) in `src/Njord.OpenMeteo/Mapping/WeatherFieldMap.cs`
- [ ] 2.2 Completeness spec `WeatherFieldMapSpec` (design D5) in `src/Njord.OpenMeteo.Tests/Mapping/`
- [ ] 2.3 Replace the `ToApiNames` body in `src/Njord.OpenMeteo/Mapping/WeatherParamsMappings.cs` with the table; delete the `ToApiName` switch
- [ ] 2.4 Replace `Has()` in `src/Njord.Forecast/Weather/WeatherPoint.cs` with the presence list (design D2); add a flag-by-flag presence spec in `src/Njord.Forecast.Tests`
- [ ] 2.5 Validate: `Njord.OpenMeteo.Tests`, `Njord.Forecast.Tests`, `Njord.Architecture.Tests`

## 3. Weather: mapper

- [ ] 3.1 Parity spec comparing the old mapper output with the table-driven one (design D7)
- [ ] 3.2 Rewrite `src/Njord.OpenMeteo/Mapping/WeatherResponseMappings.cs` as a thin call into `FieldMapper` (same signature); remove the 33-variable deconstruction and the `AnyAt` list
- [ ] 3.3 Validate: `Njord.OpenMeteo.Tests`, `Njord.Pipeline.Tests`, `Njord.IntegrationTests`

## 4. AirQuality, Marine, Flood, Seasonal

- [ ] 4.1 AirQuality: `AirQualityFieldMap`, completeness spec, `AirQualityParamsMappings.cs`, `src/Njord.Forecast/AirQuality/AirQualityPoint.cs`, `AirQualityResponseMappings.cs`
- [ ] 4.2 Marine: `MarineFieldMap`, completeness spec, `MarineParamsMappings.cs`, `src/Njord.Forecast/Marine/MarinePoint.cs`, `MarineResponseMappings.cs`
- [ ] 4.3 Flood: `FloodFieldMap` for names and presence only (`FloodParamsMappings.cs`, `src/Njord.Forecast/Flood/FloodPoint.cs`); `FloodResponseMappings.cs` untouched
- [ ] 4.4 Seasonal: `SeasonalFieldMap` for names and presence (`SeasonalParamsMappings.cs`, `src/Njord.Forecast/Seasonal/SeasonalPoint.cs`); migrate `SeasonalResponseMappings.cs` only if parity holds with `maxPoints` and `CountsAsValue` (design D6), else leave it and record that in design.md
- [ ] 4.5 Validate: the five domain test projects plus `Njord.Forecast.Tests`

## 5. Request builder

- [ ] 5.1 Specs in `src/Njord.OpenMeteo.Tests/OpenMeteoRequestBuilderSpec.cs`: unchanged URL, reserved characters, comma lists
- [ ] 5.2 Encode keys and values with `Uri.EscapeDataString` in `src/Njord.OpenMeteo/OpenMeteoRequestBuilder.cs`, commas literal
- [ ] 5.3 Validate: `Njord.OpenMeteo.Tests`

## 6. Validation

- [ ] 6.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [ ] 6.2 Run every test project sequentially; counts must be the old total plus new specs only, no existing spec edited
- [ ] 6.3 Confirm no hand-written `Has()`/`ToApiName()` switch or `AnyAt(...)` list remains except Flood members and any Seasonal exception recorded in 4.4
- [ ] 6.4 Update test counts in `AGENTS.md`; run `openspec validate typed-field-mappings --no-interactive`
