## 1. NjordOptions ResolvedLocations

- [x] 1.1 Add `ResolvedLocations` property to `NjordOptions` (`src/Njord.Core/Configuration/NjordOptions.cs`), change `FindLocation` to return `Location?` from the resolved list. Register a `PostConfigure<NjordOptions>` in `AddNjordOptions` (`src/Njord.Core/Configuration/AddNjordOptions.cs`) that builds `ResolvedLocations` from `Locations`. Update `NjordOptionsValidatorTests` (`src/Njord.Core.Tests/Configuration/NjordOptionsValidatorTests.cs`) and add tests for `ResolvedLocations` and `FindLocation`.

- [x] 1.2 Change `PollManagerConfig.Create` (`src/Njord.Polling/PollManagerState.cs`) to accept `IReadOnlyList<Location>`. Update `PollManager` (`src/Njord.Polling/PollManager.cs`) to pass `ResolvedLocations`. Update `PollManagerStateTests` (`src/Njord.Polling.Tests/PollManagerStateTests.cs`) and any other test that calls `PollManagerConfig.Create`.

- [x] 1.3 Change `CatalogMessageMappingExtensions.ToCatalogResult` (`src/Njord.Grpc/Mapping/CatalogMessageMappingExtensions.cs`) to use `ResolvedLocations` instead of `Locations` with `GetValueOrDefault()`. Update `BudgetGuard` (`src/Njord.Polling/Configuration/BudgetGuard.cs`) and `PollingOptionsValidator` (`src/Njord.Polling/Configuration/PollingOptionsValidator.cs`) if they access `Locations.Count` -- use `ResolvedLocations.Count` instead. Fix affected tests.

**Validation:** `dotnet build src/Njord.slnx`, then `dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj`, `dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`.

## 2. DomainRequirements via IOptionsFactory

- [x] 2.1 Create `DomainRequirementsFactory : IOptionsFactory<DomainRequirements>` in `src/Njord.Features/DomainRequirementsFactory.cs`. Register it in `AddFeatures` (`src/Njord.Features/ServiceCollectionExtensions.cs`) via `services.AddSingleton<IOptionsFactory<DomainRequirements>, DomainRequirementsFactory>()` plus `services.AddOptions<DomainRequirements>()`. Remove the `AddSingleton<IOptions<DomainRequirements>>` from `DomainSetupContainer` (`src/Njord\Configuration/DomainSetupContainer.cs`). Add a factory unit test in `src/Njord.Features.Tests/`.

**Validation:** `dotnet build src/Njord.slnx`, then `dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj`, `dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj`.

## 3. ActiveModels via AddOptions

- [x] 3.1 Change `ActiveModels` (`src/Njord.Core/Configuration/ActiveModels.cs`) properties from `init` to `set`. Replace the `AddSingleton<IOptions<ActiveModels>>` in `DomainSetupContainer` with `services.AddOptions<ActiveModels>().Configure<IOptions<ForecastOptions>, IOptions<EnsembleOptions>>(...)`. Update `ActiveModelsTests` (`src/Njord.Core.Tests/Configuration/ActiveModelsTests.cs`).

**Validation:** `dotnet build src/Njord.slnx`, then `dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj`, `dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj`.

## 4. DomainCallConfig via IConfigureOptions

- [x] 4.1 Convert `DomainCallConfig` (`src/Njord.Core/Polling/DomainCallConfig.cs`) from positional record to non-positional with `{ get; set; }` and matching defaults. Create `ConfigureDomainCallConfig : IConfigureOptions<DomainCallConfig>` in `src/Njord/Configuration/ConfigureDomainCallConfig.cs` with 7 `IOptions<*>` deps. Register via `services.AddOptions<DomainCallConfig>()` + `services.AddSingleton<IConfigureOptions<DomainCallConfig>, ConfigureDomainCallConfig>()` in `DomainSetupContainer`. Remove the old singleton. Add a test in `src/Njord.Polling.Tests/` (or existing `BudgetGuardTests` if it covers the config shape).

**Validation:** `dotnet build src/Njord.slnx`, then `dotnet run --project src/Njord.Polling.Tests/Njord.Polling.Tests.csproj`.

## 5. Cleanup and full validation

- [x] 5.1 Remove `using Microsoft.Extensions.Options` and `Options.Create` from `DomainSetupContainer` if no longer needed. Run `dotnet format src/Njord.slnx`. Run ALL test projects to verify no regressions.
