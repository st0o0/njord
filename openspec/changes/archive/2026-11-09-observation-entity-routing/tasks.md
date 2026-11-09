# Tasks

## 1. ObservationResult record change

- [x] 1.1 Add `string FeatureName` positional parameter to `ObservationResult` in `src/Njord.Messages/Results/ObservationResult.cs`. New signature: `ObservationResult(Location Location, string FeatureName, double Value) : FeatureResult(Location)`. Update `src/Njord.Messages.Tests/Results/ObservationResultTests.cs` to pass feature name.

## 2. Observation FeatureRules pass descriptor name

- [x] 2.1 Update all 4 Observation features to pass `Descriptor.Name` when constructing `ObservationResult`: `src/Njord.Features/Observations/Pm25/Pm25Observation.cs`, `src/Njord.Features/Observations/WaveHeight/WaveHeightObservation.cs`, `src/Njord.Features/Observations/FloodDischarge/FloodDischargeObservation.cs`, `src/Njord.Features/Observations/UvIndex/UvIndexObservation.cs`. Update corresponding tests in `src/Njord.Features.Tests/Observations/*/` to assert the feature name on results.

## 3. EntityIdLookup observation routing

- [x] 3.1 Add a second dictionary `Dictionary<(string FeatureName, string Location), string>` to `EntityIdLookup` in `src/Njord.Grpc/EntityIdLookup.cs`. In the constructor, for `OutputKind.Observation` descriptors: register by `(descriptor.Name, location)` in the new dictionary. In `Find`: pattern-match `ObservationResult` first and look up by `(result.FeatureName, result.Location.Name)`, fall through to existing type-based lookup otherwise. Update `src/Njord.Grpc.Tests/EntityIdLookupTests.cs` (or create if absent) to verify observation features get correct entity IDs and that `Find` resolves them.

## 4. Mapping and consumer tests

- [x] 4.1 Update `src/Njord.Grpc.Tests/Mapping/V3/EntityStateUpdateMappingTests.cs` and `src/Njord.Grpc.Tests/Mapping/V3/ObservationValueMappingTests.cs` to pass the new `FeatureName` parameter in test data.

## 5. Validation

Run all touched test suites:

```powershell
dotnet build src/Njord.slnx
dotnet run --project src/Njord.Messages.Tests/Njord.Messages.Tests.csproj
dotnet run --project src/Njord.Features.Tests/Njord.Features.Tests.csproj
dotnet run --project src/Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project src/Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project src/Njord.Pipeline.Tests/Njord.Pipeline.Tests.csproj
```

Then run all remaining test projects to catch any snapshot or cross-project breakage.
