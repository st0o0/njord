## 1. Configuration

- [x] 1.1 Add optional `Timezone` property (string, IANA id) to `src/Njord/Configuration/LocationOptions.cs`. Add validation in config builder (`src/Njord/Configuration/ConfigBuilder.cs`) that calls `TimeZoneInfo.FindSystemTimeZoneById` at startup and fails with a descriptive error for invalid values. Test: `src/Njord.Tests/Configuration/LocationOptionsSpec.cs` — valid timezone resolves, invalid throws, null defaults to UTC.

## 2. Domain Types

- [x] 2.1 Create `DailyConsensusSummary` sealed record in `src/Njord/Domain/Analysis/DailyConsensusSummary.cs` with properties: `Date` (DateOnly), `TemperatureMax` (double?), `TemperatureMin` (double?), `PrecipitationSum` (double?), `WindSpeedMax` (double?), `WeatherCode` (int?), `Spread` (double?), `Agreement` (double?), `AvailableModels` (int).
- [x] 2.2 Add static `Aggregate` method to `DailyConsensusSummary` that takes `ConsensusResult`, `DateTimeOffset now`, and `TimeZoneInfo tz`. Groups hourly horizons by calendar day in the given timezone. For each day: temperature_max = max of temperature_2m medians, temperature_min = min of temperature_2m medians, precipitation_sum = sum of precipitation medians, wind_speed_max = max of wind_speed_10m medians, weather_code = median (rounded) at horizon closest to local noon, spread = average of temperature_2m spreads, agreement = average of temperature_2m agreements, available_models = minimum across hours. Test: `src/Njord.Tests/Domain/Analysis/DailyConsensusSummarySpec.cs` — all aggregation scenarios from the spec.
- [x] 2.3 Add `DailySummaries` property (`IReadOnlyList<DailyConsensusSummary>`) to the `ConsensusResult` record in `src/Njord/Domain/Analysis/ConsensusResult.cs`. Provide a default empty list. Update the secondary constructor.

## 3. Enrichment Integration

- [x] 3.1 Update `ConsensusEnrichment.Compute` in `src/Njord/Enrichment/Features/ConsensusEnrichment.cs` to resolve the location's `TimeZoneInfo` from config (inject `IOptions<NjordOptions>` to access locations), call `DailyConsensusSummary.Aggregate`, and attach the result to `ConsensusResult`. The filtered result keeps only summaries where `AvailableModels >= 2`.

## 4. Proto & gRPC

- [x] 4.1 Add `DailyConsensus` message to `protos/njord/v2/common.proto` (Enrichment: Consensus section) with fields: date (string, 1), temperature_max (optional double, 2), temperature_min (optional double, 3), precipitation_sum (optional double, 4), wind_speed_max (optional double, 5), weather_code (optional int32, 6), spread (optional double, 7), agreement (optional double, 8), available_models (int32, 9). Add `repeated DailyConsensus daily = 2` to `ConsensusUpdate`.
- [x] 4.2 Extend `EnrichmentProtoMapper.MapConsensus` in `src/Njord/Grpc/EnrichmentProtoMapper.cs` to map `ConsensusResult.DailySummaries` to `ConsensusUpdate.Daily`. Test: `src/Njord.Tests/Grpc/EnrichmentProtoMapperSpec.cs` — verify daily entries are mapped with correct field values, and empty summaries produce no daily entries.

## 5. Validation

- [x] 5.1 Run `dotnet build Njord.slnx` from `src/` — must compile without errors.
- [x] 5.2 Run `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/` — all tests pass.
- [x] 5.3 Run `dotnet slopwatch` from repo root — no regressions.
