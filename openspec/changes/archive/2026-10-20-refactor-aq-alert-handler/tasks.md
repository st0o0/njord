All commands run from `src/`. Tests are xUnit v3 on Microsoft.Testing.Platform — `dotnet run --project`, not `dotnet test`. TDD: red first. `[Fact(Timeout = 5000)]`, `Spec` suffix, sealed classes.

## 1. Refactor AirQualityAlertHandler

- [x] 1.1 Rewrite `src/Njord.Alerts/AirQualityAlertHandler.cs` to implement `IMultiFeatureHandler` following the `WeatherAlertHandler` pattern: constructor takes `IEnumerable<IAlertRule<DomainForecast<AirQualityPoint>>>`, `IOptions<AlertOptions>`, `IOptions<FeatureOptions>`; lazy-resolves enabled rules; `Name` = `"AirQualityAlerts"`; `Features` lists all rules
- [x] 1.2 Simplify `src/Njord.Alerts/ServiceCollectionExtensions.cs`: replace 3 factory lambdas with `services.AddSingleton<IDomainHandler, AirQualityAlertHandler>()`

## 2. Update Tests

- [x] 2.1 Add `AirQualityHandler(...)` helper to `src/Njord.Tests.Shared/FeatureServices.cs` mirroring `WeatherHandler(...)`
- [x] 2.2 Rewrite `src/Njord.Alerts.Tests/AirQualityAlertHandlerSpec.cs`: drop `name` parameter from `Create()`, add per-feature toggling tests (enable/disable Pollen while Pm25 fires), test `Features` lists all 3 rules, test composite `Feature.Requirements` unions enabled rules

## 3. Validation

- [x] 3.1 `dotnet build Njord.slnx`
- [x] 3.2 `dotnet format Njord.slnx --verify-no-changes`
- [x] 3.3 `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj`
- [x] 3.4 `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`
- [x] 3.5 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
- [x] 3.6 `dotnet run --project Njord.IntegrationTests/Njord.IntegrationTests.csproj`
