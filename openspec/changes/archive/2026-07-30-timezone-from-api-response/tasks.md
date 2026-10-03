## 1. Ingest: Request and Response Changes

- [x] 1.1 Add `Timezone` property to `OpenMeteoForecastResponse` in `src/Njord/Ingest/OpenMeteoDtos.cs` (JsonPropertyName `"timezone"`, nullable string)
- [x] 1.2 Add `&timezone=auto` to the query string in `OpenMeteoClient.BuildUri` (`src/Njord/Ingest/OpenMeteoClient.cs`)
- [x] 1.3 In `OpenMeteoClient.FetchAsync`, resolve `TimeZoneInfo` from the response's `Timezone` field (fall back to `TimeZoneInfo.Utc` if null, fail as `MalformedPayload` if unrecognized) and pass it to `ModelForecast`
- [x] 1.4 Update `OpenMeteoClientSpec` (`src/Njord.Tests/Ingest/OpenMeteoClientSpec.cs`): verify `timezone=auto` in request URI, verify `ModelForecast.TimeZone` is set from response, verify unrecognized timezone returns `MalformedPayload`

## 2. Domain: ModelForecast

- [x] 2.1 Add `TimeZoneInfo TimeZone` parameter to the `ModelForecast` record (`src/Njord/Domain/Weather/ModelForecast.cs`)
- [x] 2.2 Update all `ModelForecast` construction sites: `FakeOpenMeteoClient` (`src/Njord.Tests.Shared/FakeOpenMeteoClient.cs`), `ModelSnapshotSpec`, `ModelForecastSpec`, and any other test helpers — default to `TimeZoneInfo.Utc`
- [x] 2.3 Update snapshot serialization verified.txt if the wire format changes (`src/Njord.Tests/Persistence/EnrichmentResultSerializationSpec.*.verified.txt`)

## 3. Config: Remove Timezone

- [x] 3.1 Remove `Timezone` property and `ResolveTimeZone()` method from `LocationOptions` (`src/Njord/Configuration/LocationOptions.cs`)
- [x] 3.2 Remove timezone validation from `NjordOptionsValidator` (`src/Njord/Configuration/NjordOptionsValidator.cs`)
- [x] 3.3 Remove timezone-related tests from `LocationOptionsSpec` (`src/Njord.Tests/Configuration/LocationOptionsSpec.cs`)
- [x] 3.4 Remove any `Timezone` entries from `appsettings.Development.json` if present

## 4. Enrichment: Data-Driven Timezone

- [x] 4.1 In `ConsensusEnrichment` (`src/Njord/Enrichment/Features/ConsensusEnrichment.cs`): remove `_locationTimeZones` dictionary and its initialization from the constructor. Replace with a helper that extracts `TimeZoneInfo` from the first `ModelForecast` entry for the location in the snapshot (fallback `TimeZoneInfo.Utc`)
- [x] 4.2 Update `ConsensusEnrichment` tests to verify timezone is sourced from snapshot data rather than config
- [x] 4.3 Update `DailyConsensusSummarySpec` (`src/Njord.Tests/Domain/Analysis/DailyConsensusSummarySpec.cs`) if any tests reference `LocationOptions.Timezone` or `ResolveTimeZone()`

## 5. Affected Test Suites

- [x] 5.1 Update `WeatherGrpcServiceSpec` and `EnrichmentProtoMapperSpec` if they construct `ModelForecast` directly
- [x] 5.2 Fix any remaining compilation errors across the test projects

## 6. Validation

- [x] 6.1 Build: `dotnet build Njord.slnx` from `src/`
- [x] 6.2 Run all tests: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 6.3 Run slopwatch: `dotnet slopwatch` from repo root
