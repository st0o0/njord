## 1. Per-Parameter Model Counts in MQTT Payload

- [x] 1.1 Update `StatePayloadBuilder.FromConsensus()` in `src/Njord/Mqtt/StatePayloadBuilder.cs`: for each parameter add a `{jsonKey}_models` field with the count from `hc.AvailableModels.Count`. Remove the global `_models_used` field. Keep `_spread` and `_agreement` from the first parameter.

## 2. Tests

- [x] 2.1 Update or add tests in `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` (or relevant test file) verifying the new per-parameter `_models` fields and absence of `_models_used`.

## 3. Validation

- [x] 3.1 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`.
- [x] 3.2 Run slopwatch: `dotnet slopwatch` from repo root.
