## 1. New shared records

- [x] 1.1 Create `AlertThresholdValues` positional record in `src/Njord.Messages/Alerts/AlertThresholdValues.cs` with the 16 threshold fields (6× `IReadOnlyList<double>`, 9× `double`, 1× `int`). Create `AlertThresholdOverrides` in the same file with all fields nullable.
- [x] 1.2 Add tests in `src/Njord.Messages.Tests/Contracts/AlertThresholdValuesSpec.cs`: construction, field count verification, nullable override semantics.

## 2. Simplify AlertSection and AlertConfigUpdate

- [x] 2.1 Refactor `AlertSection` in `src/Njord.Messages/Config/EffectiveConfig.cs` from 17 positional params to `(bool Enabled, AlertThresholdValues Thresholds)`.
- [x] 2.2 Refactor `AlertConfigUpdate` in `src/Njord.Messages/Config/UpdateAnalysisConfig.cs` from 17 nullable params to `(bool? Enabled, AlertThresholdOverrides? Thresholds)`.
- [x] 2.3 Fix all compilation errors from the changed record shapes — callers now access threshold fields via `.Thresholds.FieldName` instead of `.FieldName`.

## 3. Conversion bridges

- [x] 3.1 Add `ToThresholdValues()` extension method on `AlertOptions` in `src/Njord.Alerts/Configuration/AlertSettingsConversion.cs` (or a new `AlertThresholdConversion.cs`). Converts `double[]` → `IReadOnlyList<double>` and passes scalars through.
- [x] 3.2 Change `AlertSettingsConversion.ToSettings()` to accept `AlertThresholdValues` instead of `AlertOptions`. Converts `IReadOnlyList<double>` → `SeverityThresholds`.
- [x] 3.3 Update `src/Njord/Configuration/EffectiveConfigMappings.cs`: `ToSection(AlertOptions)` now calls `.ToThresholdValues()` and wraps in `AlertSection(options.Enabled, values)`.
- [x] 3.4 Update `src/Njord/Configuration/ConfigUpdateMappings.cs`: the alert update application logic navigates through `Thresholds.` for threshold fields.
- [x] 3.5 Update `src/Njord.Grpc/Mapping/AlertProtoMappings.cs` and `src/Njord.Grpc/Mapping/ConfigResponseMappings.cs` to navigate through the `Thresholds` wrapper.

## 4. Test updates

- [x] 4.1 Update `src/Njord.Alerts.Tests/Configuration/AlertSettingsConversionSpec.cs` for the new `ToThresholdValues()` + `ToSettings()` chain.
- [x] 4.2 Update `src/Njord.Tests/Configuration/EffectiveConfigMappingsSpec.cs` for the new `AlertSection` shape.
- [x] 4.3 Update `src/Njord.Tests/Configuration/ConfigUpdateMappingsSpec.cs` for the new `AlertConfigUpdate` shape.
- [x] 4.4 Update `src/Njord.Grpc.Tests/Mapping/AlertProtoMappingsSpec.cs` and `ConfigResponseMappingsSpec.cs` for the wrapper navigation.
- [x] 4.5 Re-approve golden master snapshots (`ProtoMappingGoldenMasterSpec`, `DefaultOptionsSpec`) — wire output must be unchanged, only C# structure differs.

## 5. Validation

- [x] 5.1 Build: `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings
- [x] 5.2 Run affected test suites:
  ```
  dotnet run --project Njord.Messages.Tests/Njord.Messages.Tests.csproj
  dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Tests/Njord.Tests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
  ```
- [x] 5.3 Full suite loop — all projects pass with no regressions.
