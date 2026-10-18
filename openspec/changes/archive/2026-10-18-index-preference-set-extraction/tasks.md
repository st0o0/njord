## 1. Simplify IndexConfigUpdate

- [x] 1.1 Refactor `IndexConfigUpdate` in `src/Njord.Messages/Config/UpdateAnalysisConfig.cs` from 12 positional params to `(bool? Enabled, IndexPreferenceValues? Preferences)`.
- [x] 1.2 Fix all compilation errors from the changed record shape — callers access preference fields via `.Preferences?.FieldName` instead of `.FieldName`.

## 2. Update conversion bridges

- [x] 2.1 Update `src/Njord/Configuration/ConfigUpdateMappings.cs`: the index update application logic navigates through `.Preferences?.` for preference fields.
- [x] 2.2 Update `src/Njord.Grpc/Mapping/ConfigRequestMappings.cs`: construct `IndexPreferenceValues` from proto fields, wrap in `IndexConfigUpdate`.
- [x] 2.3 Update `src/Njord.Grpc/Mapping/ConfigResponseMappings.cs` if it reads `IndexConfigUpdate` fields.

## 3. Test updates

- [x] 3.1 Update `src/Njord.Tests/Configuration/ConfigUpdateMappingsSpec.cs` for the new `IndexConfigUpdate` shape.
- [x] 3.2 Update `src/Njord.Tests/Configuration/ConfigManagerSpec.cs` for any `IndexConfigUpdate` constructions.
- [x] 3.3 Update `src/Njord.Grpc.Tests/Mapping/ConfigRequestMappingsSpec.cs` for the new construction path.

## 4. Validation

- [x] 4.1 Build: `dotnet build Njord.slnx` from `src/` — 0 errors, 0 warnings
- [x] 4.2 Run affected test suites:
  ```
  dotnet run --project Njord.Tests/Njord.Tests.csproj
  dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
  dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
  ```
- [x] 4.3 Full suite loop — all projects pass with no regressions.
