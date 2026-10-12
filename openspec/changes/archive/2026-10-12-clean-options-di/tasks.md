## 1. Bind EnrichmentOptions sub-types in DI

- [x] 1.1 Add individual `AddOptions<T>().Bind()` calls for `AlertOptions`, `IndexOptions`, `DerivedOptions`, `TrendOptions`, and `ConsensusOptions` in `src/Njord/Configuration/AnalysisSetupContainer.cs`, binding each to its respective `EnrichmentOptions` sub-section (e.g., `Njord:Enrichment:Alerts`).

## 2. Refactor analyzer constructors

- [x] 2.1 Refactor `src/Njord.Indices/IndexAnalyzer.cs`: replace `IndexOptions indexOptions, IEnumerable<string> locationNames, bool enabled` with `IOptions<IndexOptions>` and `IOptions<NjordOptions>` (for location names). Update `src/Njord.Indices/ServiceCollectionExtensions.cs` to plain `AddSingleton<IndexAnalyzer>()`. Update all tests in `src/Njord.Indices.Tests/`.
- [x] 2.2 Refactor `src/Njord.Trends/TrendAnalyzer.cs`: replace `bool enabled = true` with `IOptions<TrendOptions>`. Update `src/Njord.Trends/ServiceCollectionExtensions.cs` to plain `AddSingleton<TrendAnalyzer>()`. Update all tests in `src/Njord.Trends.Tests/`.
- [x] 2.3 Refactor `src/Njord.Derived/DerivedAnalyzer.cs`: replace `IReadOnlyList<int> horizons` with `IOptions<NjordOptions>`. Remove the manual factory in `src/Njord.Derived/ServiceCollectionExtensions.cs`, replace with plain `AddSingleton<DerivedAnalyzer>()`. Update all tests in `src/Njord.Derived.Tests/`.
- [x] 2.4 Verify `src/Njord.Alerts/AlertAnalyzer.cs` works correctly now that `AlertOptions` is bound (no code change expected, just verify tests pass). Update any test construction if needed in `src/Njord.Alerts.Tests/`.

## 3. Clean up MQTT options usage

- [x] 3.1 Refactor `src/Njord.Mqtt/Transport/MqttNetPublisher.cs`: change constructor from `MqttOptions options` to `IOptions<MqttOptions> options`. Remove the manual factory in `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs`, replace with `services.TryAddSingleton<MqttNetPublisher>()`. Update tests in `src/Njord.Mqtt.Tests/`.
- [x] 3.2 Replace raw `IConfiguration.GetValue("Enabled")` in `src/Njord.Mqtt/Configuration/MqttSetupContainer.cs` with `configuration.GetSection(MqttOptions.SectionName).Get<MqttOptions>()?.Enabled ?? false`.
- [x] 3.3 Replace raw `IConfiguration.GetValue("Enabled")` in `src/Njord/Configuration/CoreSetupContainer.cs` with the same pattern. Update tests in `src/Njord.Core.Tests/` if any cover this path.

## 4. Update pipeline and host tests

- [x] 4.1 Update any analyzer construction in `src/Njord.Pipeline.Tests/` that passes raw options or primitives to use `Options.Create(new XxxOptions { ... })`.
- [x] 4.2 Update any analyzer construction in shared test infrastructure `src/Njord.Tests.Shared/` if affected.

## 5. Validation

- [x] 5.1 Build: `dotnet build Njord.slnx` from `src/`.
- [x] 5.2 Run affected test suites:
  - `dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj`
  - `dotnet run --project Njord.Indices.Tests/Njord.Indices.Tests.csproj`
  - `dotnet run --project Njord.Trends.Tests/Njord.Trends.Tests.csproj`
  - `dotnet run --project Njord.Derived.Tests/Njord.Derived.Tests.csproj`
  - `dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj`
  - `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj`
  - `dotnet run --project Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj`
  - `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj`
- [x] 5.3 Run `dotnet format Njord.slnx --verify-no-changes` to confirm formatting.
