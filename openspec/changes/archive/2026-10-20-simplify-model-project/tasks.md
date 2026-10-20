## 1. Remove dead types

- [x] 1.1 Delete `src/Njord.Model/ModelCoverageRegistry.cs` and its test
- [x] 1.2 Delete `src/Njord.Model/WeatherModel.cs` and its test
- [x] 1.3 Delete `src/Njord.Model/WeatherCondition.cs` and its test
- [x] 1.4 Fix all compilation errors from removed types

## 2. Move Parameter types to Njord.OpenMeteo

- [x] 2.1 Move `ParameterDef.cs` from `src/Njord.Model/` to `src/Njord.OpenMeteo/` (make internal)
- [x] 2.2 Move `ParameterRegistry.cs` from `src/Njord.Model/` to `src/Njord.OpenMeteo/` (make internal)
- [x] 2.3 Move `ResolvedParameterSet.cs` to `src/Njord.OpenMeteo/` or `src/Njord.Core/Configuration/`
- [x] 2.4 Move `ParameterSelection.cs` to `src/Njord.Core/Configuration/` if in Model
- [x] 2.5 Move corresponding tests to `src/Njord.OpenMeteo.Tests/`
- [x] 2.6 Update `ParameterOptions.Resolve()` in Core to not depend on Model-level ParameterRegistry (take resolved set as input or move resolution to OpenMeteo)
- [x] 2.7 Fix all compilation errors from moved types

## 3. Simplify ServiceInfoMappings

- [x] 3.1 Remove `ModelCoverageRegistry` usage from `src/Njord/Configuration/ServiceInfoMappings.cs`
- [x] 3.2 Simplify catalog to list location names + enabled domains (no per-model capabilities)
- [x] 3.3 Remove or simplify `CatalogLocation` if model fields are dropped
- [x] 3.4 Update `src/Njord.Tests/` ServiceInfoMappings tests

## 4. Clean up WeatherModel references

- [x] 4.1 Remove `WeatherModel` from MQTT `TopicScheme` / `TopicSlug`
- [x] 4.2 Update `NjordOptions.Models` from `WeatherModel[]` to `string[]` if still enum-typed
- [x] 4.3 Update `NjordOptionsValidator` — remove model coverage validation
- [x] 4.4 Fix all compilation errors

## 5. Evaluate Horizon

- [x] 5.1 Check if `Horizon` can be replaced with `TimeSpan` in its 3 prod usages
- [x] 5.2 If replaceable: delete `Horizon.cs`, use `TimeSpan` directly
- [x] 5.3 If load-bearing in AlertDetails: keep but document as weather-alert-specific

## 6. Validation

- [x] 6.1 `dotnet build Njord.slnx` — 0 errors
- [x] 6.2 `dotnet run --project Njord.Model.Tests/Njord.Model.Tests.csproj` — only vocabulary type tests remain
- [x] 6.3 `dotnet run --project Njord.OpenMeteo.Tests/Njord.OpenMeteo.Tests.csproj` — parameter tests pass in new home
- [x] 6.4 `dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj` — rules updated
- [x] 6.5 Full test suite loop passes
