## 1. Proto: optional fields and DomainSchema message

- [x] 1.1 Add `DomainSchema` message to `protos/njord/v2/common.proto`: `Domain domain = 1; repeated string fields = 2`. Add `repeated DomainSchema resolved_domains = 5` to `LocationInfo`.
- [x] 1.2 Mark all numeric fields in `ForecastSample`, `AirQualitySample`, `MarineSample`, `FloodSample`, `SeasonalSample`, `EnsembleSample` as `optional` in `protos/njord/v2/weather.proto` (except `valid_at` timestamps and `weather_code`). Verify `dotnet build src/Njord.slnx` compiles.
- [x] 1.3 Copy updated `protos/njord/v2/common.proto` and `protos/njord/v2/weather.proto` to `D:\GIT\ha-njord\protos\njord\v2\`. Regenerate Python stubs: `cd D:\GIT\ha-njord && python -m grpc_tools.protoc ...`.

## 2. njord: ParamFieldNames mapping

- [x] 2.1 Create `src/Njord.Grpc/Mapping/ParamFieldNames.cs` with one `FrozenDictionary<TParams, string>` per domain (`ForecastParams`, `EnsembleParams`, `AirQualityParams`, `FloodParams`, `MarineParams`, `SeasonalParams`) mapping each flag to its proto field name. Use explicit entries (no convention-based derivation).
- [x] 2.2 Add tests in `src/Njord.Grpc.Tests/Mapping/ParamFieldNamesTests.cs`: verify every `*Params` enum member (except `None`/0) has a mapping, and spot-check representative mappings (`AirQualityParams.Pm25 -> "pm25"`, `MarineParams.WavePeriod -> "wave_period"`, `ForecastParams.WeatherCode -> "weather_code"`).

## 3. njord: Catalog schema building

- [x] 3.1 Add `IReadOnlyList<CatalogDomainSchema> ResolvedDomains` to `CatalogLocation` in `src/Njord.Messages/Config/CatalogResult.cs`. Add `sealed record CatalogDomainSchema(DomainKind Domain, IReadOnlyList<string> Fields)`.
- [x] 3.2 Build the `ResolvedDomains` where `CatalogResult` is assembled (the actor or service that handles `QueryCatalog`). Use `IOptions<DomainRequirements>` to get the resolved requirements, iterate each domain's `All*` flags, and map to proto field names via `ParamFieldNames`.
- [x] 3.3 Extend `CatalogProtoMappingExtensions.ToProto` in `src/Njord.Grpc/Mapping/CatalogProtoMappingExtensions.cs` to map `CatalogDomainSchema` to the proto `DomainSchema` message.
- [x] 3.4 Add tests in `src/Njord.Grpc.Tests/Mapping/CatalogProtoMappingExtensionsTests.cs`: verify `CatalogLocation` with resolved domains maps correctly to `LocationInfo` proto with `DomainSchema` entries.

## 4. njord: Fix DomainData mapping for optional fields

- [x] 4.1 Update `DomainDataProtoMappingExtensions` in `src/Njord.Grpc/Mapping/DomainDataProtoMappingExtensions.cs`: use conditional setters (`if (p.Pm25 is { } v) sample.Pm25 = v;`) for all newly-optional fields in every domain sample mapper. The fields already using this pattern stay unchanged.
- [x] 4.2 Verify existing tests in `src/Njord.Grpc.Tests/` still pass (the `optional` keyword changes the generated C# properties to nullable). Fix any compilation or assertion issues.

## 5. ha-njord: Nullable model fields and _opt usage

- [x] 5.1 In `custom_components/njord/models.py`: change `pm25`, `pm10`, `ozone` in `AirQualitySampleData` from `float` to `float | None = None`. Change `wave_height`, `wave_period`, `wave_direction` in `MarineSampleData` from `float` to `float | None = None`. Change `discharge` in `FloodSampleData` from `float` to `float | None = None`. Change all non-optional numeric fields in `ForecastSampleData` (temperature, wind_speed, precipitation, etc.) to `float | None = None`.
- [x] 5.2 In `custom_components/njord/grpc_client.py`: change `_to_air_quality_sample`, `_to_marine_sample`, `_to_flood_sample`, `_to_forecast_sample` to use `_opt(pb, "field")` for all formerly non-optional fields.
- [x] 5.3 Update `tests/test_grpc_client.py` and `tests/test_models.py` for nullable fields.

## 6. ha-njord: Catalog-driven sensor registration

- [x] 6.1 Extend `CatalogData` and `NjordLocation` in `models.py` to carry per-location resolved domain schemas (`dict[Domain, set[str]]`). Parse from proto in `_to_location` / `_to_catalog_data` in `grpc_client.py`.
- [x] 6.2 Store the domain schemas in `NjordCoordinatorData` (e.g. `domain_schemas: dict[tuple[str, Domain], set[str]]`). Populate during `_async_update_data` from the catalog.
- [x] 6.3 Rewrite `sensor.py`: replace the `DOMAIN_SENSORS` tuple with a `DOMAIN_SENSOR_REGISTRY: dict[tuple[Domain, str], DomainSensorDescription]` keyed by `(domain, field_name)`. In `_domain_sensors`, look up the location's schema fields and create sensors only for fields in both the registry and the schema.
- [x] 6.4 Update `tests/test_sensor.py` and `tests/test_dynamic_entities.py` for catalog-driven registration: test that sensors are not created for fields absent from the schema.

## 7. Validation

- [x] 7.1 Build and run all njord test suites:
  ```
  cd src
  dotnet build Njord.slnx
  dotnet format Njord.slnx --verify-no-changes
  dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj
  dotnet run --project Njord.Budget.Tests/Njord.Budget.Tests.csproj
  dotnet run --project Njord.Polling.Tests/Njord.Polling.Tests.csproj
  dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
  dotnet run --project Njord.Features.Tests/Njord.Features.Tests.csproj
  dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  ```
- [x] 7.2 Run ha-njord tests: `cd D:\GIT\ha-njord && pytest tests/`
- [x] 7.3 Update E2E test plan (`e2e/E2E-TEST-PLAN.md`) to verify catalog includes domain schemas and absent sensors are not registered.
