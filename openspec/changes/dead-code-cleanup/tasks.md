## 1. Remove dead types

- [x] 1.1 Delete `AlertComputed` variant from `src/Njord.Messages/Pipeline/PipelineEvent.cs`
- [x] 1.2 Delete `IWithTargetId` interface (find and remove file)
- [x] 1.3 Delete or collapse `EnrichmentKind` enum if only 1 member
- [x] 1.4 Check `ForecastValue` in Njord.Model — delete if unreferenced
- [x] 1.5 Fix all compilation errors from removals

## 2. Move PollFailureReason

- [x] 2.1 Move `PollFailureReason` from `src/Njord.Model/` to `src/Njord.Messages/Forecast/`
- [x] 2.2 Update all using statements

## 3. Fix ServiceInfoMappings

- [x] 3.1 Simplify `src/Njord/Configuration/ServiceInfoMappings.cs` — remove multi-model resolution logic
- [x] 3.2 Use Location coordinates from LocationOptions (not `new Location(name, 0, 0)`)
- [x] 3.3 Fix `CatalogLocation` — remove redundant Latitude/Longitude, use Location directly
- [x] 3.4 Update tests for simplified ServiceInfo

## 4. Split StreamWiringMessages

- [x] 4.1 Split `src/Njord.Core/Actors/StreamWiringMessages.cs` into per-owner files
- [x] 4.2 Update all references

## 5. Validation

- [x] 5.1 `dotnet build Njord.slnx` — 0 errors
- [x] 5.2 Run all test projects — 0 failures
