## 1. AlertType enum and extensions

- [x] 1.1 Add `Ice`, `WindChill`, `Visibility`, `TropicalNight`, `Humidity` to `AlertType` enum in `src/Njord/Domain/Analysis/AlertType.cs`
- [x] 1.2 Add topic segment mappings in `src/Njord/Domain/Analysis/AlertTypeExtensions.cs`: ice, wind-chill, visibility, tropical-night, humidity

## 2. AlertOptions configuration changes

- [x] 2.1 Rename `FrostThreshold` (double) to `FrostThresholds` (double[], default [0, -5, -15]) in `src/Njord/Configuration/AlertOptions.cs`
- [x] 2.2 Rename `StormGustThreshold` (double) to `StormGustThresholds` (double[], default [17, 25, 33]) in `src/Njord/Configuration/AlertOptions.cs`
- [x] 2.3 Add `FogPersistentHours` (int, default 4) to `src/Njord/Configuration/AlertOptions.cs`
- [x] 2.4 Add `PressureDropSevereThreshold` (double, default 10.0) to `src/Njord/Configuration/AlertOptions.cs`
- [x] 2.5 Add new alert config fields: `IceThreshold` (double, default 2.0), `WindChillThresholds` (double[], default [-10, -20, -30]), `VisibilityThresholds` (double[], default [1000, 200, 50]), `TropicalNightThresholds` (double[], default [20, 23, 25]), `HumidityThresholds` (double[], default [16, 21, 24]) in `src/Njord/Configuration/AlertOptions.cs`
- [x] 2.6 Update config validation in `src/Njord/Configuration/NjordServiceSetup.cs` and any validators to handle the renamed and new fields

## 3. Severity extensions for existing evaluators

- [x] 3.1 Refactor `EvaluateFrost` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to accept `double[]` thresholds and produce Yellow/Orange/Red based on tiered min temperature. Write tests in `src/Njord.Tests/Domain/Analysis/AlertEvaluatorSpec.cs`
- [x] 3.2 Refactor `EvaluateStorm` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to accept `double[]` thresholds and produce Yellow/Orange/Red based on tiered max gust. Write tests
- [x] 3.3 Extend `EvaluateFog` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to produce Orange when fogHours >= fogPersistentHours. Write tests
- [x] 3.4 Extend `EvaluatePressureDrop` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to produce Orange when maxDrop >= severeThreshold. Write tests

## 4. New evaluator: Ice

- [x] 4.1 Add `Rain` static field to `AlertEvaluator` referencing `ParameterRegistry.GetByApiName("rain")`
- [x] 4.2 Add `SoilTemp0cm` static field referencing `ParameterRegistry.GetByApiName("soil_temperature_0cm")`
- [x] 4.3 Implement `EvaluateIce` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` — scan rain + temperature_2m consensus, escalate with soil_temperature_0cm
- [x] 4.4 Write `EvaluateIce` tests: rain at near-freezing (Yellow), freezing rain (Orange), frozen ground (Red), snow-only (None), missing rain param (None), missing soil temp (Orange max). Tests in `src/Njord.Tests/Domain/Analysis/AlertEvaluatorSpec.cs`

## 5. New evaluator: WindChill

- [x] 5.1 Implement `EvaluateWindChill` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` — scan apparent_temperature min, compute wind_factor from temperature_2m delta
- [x] 5.2 Write `EvaluateWindChill` tests: moderate (Yellow), severe (Orange with frostbite_30min), extreme (Red with frostbite_10min), cold-but-not-extreme (None)

## 6. New evaluator: Visibility

- [x] 6.1 Add `Visibility` static field to `AlertEvaluator` referencing `ParameterRegistry.GetByApiName("visibility")`
- [x] 6.2 Implement `EvaluateVisibility` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` — scan visibility min median against tiered thresholds
- [x] 6.3 Write `EvaluateVisibility` tests: reduced (Yellow), dense (Orange), near-zero (Red), good (None), missing param (None)

## 7. New evaluator: TropicalNight

- [x] 7.1 Implement `EvaluateTropicalNight` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` — filter night hours via is_day, find min temperature, compare against thresholds
- [x] 7.2 Write `EvaluateTropicalNight` tests: tropical (Yellow), severe (Orange), extreme (Red), cool (None), no night hours (None)

## 8. New evaluator: Humidity

- [x] 8.1 Implement `EvaluateHumidity` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` — filter daytime hours via is_day, find max dewpoint, compare against thresholds
- [x] 8.2 Write `EvaluateHumidity` tests: muggy (Yellow), oppressive (Orange), tropical (Red), comfortable (None), no daytime hours (None)

## 9. Wire into EvaluateAll

- [x] 9.1 Update `EvaluateAll` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to call all 5 new evaluators and pass updated parameters for severity-extended evaluators
- [x] 9.2 Update `AlertEnrichment` in `src/Njord/Enrichment/Features/AlertEnrichment.cs` to pass new AlertOptions fields to EvaluateAll
- [x] 9.3 Update `AlertEnrichmentSpec` in `src/Njord.Tests/Enrichment/Features/AlertEnrichmentSpec.cs` to verify 14 alerts in result

## 10. Egress: Discovery and State payloads

- [x] 10.1 Update `DiscoveryPayloadBuilder` in `src/Njord/Mqtt/DiscoveryPayloadBuilder.cs` to produce 14 binary_sensor components for the alerts device
- [x] 10.2 Update `StatePayloadBuilder` in `src/Njord/Mqtt/StatePayloadBuilder.cs` to include all 14 alert types in state JSON
- [x] 10.3 Update discovery and state payload specs in `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs` and `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs`

## 11. Persistence compatibility

- [x] 11.1 Update `EnrichmentSnapshotDtos` in `src/Njord/Persistence/EnrichmentSnapshotDtos.cs` to handle new alert types — old snapshots without new types SHALL produce Alert.None on recovery
- [x] 11.2 Update serialization specs in `src/Njord.Tests/Persistence/EnrichmentSnapshotDtoSerializationSpec.cs` and `src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.cs`

## 12. gRPC mapping

- [x] 12.1 Update `EnrichmentProtoMapper` in `src/Njord/Grpc/EnrichmentProtoMapper.cs` to map new AlertType enum values
- [x] 12.2 Update `EnrichmentProtoMapperSpec` in `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs`

## 13. AlertResult spec

- [x] 13.1 Update `AlertResultSpec` in `src/Njord.Tests/Domain/Analysis/AlertResultSpec.cs` to verify 14-alert round-trip serialization

## 14. Validation

- [x] 14.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 14.2 Run `dotnet build Njord.slnx` from `src/` to verify zero warnings
- [x] 14.3 Run `dotnet slopwatch` from repo root
- [x] 14.4 Run `dotnet format --verify-no-changes` from `src/`
