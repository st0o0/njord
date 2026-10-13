## 1. Relocate ModelCoverageRegistry → Njord.Weather

- [x] 1.1 Move `src/Njord.Core/Configuration/ModelCoverageRegistry.cs` to `src/Njord.Weather/ModelCoverageRegistry.cs`. The file contains `ModelCoverageRegistry`, `ModelCoverage`, `BoundingBox`, `CoverageTier` — move all. Update namespace from `Njord.Core.Configuration` to `Njord.Weather`.
- [x] 1.2 Update `using` statements in consumers: `src/Njord.Core/Configuration/NjordOptionsValidator.cs`, `src/Njord.OpenMeteo/OpenMeteoClient.cs`, `src/Njord.Grpc/CatalogMapper.cs`, `src/Njord.Mqtt/MqttDiscoveryPayload.cs`.
- [x] 1.3 Move `src/Njord.Core.Tests/Configuration/ModelCoverageRegistrySpec.cs` to `src/Njord.Weather.Tests/ModelCoverageRegistrySpec.cs`. Update namespace and project reference if needed.
- [x] 1.4 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 2. Relocate HorizonProjection → Njord.Weather

- [x] 2.1 Move `src/Njord.Core/Egress/HorizonProjection.cs` to `src/Njord.Weather/HorizonProjection.cs`. Update namespace from `Njord.Core.Egress` to `Njord.Weather`.
- [x] 2.2 Update `using` statements in consumers: `src/Njord.Mqtt/StatePayloadBuilder.cs`, `src/Njord.Grpc/WeatherGrpcService.cs`.
- [x] 2.3 Move `src/Njord.Core.Tests/Egress/HorizonProjectionSpec.cs` to `src/Njord.Weather.Tests/HorizonProjectionSpec.cs`. Update namespace.
- [x] 2.4 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 3. Relocate TopicSlug → Njord.Mqtt

- [x] 3.1 Move `src/Njord.Core/Egress/TopicSlug.cs` to `src/Njord.Mqtt/TopicSlug.cs`. Update namespace from `Njord.Core.Egress` to `Njord.Mqtt`.
- [x] 3.2 Move `src/Njord.Core.Tests/Egress/TopicSlugSpec.cs` to `src/Njord.Mqtt.Tests/TopicSlugSpec.cs`. Update namespace.
- [x] 3.3 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
```

## 4. Add EnrichmentKind enum and IEnrichmentEvent

- [x] 4.1 Add `EnrichmentKind` enum (`Consensus`, `Alerts`, `Derived`, `Trends`, `Indices`) to `src/Njord.Core/Egress/EnrichmentKind.cs`.
- [x] 4.2 Add `IEnrichmentEvent` interface with `EnrichmentKind Kind { get; }` to `src/Njord.Core/Egress/PipelineEvent.cs` (same file). Implement on `ConsensusComputed`, `AlertComputed`, `DerivedComputed`, `TrendComputed`, `IndexComputed`.
- [x] 4.3 Replace bare `"consensus"`, `"alerts"`, `"derived"`, `"trends"`, `"indices"` strings in `src/Njord.Grpc/OpsGrpcService.cs` (~5 occurrences) and `src/Njord.Grpc/EnrichmentProtoMapper.cs` (~5 occurrences) with `EnrichmentKind` enum references.
- [x] 4.4 Replace bare enrichment type strings in `src/Njord.Mqtt/StatePayloadBuilder.cs` (~6 occurrences) with `EnrichmentKind` references.
- [x] 4.5 Add tests in `src/Njord.Core.Tests/Egress/EnrichmentKindSpec.cs`: verify each `PipelineEvent` enrichment variant returns the correct `Kind`, verify `ForecastReceived` does not implement `IEnrichmentEvent`.
- [x] 4.6 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
dotnet run --project Njord.Mqtt.Tests/Njord.Mqtt.Tests.csproj
```

## 5. Add ConsensusMethod enum

- [x] 5.1 Add `ConsensusMethod` enum (`Mean`, `Median`, `TrimmedMean`) to `src/Njord.Compute/Configuration/ConsensusMethod.cs`.
- [x] 5.2 Change `ConsensusOptions.Method` from `string` to `ConsensusMethod` in `src/Njord.Core/Configuration/ConsensusOptions.cs`. Default to `ConsensusMethod.Median`.
- [x] 5.3 Simplify `src/Njord.Core/Configuration/ConsensusOptionsValidator.cs`: replace the `HashSet<string>` check with `Enum.IsDefined(options.Method)`.
- [x] 5.4 Update `src/Njord.Compute/Analysis/ConsensusComputer.cs` to switch on `ConsensusMethod` enum instead of string comparison.
- [x] 5.5 Update tests in `src/Njord.Core.Tests/Configuration/ConsensusOptionsValidatorSpec.cs` and `src/Njord.Compute.Tests/` for enum-based method selection.
- [x] 5.6 Update `appsettings.Development.json` if it sets `Consensus:Method` as a string (ASP.NET Core binds enum names from JSON natively).
- [x] 5.7 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Core.Tests/Njord.Core.Tests.csproj
dotnet run --project Njord.Compute.Tests/Njord.Compute.Tests.csproj
```

## 6. Complete ParameterRegistry typed properties

- [x] 6.1 Add typed static properties to `src/Njord.Weather/ParameterRegistry.cs` for the 4 missing hourly parameters: `Rain`, `SoilTemperature0cm`, `Visibility`, `WindDirection10m`.
- [x] 6.2 Add typed static properties for the 10 missing daily parameters: `PrecipitationSum`, `UvIndexMax`, `SnowfallSum`, `Temperature2mMax`, `Temperature2mMin`, `WindSpeed10mMax`, `WindGusts10mMax`, `Sunrise`, `Sunset`, `WeatherCode`.
- [x] 6.3 Replace `GetByApiName("...")` calls in `src/Njord.Alerts/AlertEvaluator.cs` (6 calls) with the new typed properties.
- [x] 6.4 Replace `GetByApiName("...")` calls in `src/Njord.Grpc/WeatherGrpcService.cs` (10+ calls) with the new typed properties.
- [x] 6.5 Add tests in `src/Njord.Weather.Tests/ParameterRegistrySpec.cs` verifying each new typed property returns the correct `ParameterDef` (matching api name, unit, value type).
- [x] 6.6 Build: `dotnet build Njord.slnx` — zero errors.

**Validation:**
```powershell
dotnet run --project Njord.Weather.Tests/Njord.Weather.Tests.csproj
dotnet run --project Njord.Alerts.Tests/Njord.Alerts.Tests.csproj
dotnet run --project Njord.Grpc.Tests/Njord.Grpc.Tests.csproj
```

## 7. Architecture test + final validation

- [x] 7.1 Verify `src/Njord.Architecture.Tests/LayerReferenceSpec.cs` still passes — the moves respect the existing reference direction (Weather has no Njord refs, Mqtt refs only Core and below).
- [x] 7.2 Verify `src/Njord.Architecture.Tests/ZoneArchitectureSpec.cs` still passes — no lateral references introduced.
- [x] 7.3 Run full suite: `dotnet build Njord.slnx` then sequential test loop across all `Njord.*Tests` projects — zero failures.

**Validation:**
```powershell
dotnet run --project Njord.Architecture.Tests/Njord.Architecture.Tests.csproj
```

## 8. Update AGENTS.md

- [x] 8.1 Update `AGENTS.md`: reflect that `ModelCoverageRegistry` and `HorizonProjection` now live in `Njord.Weather`, `TopicSlug` in `Njord.Mqtt`. Mention `EnrichmentKind` enum and `ConsensusMethod` enum. Note that `GetByApiName` string-literal usage is not permitted in production code.
