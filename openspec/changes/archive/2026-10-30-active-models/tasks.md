## 1. ActiveModels record and Host registration

- [x] 1.1 Create `ActiveModels` record in `src/Njord.Core/Configuration/ActiveModels.cs` with `PrimaryForecastModel` (string, default `"best_match"`), `ForecastModels` (`IReadOnlyList<string>`, default `[]`), `EnsembleModels` (`IReadOnlyList<string>`, default `[]`), and an `AllModels` computed property returning the combined list (primary first, secondary forecast, ensemble).
- [x] 1.2 Register `IOptions<ActiveModels>` in the Host setup container (e.g. `src/Njord/Configuration/DomainSetupContainer.cs`) via `PostConfigure` that reads `ForecastOptions` and `EnsembleOptions`. Verify the `PostConfigure` runs after `EnsembleOptions.ApplyDefaults()`.
- [x] 1.3 Add `ActiveModelsTests` in `src/Njord.Core.Tests/Configuration/ActiveModelsTests.cs` testing: default values (`BestMatch`, empty lists), `AllModels` ordering (primary first, then secondary forecast, then ensemble), and edge cases (empty forecast models, single ensemble model).

### Validation

```
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 2. Catalog uses ActiveModels

- [x] 2.1 Change `CatalogMessageMappingExtensions.ToCatalog` in `src/Njord.Grpc/Mapping/CatalogMessageMappingExtensions.cs` to take `ActiveModels` instead of `EnsembleOptions`. Build `CatalogLocation.Models` from `ActiveModels.AllModels`. Build `CatalogModel` entries for every model in the list.
- [x] 2.2 Update `WeatherGrpcService` in `src/Njord.Grpc/WeatherGrpcService.cs` to inject `IOptions<ActiveModels>` instead of `IOptions<EnsembleOptions>`. Remove `using Njord.OpenMeteo.Configuration`.
- [x] 2.3 Update `src/Njord.Grpc.Tests/WeatherGrpcServiceTests.cs` — Catalog tests should verify that both forecast and ensemble models appear in the response. Add a test case with explicit `ForecastOptions.Models` producing secondary forecast models in the catalog.

### Validation

```
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 3. Remove temporary Grpc → OpenMeteo reference

- [x] 3.1 Remove the `ProjectReference` to `Njord.OpenMeteo` from `src/Njord.Grpc/Njord.Grpc.csproj`.
- [x] 3.2 Remove the architecture test exemption for `EnsembleOptions` in Grpc from `src/Njord.Architecture.Tests/ArchitectureTests.cs`.

### Validation

```
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 4. Full validation

- [x] 4.1 Run all test projects, `dotnet format`, and verify clean build. Update `AGENTS.md` if any Catalog or model-related documentation changed.

### Validation

```
dotnet build Njord.slnx
dotnet format Njord.slnx --verify-no-changes
for p in Njord.*Tests; do [ "$p" = Njord.Tests.Shared ] && continue; dotnet run --project "$p/$p.csproj" --no-build; done
```
