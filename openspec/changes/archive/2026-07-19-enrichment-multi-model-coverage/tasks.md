## 1. Daily Consensus — Domain & Computation

- [x] 1.1 Extend `ConsensusResult` record in `src/Njord/Domain/Analysis/ConsensusResult.cs` to add `DailyParameters` property (`IReadOnlyList<ParameterConsensus>`). Update `Compute` to iterate `parameters.Daily`, look up values from `ModelForecast.Daily` by `DateOnly`, and produce day-horizon keys `d0`–`dN`. Compute cutoff as the second-shortest daily series length across models (matching hourly cutoff logic).
- [x] 1.2 Add tests in `src/Njord.Tests/Domain/Analysis/ConsensusResultSpec.cs`: daily consensus with 3+ models, single-model filter (< 2 excluded), empty daily parameter set, missing daily points produce null, day offset computation from cycle timestamp.

## 2. Daily Consensus — Discovery & Egress

- [x] 2.1 Update `ConsensusEnrichment.BuildDiscoveryPayload` in `src/Njord/Enrichment/Features/ConsensusEnrichment.cs` to emit daily components keyed `{param.JsonKey}_d{N}` with sub-topic `d{N}`. Introduce `_maxDiscoveryDays` (from `ForecastDays`).
- [x] 2.2 Update `StatePayloadBuilder.FromConsensus` in `src/Njord/Mqtt/StatePayloadBuilder.cs` to emit one MQTT message per daily horizon (`{baseTopic}/{location}/consensus/d{N}`).
- [x] 2.3 Add/update tests in `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` and `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` for daily consensus discovery components and state messages.

## 3. Indices Envelope — Domain

- [x] 3.1 Extend `IndexResult` record in `src/Njord/Domain/Analysis/IndexResult.cs` with envelope fields: `OutdoorMin/Max/Confidence`, `LaundryMin/Max/Confidence`, etc. for all 8 scores. Update `Compute` to evaluate each model independently then aggregate (min, max, confidence = fraction within tolerance of median).
- [x] 3.2 Add tests in `src/Njord.Tests/Domain/Analysis/IndexResultSpec.cs`: per-model scoring isolation, envelope min/max/confidence computation, single-model fallback (min=max, confidence=1.0), confidence tolerance.

## 4. Indices Envelope — Discovery & Egress

- [x] 4.1 Update `IndexEnrichment.BuildDiscoveryPayload` in `src/Njord/Enrichment/Features/IndexEnrichment.cs` to register `_min`, `_max`, `_confidence` sensor components per score.
- [x] 4.2 Update `StatePayloadBuilder.FromIndices` in `src/Njord/Mqtt/StatePayloadBuilder.cs` to include envelope fields in state JSON.
- [x] 4.3 Add/update tests in `src/Njord.Tests/Mqtt/StatePayloadBuilderSpec.cs` and `src/Njord.Tests/Mqtt/DiscoveryPayloadBuilderSpec.cs` for index envelope components and state payload.

## 5. Energy Envelope — Domain

- [x] 5.1 Extend `EnergyResult` record in `src/Njord/Domain/Analysis/EnergyResult.cs` with `HeatingDemandMax` (int), `CopEstimateMin` (double?), `CopOptimalConservative` (IReadOnlyList). Update `Compute` to evaluate each model independently, then derive worst-case (max HeatingDemand, min COP, intersection of CopOptimal hours).
- [x] 5.2 Add tests in `src/Njord.Tests/Domain/Analysis/EnergyResultSpec.cs`: per-model energy computation, HeatingDemandMax from worst model, CopEstimateMin, CopOptimalConservative as intersection, single-model fallback.

## 6. Energy Envelope — Discovery & Egress

- [x] 6.1 Update `EnergyEnrichment.BuildDiscoveryPayload` in `src/Njord/Enrichment/Features/EnergyEnrichment.cs` to register `heating_demand_max`, `cop_estimate_min`, `cop_optimal_conservative` sensor components.
- [x] 6.2 Update `StatePayloadBuilder.FromEnergy` in `src/Njord/Mqtt/StatePayloadBuilder.cs` to include envelope fields.
- [x] 6.3 Add/update tests for energy envelope discovery and state payload.

## 7. Alerts — Daily Threshold Evaluation

- [x] 7.1 Update `AlertEvaluator.EvaluateHeavyRain` in `src/Njord/Domain/Analysis/AlertEvaluator.cs` to additionally check `DailyForecastSeries` for `precipitation_sum` against `HeavyRainDailyThreshold`. Final severity/confidence = max of hourly and daily evaluations.
- [x] 7.2 Update `AlertEvaluator.EvaluateUv` to additionally check `DailyForecastSeries` for `uv_index_max`. Final severity = max of hourly and daily evaluations.
- [x] 7.3 Update `AlertEvaluator.EvaluateSnow` to additionally check `DailyForecastSeries` for `snowfall_sum`. Final severity/confidence = max of hourly and daily evaluations.
- [x] 7.4 Add `HeavyRainDailyThreshold` to `AlertThresholdOptions` in `src/Njord/Configuration/EnrichmentOptions.cs` (default: 30.0 mm).
- [x] 7.5 Add tests in `src/Njord.Tests/Domain/Analysis/AlertEvaluatorSpec.cs` (or equivalent): daily-only trigger, hourly+daily combined (max wins), daily data unavailable (graceful fallback), UV daily max, snow daily sum.

## 8. Validation

- [x] 8.1 Run full test suite: `dotnet run --project Njord.Tests/Njord.Tests.csproj` from `src/`
- [x] 8.2 Run `dotnet slopwatch` from repo root
- [x] 8.3 Verify build: `dotnet build Njord.slnx` from `src/`
