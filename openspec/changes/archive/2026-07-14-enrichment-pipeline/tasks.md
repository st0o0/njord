## M0. Infrastructure — EnrichmentActor + ModelSnapshot

- [ ] 0.1 `ModelSnapshot` domain type: immutable record holding `IReadOnlyDictionary<(string Location, WeatherModel Model), ModelForecast>`, `Update(ModelForecast)` returns new snapshot, `HasChanged` flag set when an update actually replaces an entry. Unit tests in `src/Njord.Tests/Enrichment/ModelSnapshotSpec.cs`.
- [ ] 0.2 `EnrichmentActor` in `src/Njord/Enrichment/EnrichmentActor.cs`: requests `SourceRef<FetchOutcome>` from PipelineActor, materializes Scan(ModelSnapshot) → Where(HasChanged) → BroadcastHub<ModelSnapshot>. Lifecycle: WaitingForSourceRef → Ready. TestKit test in `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`.
- [ ] 0.3 `RequestMqttSink` / `MqttSinkResponse(SinkRef<MqttMessage>)` protocol in MqttEgressActor: new message handler providing a SinkRef to the existing MergeHub. Test in `src/Njord.Tests/Egress/MqttEgressActorSpec.cs`.
- [ ] 0.4 EnrichmentActor requests MqttSinkRef from MqttEgressActor and passes it to consumer streams. Each consumer stream sinks into this SinkRef.
- [ ] 0.5 `EnrichmentOptions` configuration in `src/Njord/Configuration/EnrichmentOptions.cs`: per-consumer `Enabled` flag. Binding in `NjordOptions.Enrichment`. Only enabled consumers are materialized.
- [ ] 0.6 DI registration: register EnrichmentActor in Akka.Hosting, bind `EnrichmentOptions`. `src/Njord/Program.cs`.
- [ ] 0.7 Topic scheme extension: `TopicScheme.ConsensusTopic(baseTopic, location, horizon)`, `TopicScheme.AlertTopic(...)`, `TopicScheme.DerivedTopic(...)`, `TopicScheme.IndexTopic(...)`, `TopicScheme.EnergyTopic(...)`, `TopicScheme.MetaTopic(...)`. Tests in `src/Njord.Tests/Egress/TopicSchemeSpec.cs`.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M1. Consensus Core — 8 Enrichments

- [ ] 1.1 `ConsensusComputer.ComputeMedian(IReadOnlyList<double?> values)` — median over non-null values. Pure static method. Unit tests in `src/Njord.Tests/Enrichment/ConsensusComputerSpec.cs`.
- [ ] 1.2 `ConsensusComputer.ComputeTrimmedMean(values, trimPercent)` — trim upper/lower X%, average the rest.
- [ ] 1.3 `ConsensusComputer.ComputeSpread(values)` — max − min of non-null values.
- [ ] 1.4 `ConsensusComputer.ComputeIqr(values)` — interquartile range (P75 − P25).
- [ ] 1.5 `ConsensusComputer.ComputeAgreement(values, median, tolerance)` — fraction of models within tolerance of median (0.0–1.0).
- [ ] 1.6 `ConsensusComputer.IdentifyOutlier(models, values, median)` — model with largest deviation + deviation value.
- [ ] 1.7 `ConsensusComputer.ComputeConfidenceInterval(values, lowerPercentile, upperPercentile)` — P10/P90 bounds.
- [ ] 1.8 `ConsensusComputer.BuildAvailabilityMatrix(snapshot, horizon)` — which models have data for this time point.
- [ ] 1.9 `ConsensusResult` record: per (parameter, horizon) the median, spread, IQR, agreement, outlier, CI, available models. `ToMqttMessages(...)` serializes as flat JSON identical to the model state schema + meta attributes.
- [ ] 1.10 Consensus consumer stream in EnrichmentActor: `snapshotSource.Select(ConsensusComputer.Compute(...)).SelectMany(ToMqttMessages).RunWith(mqttSinkRef)`. Delta check via lastPublished cache.
- [ ] 1.11 Discovery payload for consensus device: `njord_{location}_consensus` with the same horizons and parameters as model devices, plus meta attributes (spread, agreement, models_used) as sensor attributes. Verify snapshot test in `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs`.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M2. Threshold Alerts — 9 Enrichments

- [ ] 2.1 `AlertEvaluator` static class in `src/Njord/Enrichment/AlertEvaluator.cs`. `AlertResult` record with list of `Alert(Type, Severity, Confidence, Attributes)`.
- [ ] 2.2 Frost warning: `EvaluateFrost(snapshot, threshold=0°C)` — confidence = fraction of models with temp_min ≤ threshold, earliest time, expected low (median). Unit tests in `src/Njord.Tests/Enrichment/AlertEvaluatorSpec.cs`.
- [ ] 2.3 Heat warning: `EvaluateHeat(snapshot, thresholds=[30,35,40])` — levels (yellow/orange/red) based on apparent_temperature_max, confidence per level.
- [ ] 2.4 Storm warning: `EvaluateStorm(snapshot, gustThreshold=16.7)` — wind_gusts_10m ≥ threshold, confidence, expected max gust.
- [ ] 2.5 Heavy rain warning: `EvaluateHeavyRain(snapshot, hourlyThreshold=10, dailyThreshold=25)` — hourly and daily sum.
- [ ] 2.6 UV warning: `EvaluateUv(snapshot)` — WHO levels (low/moderate/high/very_high/extreme) from uv_index consensus.
- [ ] 2.7 Fog risk: `EvaluateFog(snapshot)` — temp − dewpoint < 2°C AND wind < 3 m/s AND humidity > 90%.
- [ ] 2.8 Snow warning: `EvaluateSnow(snapshot)` — snowfall sum + freezing_level_height, confidence.
- [ ] 2.9 Pressure drop / weather front: `EvaluatePressureDrop(snapshot, dropThreshold=5)` — pressure_msl drop ≥ 5 hPa in 3h, confidence.
- [ ] 2.10 CAPE thunderstorm warning: `EvaluateThunderstorm(snapshot)` — CAPE > 1000 J/kg AND precip > 5mm AND gusts > 15 m/s → none/low/moderate/high.
- [ ] 2.11 Alert consumer stream in EnrichmentActor: `snapshotSource.Select(AlertEvaluator.Evaluate(...)).SelectMany(ToMqttMessages).RunWith(mqttSinkRef)`.
- [ ] 2.12 `AlertThresholdOptions` configuration: all thresholds configurable. In `EnrichmentOptions.Alerts`.
- [ ] 2.13 Discovery payload for alert sensors: binary_sensor and enum sensors per alert type. Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M3. Derived Meteorological Values — 7 Enrichments

- [ ] 3.1 `DerivedValues` static class in `src/Njord/Enrichment/DerivedValues.cs`. Unit tests in `src/Njord.Tests/Enrichment/DerivedValuesSpec.cs`.
- [ ] 3.2 Dew-point comfort class: `ClassifyDewpointComfort(dewpoint)` → dry/comfortable/slightly_humid/muggy/oppressive/unbearable.
- [ ] 3.3 Beaufort wind scale: `ToBeaufort(windSpeedMs)` → 0–12 + text description.
- [ ] 3.4 Wind chill: `ComputeWindchill(tempC, windKmh)` — North American formula, only when T ≤ 10°C and V ≥ 4.8 km/h.
- [ ] 3.5 Diurnal temperature amplitude: `ComputeAmplitude(tempMax, tempMin)`.
- [ ] 3.6 Sunshine percentage: `ComputeSunshinePct(sunshineDuration, daylightDuration)` — ratio as percentage.
- [ ] 3.7 WMO weather code → plain text: `DecodeWmoCode(code, isDay)` → (description, condition, iconName). Lookup table for WMO codes 0–99.
- [ ] 3.8 Temperature inversion detection: `DetectInversion(tempSeries, pressureSeries, humiditySeries)` — falling temp + stable pressure + rising humidity.
- [ ] 3.9 Derived consumer stream in EnrichmentActor: computes derived values on consensus (or per model, configurable). Delta check, MqttMessages, RunWith(mqttSinkRef).
- [ ] 3.10 Discovery payload for derived sensors. Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M4. Temporal Analysis & Trends — 6 Enrichments

- [ ] 4.1 `TrendAnalyzer` static class in `src/Njord/Enrichment/TrendAnalyzer.cs`. Unit tests in `src/Njord.Tests/Enrichment/TrendAnalyzerSpec.cs`.
- [ ] 4.2 Trend direction: `AnalyzeTrend(timeSeries, windowHours)` — linear regression over consensus time series → rising/falling/stable + rate.
- [ ] 4.3 Weather change detection: `DetectWeatherChange(snapshot)` — weighted change score from ΔT, ΔP, ΔR, ΔW → boolean + expected_in_hours + change_type.
- [ ] 4.4 Precipitation timing: `FindNextRain(consensusSeries)` → next_rain_in (hours), rain_duration, rain_total. First time point with precip > 0.1mm.
- [ ] 4.5 Extrema timing: `FindExtrema(consensusSeries, parameter)` → peak_at, low_at (times of daily max/min).
- [ ] 4.6 Consensus stability: `ComputeStability(currentSnapshot, previousSnapshot)` — deviation of consensus for the same time points between two consecutive snapshots. Requires `Scan` with (prev, curr) tuple in the trends consumer stream.
- [ ] 4.7 Predictability decay: `ComputePredictability(snapshot, horizons)` — spread as a function of horizon. Growth rate → high/medium/low predictability.
- [ ] 4.8 Trends consumer stream in EnrichmentActor: `snapshotSource.Scan((prev, curr), ...).Select(TrendAnalyzer.Analyze).SelectMany(ToMqttMessages).RunWith(mqttSinkRef)`.
- [ ] 4.9 Discovery payload for trend sensors. Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M5. Daily-Life & Activity Indices — 10 Enrichments

- [ ] 5.1 `IndexScorer` static class in `src/Njord/Enrichment/IndexScorer.cs`. Unit tests in `src/Njord.Tests/Enrichment/IndexScorerSpec.cs`.
- [ ] 5.2 Laundry drying index: `ScoreLaundryDrying(temp, humidity, wind, precipProb)` → poor/fair/good/excellent + best_window (hour range).
- [ ] 5.3 Outdoor activity score: `ScoreOutdoor(temp, precip, wind, cloud, uv)` → 0–100 + best_hours.
- [ ] 5.4 Running/cycling comfort: `ScoreRunning(apparentTemp, wind, precipProb)` and `ScoreCycling(...)` → comfort score + optimal time windows.
- [ ] 5.5 BBQ weather index: `ScoreBbq(temp, precipProb, wind, cloud)` → boolean + score.
- [ ] 5.6 Irrigation recommendation: `EvaluateIrrigation(precipSum, et0, soilMoisture?)` → irrigate boolean + water_deficit_mm. Requires Soil group (optional).
- [ ] 5.7 Heating/cooling degree days: `ComputeDegreeDays(tempMax, tempMin, heatingBase=18, coolingBase=24)` → HDD, CDD per day.
- [ ] 5.8 Solar yield estimate: `EstimateSolarYield(gtiSum, temp, panelKwp)` → kWh per day. Requires Solar group + panel config.
- [ ] 5.9 Window ventilation recommendation: `EvaluateVentilation(dewpointOutside, tempInsideTarget, precipProb, tempOutside)` → ventilate boolean + best_window.
- [ ] 5.10 Frost protection timer: `EvaluateGroundFrost(soilTemp0cm, airTemp, wind, cloud)` → ground_frost_risk boolean + protect_from (time). Requires Soil group.
- [ ] 5.11 VPD plant stress: `ClassifyVpd(vpd)` → mold_risk/optimal/moderate_stress/high_stress.
- [ ] 5.12 Index consumer stream in EnrichmentActor. Delta check, MqttMessages.
- [ ] 5.13 `IndexWeightOptions` configuration: weights and thresholds for each index. Defaults in code.
- [ ] 5.14 Discovery payload for index sensors. Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M6. Energy & Building Management — 5 Enrichments

- [ ] 6.1 `EnergyForecaster` static class in `src/Njord/Enrichment/EnergyForecaster.cs`. Unit tests in `src/Njord.Tests/Enrichment/EnergyForecasterSpec.cs`.
- [ ] 6.2 Heating demand forecast: `ForecastHeatingDemand(tempSeries, windSeries)` → low/medium/high per 6h block.
- [ ] 6.3 Heat pump COP forecast: `EstimateCop(tempOutside)` → COP curve (simplified linear approximation).
- [ ] 6.4 Shading control: `EvaluateShading(directRadiation, temp, isDay)` → shade boolean per orientation (configurable).
- [ ] 6.5 Battery charging strategy: `RecommendBatteryStrategy(solarForecastTomorrow, expectedDemand)` → charge_from_grid_tonight / reserve_for_solar.
- [ ] 6.6 Night cooling: `FindNightCoolingWindow(tempSeries, humiditySeries, targetIndoorTemp)` → start_hour + expected_low_temp.
- [ ] 6.7 `BuildingOptions` configuration: panel kWp, orientations, heating base temperature, indoor target temperature. In `EnrichmentOptions.Energy`.
- [ ] 6.8 Energy consumer stream in EnrichmentActor. Delta check, MqttMessages.
- [ ] 6.9 Discovery payload for energy sensors. Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

## M7. Historical & Learning — 5 Enrichments

- [ ] 7.1 Persistence layer for historical forecasts: store forecast snapshots in SQLite or via Akka.Persistence. Rolling retention (90 days). Design decision: possibly dedicated PersistentActor.
- [ ] 7.2 `AccuracyTracker` in `src/Njord/Enrichment/AccuracyTracker.cs`: store forecast value for t+3h, compare after 3h with actual value (next snapshot, t+0). MAE/RMSE per model and parameter over rolling 30 days. Unit tests in `src/Njord.Tests/Enrichment/AccuracyTrackerSpec.cs`.
- [ ] 7.3 Weighted consensus: `WeightedConsensus.Compute(snapshot, accuracyWeights)` — model weight = 1 / (MAE + ε). Replaces or supplements median consensus.
- [ ] 7.4 Forecast drift detection: `DriftDetector.Compute(currentSnapshot, historicalSnapshot24hAgo)` — deviation of forecast for the same time point over 24h.
- [ ] 7.5 Seasonal model preference: `SeasonalWeights.Compute(accuracyHistory, currentSeason)` — MAE trends per season per model.
- [ ] 7.6 Anomaly detection: `AnomalyDetector.Detect(currentValue, rollingMean30d, rollingStdDev)` — deviation > 2σ flagged as anomaly.
- [ ] 7.7 History consumer stream(s) in EnrichmentActor. Reads historical data, updates accuracy.
- [ ] 7.8 Discovery payload for historical sensors (accuracy, drift, anomaly). Verify snapshot tests.

### Validation
```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```
