## 1. WritableNjordOptions — Interface + Implementation (TDD)

- [x] 1.1 Create `IWritableOptions<T>` interface in `src/Njord.Core/Configuration/IWritableOptions.cs`: generic interface with `T Value { get; }` property and `T Update(Action<T> applyChanges)` method that returns the post-mutation snapshot.
- [x] 1.2 Write `WritableNjordOptionsSpec` in `src/Njord.Core.Tests/Configuration/WritableNjordOptionsSpec.cs`: sealed class, BDD method names. Test cases: Update mutates and persists to section-wrapped JSON (`{"Njord": {...}}`); override file created on first mutation (not at startup); atomic write (temp file + rename); concurrent mutations serialized; reload triggers IOptionsMonitor change; reading back after restart reflects mutation. Use a temp directory for the override file path.
- [x] 1.3 Implement `WritableNjordOptions` in `src/Njord.Core/Configuration/WritableNjordOptions.cs`. Constructor takes `IOptionsMonitor<NjordOptions>`, `IConfigurationRoot`, and `string overridePath`. Uses `SemaphoreSlim(1)` for thread safety. `Update` clones `CurrentValue`, applies mutation, wraps in `{"Njord": clone}`, serializes with `System.Text.Json`, writes atomically (temp + `File.Move`), calls `IConfigurationRoot.Reload()`, returns `_optionsMonitor.CurrentValue`. Deep clone via the existing `AdminGrpcService.CloneOptions` pattern (move static method here).
- [x] 1.4 Verify: `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj -- -class "Njord.Core.Tests.Configuration.WritableNjordOptionsSpec"` — all tests green.

## 2. AdminGrpcService Refactor (TDD)

- [x] 2.1 Update `src/Njord.Grpc.Tests/AdminGrpcServiceSpec.cs`: replace `ConfigPersistence` with `IWritableOptions<NjordOptions>` in `CreateService()`. Create a test-friendly `WritableNjordOptions` instance backed by a temp directory with a real `ConfigurationBuilder` chain (appsettings baseline + override file) so mutations are observable via `GetConfig`.
- [x] 2.2 Add test case: `SetEnrichment_disable_alerts_is_reflected_in_GetConfig` — call `SetEnrichment(alerts.enabled=false)`, then `GetConfig`, verify `alerts.enabled=false`. This is the bug regression test.
- [x] 2.3 Add test case: `SetEnrichment_overrides_baseline_config` — set up options with alerts enabled as baseline, call `SetEnrichment(alerts.enabled=false)`, verify `GetConfig` returns disabled. Simulates the env-var-override scenario.
- [x] 2.4 Refactor `src/Njord.Grpc/AdminGrpcService.cs`: replace constructor parameter `ConfigPersistence persistence` with `IWritableOptions<NjordOptions> writableOptions`. Replace all mutation methods (`SetLocations`, `SetSettings`, `SetEnrichment`, `SetBudget`) to use `_writableOptions.Update(opt => { ... })` instead of `CloneOptions` + `_persistence.SaveAsync`. Keep budget validation: call `BudgetCalculator.Validate` on the returned snapshot, if rejected call `Update` again to restore. Remove `CloneOptions` method (move deep-clone logic into `WritableNjordOptions`). Remove `_mutationLock` (lock is now in `WritableNjordOptions`).
- [x] 2.5 Verify: `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj -- -class "Njord.Grpc.Tests.AdminGrpcServiceSpec"` — all tests green including new regression tests.

## 3. Kestrel Config Cleanup

- [x] 3.1 Add `Kestrel:Endpoints` section to `src/Njord/appsettings.json`: `"Kestrel": { "Endpoints": { "Http": { "Url": "http://+:8080", "Protocols": "Http1" }, "Grpc": { "Url": "http://+:8081", "Protocols": "Http2" } } }`.
- [x] 3.2 Replace the `ConfigureKestrel` block in `src/Njord/Program.cs` with: `builder.WebHost.ConfigureKestrel(options => { options.Limits.MinResponseDataRate = null; });`. Remove the `var njordConfig`, `var grpcPort`, `var httpPort` variables and the entire `if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))` block.

## 4. DI Registration + Program.cs Wiring

- [x] 4.1 Update `src/Njord/Configuration/CoreSetupContainer.cs`: remove `services.AddSingleton<ConfigPersistence>()`. Add registration for `IWritableOptions<NjordOptions>` as singleton backed by `WritableNjordOptions`, injecting `IOptionsMonitor<NjordOptions>`, `IConfigurationRoot` (from `IConfiguration` cast), and the override file path (`Path.Combine("data", "appsettings.Override.json")`).
- [x] 4.2 Update `src/Njord/Program.cs`: change `AddJsonFile(Path.Combine("data", "njord-config.json"), ...)` to `AddJsonFile(Path.Combine("data", "appsettings.Override.json"), optional: true, reloadOnChange: true)`.

## 5. Dead Code Removal

- [x] 5.1 Delete `src/Njord.Core/Configuration/ConfigPersistence.cs`.
- [x] 5.2 Delete `src/Njord.Core/Configuration/GrpcOptions.cs`.
- [x] 5.3 Remove any remaining references to `ConfigPersistence` or `GrpcOptions` across the codebase (grep and fix compilation errors).

## 6. Validation

- [x] 6.1 Build: `dotnet build Njord.slnx` from `src/` — no errors.
- [x] 6.2 Run `dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj` — all tests pass.
- [x] 6.3 Run `dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj` — all tests pass.
- [x] 6.4 Run `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — all tests pass (verify layer rules still hold with new file locations).
- [x] 6.5 Run `dotnet slopwatch analyze -d . --fail-on warning` from repo root.
- [x] 6.6 Run `dotnet format --verify-no-changes Njord.slnx` from `src/` — no formatting violations.
