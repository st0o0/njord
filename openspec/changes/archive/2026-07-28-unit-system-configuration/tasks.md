## 1. Domain: UnitSystem enum, UnitResolver, UnitConverter

- [x] 1.1 Add `UnitSystem` enum (`Metric`, `Imperial`) to `src/Njord/Domain/Weather/UnitSystem.cs`
- [x] 1.2 Add `UnitResolver` static class to `src/Njord/Domain/Weather/UnitResolver.cs` — `GetUnit(ParameterDef, UnitSystem)` returns the active unit string. Metric returns `ParameterDef.Unit`; Imperial maps by metric unit string (`°C→°F`, `m/s→mph`, `mm→in`, `hPa→inHg`, `cm→in`, `m→ft` for distance params, `kPa→psi` etc.). Dimensionless units (`%`, `°`, `""`, `wmo code`, `J/kg`, `W/m²`, `MJ/m²`, `s`, `h`, `m³/m³`) pass through unchanged.
- [x] 1.3 Add `UnitConverter` static class to `src/Njord/Domain/Weather/UnitConverter.cs` — `Convert(ParameterDef, double, UnitSystem)` applies server-side conversion only for parameters without Open-Meteo API unit support: `hPa→inHg` (×0.02953), `cm→in` (×0.3937) for snowfall, `m→in` (×39.37) for snow_depth, `m→ft` (×3.28084) for visibility/freezing_level_height. Returns value unchanged for Metric and for API-covered parameters.
- [x] 1.4 Add `MetricNormalizer` static class to `src/Njord/Domain/Weather/MetricNormalizer.cs` — `Normalize(ParameterDef, double, UnitSystem)` inverts unit conversion for enrichment input: `°F→°C`, `mph→m/s`, `in→mm`, `inHg→hPa`, `in→cm` for snowfall, `in→m` for snow_depth, `ft→m`. No-op for Metric.
- [x] 1.5 Tests: `src/Njord.Tests/Domain/Weather/UnitResolverSpec.cs` — verify all unit mappings for Metric and Imperial, dimensionless passthrough.
- [x] 1.6 Tests: `src/Njord.Tests/Domain/Weather/UnitConverterSpec.cs` — verify server-side conversions and no-op for API-covered params.
- [x] 1.7 Tests: `src/Njord.Tests/Domain/Weather/MetricNormalizerSpec.cs` — verify round-trip: Convert then Normalize returns approximately the original value.

## 2. Configuration: UnitSystem option + validation

- [x] 2.1 Add `UnitSystem UnitSystem` property to `src/Njord/Configuration/NjordOptions.cs` (default `UnitSystem.Metric`)
- [x] 2.2 Add validation in `src/Njord/Configuration/NjordOptionsValidator.cs` — reject invalid `UnitSystem` values with a clear message
- [x] 2.3 Add `UnitSystem` to `appsettings.Development.json` as `"Metric"` (explicit default for dev)
- [x] 2.4 Tests: `src/Njord.Tests/Configuration/NjordOptionsValidatorSpec.cs` — add test cases for valid Metric/Imperial and invalid UnitSystem values

## 3. Ingest: OpenMeteoClient unit-aware requests

- [x] 3.1 Modify `src/Njord/Ingest/OpenMeteoClient.cs` — inject `UnitSystem` from options; build URL with `temperature_unit`, `wind_speed_unit`, `precipitation_unit` based on unit system instead of hardcoded `wind_speed_unit=ms`
- [x] 3.2 Modify `src/Njord/Ingest/OpenMeteoClient.cs` — after deserialization, apply `UnitConverter.Convert` to each value of each parameter for the active unit system
- [x] 3.3 Modify `src/Njord/Ingest/OpenMeteoClient.cs` — update unit verification to use `UnitResolver` for expected units instead of hardcoded strings
- [x] 3.4 Tests: `src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs` — add scenarios for Imperial URL params, server-side conversion of pressure/snowfall values, and adapted unit verification

## 4. Proto: ParameterMeta + GetCatalogResponse

- [x] 4.1 Add `ParameterMeta` message to `protos/njord/v2/common.proto` — `string name = 1; string unit = 2;`
- [x] 4.2 Add `repeated ParameterMeta parameters = 3` to `GetCatalogResponse` in `protos/njord/v2/weather.proto`
- [x] 4.3 Verify `dotnet build src/Njord.slnx` generates the updated stubs without errors

## 5. gRPC: GetCatalog populates ParameterMeta

- [x] 5.1 Modify `src/Njord/Grpc/WeatherGrpcService.cs` `GetCatalog` — populate `response.Parameters` from the resolved parameter set using `UnitResolver.GetUnit(param, unitSystem)` for each parameter
- [x] 5.2 Tests: `src/Njord.Tests/Grpc/WeatherGrpcServiceSpec.cs` — add test cases verifying ParameterMeta entries for Metric and Imperial, and that only active parameters are listed

## 6. MQTT: Discovery payloads use active units

- [x] 6.1 Modify `src/Njord/Mqtt/DiscoveryPayloadBuilder.cs` — accept `UnitSystem` and use `UnitResolver.GetUnit` for `unit_of_measurement` instead of reading `ParameterDef.Unit` directly
- [x] 6.2 Modify derived device builder — wind_chill, diurnal_amplitude, decay_rate units use `UnitResolver`
- [x] 6.3 Tests: `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs` — add test cases for Imperial unit in discovery components (temperature → °F, wind → mph, pressure → inHg)
- [x] 6.4 Update verified snapshots if snapshot tests exist for discovery payloads

## 7. Enrichment: Metric normalization at pipeline boundary

- [x] 7.1 Modify `src/Njord/Enrichment/EnrichmentActor.cs` (or the enrichment dispatch point) — apply `MetricNormalizer` to forecast values before dispatching to enrichment features when unit system is Imperial
- [x] 7.2 Tests: verify enrichment features receive metric values regardless of configured unit system — e.g. frost alert still triggers at 0°C equivalent when input is in °F

## 8. Validation

- [x] 8.1 Run full test suite: `dotnet run --project src/Njord.Tests/Njord.Tests.csproj`
- [x] 8.2 Run `dotnet build src/Njord.slnx` to verify proto compilation
- [x] 8.3 Run `dotnet slopwatch` from repo root
