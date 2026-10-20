All commands run from `src/`. Tests use `dotnet run --project`, not `dotnet test`. Run `dotnet format Njord.slnx` after editing `.cs` files.

## 1. Create New Projects

- [x] 1.1 Create `src/Njord.Alerts/Njord.Alerts.csproj` with `<ProjectReference>` to `Njord.Core`; add `Rules/` subfolder
- [x] 1.2 Create `src/Njord.Scores/Njord.Scores.csproj` with `<ProjectReference>` to `Njord.Core`
- [x] 1.3 Create `src/Njord.Insights/Njord.Insights.csproj` with `<ProjectReference>` to `Njord.Core`
- [x] 1.4 Create `src/Njord.Advisories/Njord.Advisories.csproj` with `<ProjectReference>` to `Njord.Core`
- [x] 1.5 Create `src/Njord.Alerts.Tests/Njord.Alerts.Tests.csproj` referencing `Njord.Alerts` and `Njord.Tests.Shared`
- [x] 1.6 Create `src/Njord.Scores.Tests/Njord.Scores.Tests.csproj` referencing `Njord.Scores` and `Njord.Tests.Shared`
- [x] 1.7 Create `src/Njord.Insights.Tests/Njord.Insights.Tests.csproj` referencing `Njord.Insights` and `Njord.Tests.Shared`
- [x] 1.8 Create `src/Njord.Advisories.Tests/Njord.Advisories.Tests.csproj` referencing `Njord.Advisories` and `Njord.Tests.Shared`
- [x] 1.9 Update `src/Njord.slnx` to include the eight new projects
- [x] 1.10 Validate: `dotnet build Njord.slnx` (empty projects build)

## 2. Move Alerts

- [x] 2.1 Move `WeatherAlertHandler.cs`, `AlertWindow.cs` from `Njord.Weather/` to `Njord.Alerts/`; update namespace to `Njord.Alerts`
- [x] 2.2 Move all 14 weather rules from `Njord.Weather/Rules/` to `Njord.Alerts/Rules/`; update namespace to `Njord.Alerts.Rules`
- [x] 2.3 Move `AirQualityAlertHandler.cs`, `AirQualityAlertWindow.cs`, `PollenAlertRule.cs`, `Pm25AlertRule.cs` from `Njord.AirQuality/` to `Njord.Alerts/`; update namespace to `Njord.Alerts`
- [x] 2.4 Move `FloodRiskHandler.cs`, `FloodRiskAlertRule.cs`, `FloodRainHandler.cs`, `FloodRainAlertRule.cs`, `FloodRainInput.cs` from `Njord.Flood/` to `Njord.Alerts/`; update namespace to `Njord.Alerts`
- [x] 2.5 Move `SwimmingConditionsHandler.cs`, `SwimmingConditionsRule.cs` from `Njord.Marine/` to `Njord.Alerts/`; update namespace to `Njord.Alerts`
- [x] 2.6 Create `src/Njord.Alerts/ServiceCollectionExtensions.cs` with `AddAlertHandlers()` combining registrations from all four old extension methods (alert-related only)
- [x] 2.7 Move corresponding tests from `Njord.Weather.Tests/`, `Njord.AirQuality.Tests/`, `Njord.Flood.Tests/`, `Njord.Marine.Tests/` to `Njord.Alerts.Tests/`; update namespaces
- [x] 2.8 Validate: `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj`

## 3. Move Scores

- [x] 3.1 Move `BBQScoreHandler.cs`, `LaundryScoreHandler.cs`, `OutdoorScoreHandler.cs`, `Scoring.cs` from `Njord.Weather/Lifestyle/` to `Njord.Scores/`; update namespace to `Njord.Scores`
- [x] 3.2 Move `SurfConditionsHandler.cs`, `SurfConditionsRule.cs` from `Njord.Marine/` to `Njord.Scores/`; update namespace to `Njord.Scores`
- [x] 3.3 Create `src/Njord.Scores/ServiceCollectionExtensions.cs` with `AddScoreHandlers()`
- [x] 3.4 Move corresponding tests to `Njord.Scores.Tests/`; update namespaces
- [x] 3.5 Validate: `dotnet run --project Njord.Scores.Tests/Njord.Scores.Tests.csproj`

## 4. Move Insights

- [x] 4.1 Move `SeasonalOutlookHandler.cs`, `SolarYieldHandler.cs`, `SolarIrradianceBlend.cs`, `SatelliteRadiationHandler.cs` from `Njord.Weather/Lifestyle/` to `Njord.Insights/`; update namespace to `Njord.Insights`
- [x] 4.2 Move `ConfidenceTrendHandler.cs`, `ApproachingFrontHandler.cs` from `Njord.Weather/Lifestyle/` to `Njord.Insights/`; update namespace to `Njord.Insights`
- [x] 4.3 Move `PollenTrendHandler.cs` from `Njord.AirQuality/` to `Njord.Insights/`; update namespace to `Njord.Insights`
- [x] 4.4 Create `src/Njord.Insights/ServiceCollectionExtensions.cs` with `AddInsightHandlers()`
- [x] 4.5 Move corresponding tests to `Njord.Insights.Tests/`; update namespaces
- [x] 4.6 Validate: `dotnet run --project Njord.Insights.Tests/Njord.Insights.Tests.csproj`

## 5. Move Advisories

- [x] 5.1 Move `CommuteSafetyHandler.cs`, `VentilationWindowHandler.cs`, `NightVentilationHandler.cs`, `ExerciseAdvisoryHandler.cs`, `GardeningHandler.cs` from `Njord.Weather/Lifestyle/` to `Njord.Advisories/`; update namespace to `Njord.Advisories`
- [x] 5.2 Move `FloodTimelineHandler.cs` from `Njord.Flood/` to `Njord.Advisories/`; update namespace to `Njord.Advisories`
- [x] 5.3 Create `src/Njord.Advisories/ServiceCollectionExtensions.cs` with `AddAdvisoryHandlers()`
- [x] 5.4 Move corresponding tests to `Njord.Advisories.Tests/`; update namespaces
- [x] 5.5 Validate: `dotnet run --project Njord.Advisories.Tests/Njord.Advisories.Tests.csproj`

## 6. Update Host and Architecture

- [x] 6.1 Update `src/Njord/` Host registration: replace `AddWeatherDomain()`, `AddAirQualityDomain()`, `AddFloodDomain()`, `AddMarineDomain()` with `AddAlertHandlers()`, `AddScoreHandlers()`, `AddInsightHandlers()`, `AddAdvisoryHandlers()`; update project references in Host `.csproj`
- [x] 6.2 Update `src/Njord.Architecture.Tests/ArchitectureSpec.cs`: replace old project names with new ones in zone enforcement rules; ensure handler-to-handler cross-references and handler-to-Host references are rejected
- [x] 6.3 Update `src/Njord.IntegrationTests/` if it references old handler namespaces or projects

## 7. Delete Old Projects

- [x] 7.1 Delete `src/Njord.Weather/`, `src/Njord.AirQuality/`, `src/Njord.Flood/`, `src/Njord.Marine/` and their test projects
- [x] 7.2 Remove old projects from `src/Njord.slnx`

## 8. Final Validation and Docs

- [x] 8.1 `dotnet build Njord.slnx` and `dotnet format Njord.slnx --verify-no-changes`
- [x] 8.2 Run all test projects sequentially: Njord.Alerts.Tests, Njord.Scores.Tests, Njord.Insights.Tests, Njord.Advisories.Tests, Njord.Core.Tests, Njord.Forecast.Tests, Njord.Messages.Tests, Njord.Persistence.Tests, Njord.OpenMeteo.Tests, Njord.Budget.Tests, Njord.Polling.Tests, Njord.Pipeline.Tests, Njord.Grpc.Tests, Njord.Architecture.Tests, Njord.IntegrationTests
- [x] 8.3 Verify total test count across the four new handler test projects equals 236 (123+57+34+22; +3 vs old 233 from split requirements specs)
- [x] 8.4 Update `AGENTS.md` solution structure section with the new project layout
