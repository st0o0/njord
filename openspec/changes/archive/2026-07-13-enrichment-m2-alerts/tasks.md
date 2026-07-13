## 1. Alert Domain Types

- [x] 1.1 `AlertType` enum and `AlertSeverity` enum in `src/Njord/Enrichment/AlertEvaluator.cs`: AlertType { Frost, Heat, Storm, HeavyRain, Uv, Fog, Snow, PressureDrop, Thunderstorm }; AlertSeverity { None, Yellow, Orange, Red }.
- [x] 1.2 `Alert` record in `src/Njord/Enrichment/AlertEvaluator.cs`: `Alert(AlertType Type, AlertSeverity Severity, double Confidence, IReadOnlyDictionary<string, object?> Attributes)`. Static helper `Alert.None(AlertType)` returning severity None, confidence 0, empty attributes.
- [x] 1.3 `AlertResult` record in `src/Njord/Enrichment/AlertResult.cs`: holds `string Location` and `IReadOnlyList<Alert> Alerts`. `ToMqttMessages(string baseTopic)` produces one retained `MqttMessage` per alert on `{baseTopic}/{location}/alerts/{kebab-type}` with flat JSON payload `{"severity":"yellow","confidence":0.75,...attributes}`.
- [x] 1.4 Unit tests for Alert construction and AlertResult.ToMqttMessages in `src/Njord.Tests/Enrichment/AlertResultSpec.cs`.

## 2. AlertThresholdOptions Configuration

- [x] 2.1 `AlertThresholdOptions` in `src/Njord/Configuration/EnrichmentOptions.cs`: `bool Enabled` (default `true`), `double FrostThreshold` (0), `double[] HeatThresholds` ([30,35,40]), `double StormGustThreshold` (16.7), `double HeavyRainHourlyThreshold` (10), `double HeavyRainDailyThreshold` (25), `double PressureDropThreshold` (5), `double CapeThreshold` (1000), `double ThunderstormPrecipThreshold` (5), `double ThunderstormGustThreshold` (15).
- [x] 2.2 Add `AlertThresholdOptions Alerts` property to `EnrichmentOptions`.

## 3. AlertEvaluator — Pure Functions

- [x] 3.1 `AlertEvaluator.EvaluateFrost(ModelSnapshot, string location, double threshold, TimeProvider)` in `src/Njord/Enrichment/AlertEvaluator.cs`. Scans `temperature_2m` minima in next 24 h per model. Returns Alert with confidence, expected_low (median), earliest_frost, models_agreeing. Unit tests in `src/Njord.Tests/Enrichment/AlertEvaluatorSpec.cs`: all agree, none agree, partial.
- [x] 3.2 `AlertEvaluator.EvaluateHeat(ModelSnapshot, string location, double[] thresholds, TimeProvider)`. Scans `apparent_temperature` maxima. Tiered severity: Yellow ≥ thresholds[0], Orange ≥ thresholds[1], Red ≥ thresholds[2]. Confidence = fraction at highest triggered tier. Tests: extreme, moderate, none.
- [x] 3.3 `AlertEvaluator.EvaluateStorm(ModelSnapshot, string location, double gustThreshold, TimeProvider)`. Scans `wind_gusts_10m` maxima. Confidence = fraction ≥ threshold. Attributes: expected_max_gust (median). Tests: storm, no storm.
- [x] 3.4 `AlertEvaluator.EvaluateHeavyRain(ModelSnapshot, string location, double hourlyThreshold, double dailyThreshold, TimeProvider)`. Checks max hourly `precipitation` and daily `precipitation_sum`. Severity: Yellow (hourly), Orange (daily), Red (both). Confidence = fraction exceeding either. Tests: hourly, daily, both, none.
- [x] 3.5 `AlertEvaluator.EvaluateUv(ModelSnapshot, string location, TimeProvider)`. Max `uv_index` consensus median. WHO levels. Severity: None(low), Yellow(moderate), Orange(high), Red(very_high/extreme). Tests: high UV, low UV.
- [x] 3.6 `AlertEvaluator.EvaluateFog(ModelSnapshot, string location, TimeProvider)`. Per model per hour: temp − dewpoint < 2 AND wind < 3 AND humidity > 90. Confidence = fraction with ≥1 fog hour. Attributes: fog_hours (median). Tests: fog likely, no fog.
- [x] 3.7 `AlertEvaluator.EvaluateSnow(ModelSnapshot, string location, TimeProvider)`. Sums `snowfall` per model in 24 h. Severity: Yellow (any), Orange (>5 cm median), Red (>20 cm). Confidence = fraction with sum > 0. Attributes: expected_accumulation, freezing_level. Tests: light, heavy, none.
- [x] 3.8 `AlertEvaluator.EvaluatePressureDrop(ModelSnapshot, string location, double dropThreshold, TimeProvider)`. 3 h sliding windows on `pressure_msl`. Confidence = fraction with ≥1 window ≥ threshold. Attributes: max_drop (median). Tests: front approaching, stable.
- [x] 3.9 `AlertEvaluator.EvaluateThunderstorm(ModelSnapshot, string location, double capeThreshold, double precipThreshold, double gustThreshold, TimeProvider)`. Per model per hour: cape > threshold AND precip > threshold AND gusts > threshold. Confidence = fraction meeting all 3. Severity: None(0), Yellow(<0.5), Orange(0.5–0.75), Red(>0.75). Tests: likely, none.
- [x] 3.10 `AlertEvaluator.EvaluateAll(ModelSnapshot, string location, AlertThresholdOptions, TimeProvider)` orchestrator returning `AlertResult` with all 9 alerts. Test: returns 9 alerts for any snapshot.

## 4. Topic Scheme Extension

- [x] 4.1 `TopicScheme.AlertDeviceId(string location)` returning `njord_{slug}_alerts` in `src/Njord/Egress/TopicScheme.cs`.
- [x] 4.2 `TopicScheme.AlertTopic(string baseTopic, string location, string alertType)` returning `{baseTopic}/{slug}/alerts/{alertType}`.
- [x] 4.3 `AlertType.ToTopicSegment()` extension returning kebab-case: Frost→"frost", HeavyRain→"heavy-rain", PressureDrop→"pressure-drop", Thunderstorm→"thunderstorm", etc.
- [x] 4.4 Tests in `src/Njord.Tests/Egress/TopicSchemeSpec.cs`: alert device id, alert topic, all type segments.

## 5. Discovery Payload for Alerts Device

- [x] 5.1 Extend `DiscoveryPayloadBuilder` with `BuildAlerts(location, mqttOptions, pollInterval, version)` producing a device payload for `njord_{location}_alerts` with 9 `binary_sensor` components. Each component: `value_template` = `{% if value_json.severity != 'none' %}ON{% else %}OFF{% endif %}`, JSON attributes for severity, confidence, and diagnostics.
- [x] 5.2 Tests in `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs`: 9 components, binary_sensor platform, value template, device id.

## 6. Alert Consumer Stream in EnrichmentActor

- [x] 6.1 `MaterializeAlertConsumer(Source<ModelSnapshot, NotUsed>, IMaterializer)` in `EnrichmentActor`: `snapshotSource.SelectMany(snap => { foreach location: AlertEvaluator.EvaluateAll(...).ToMqttMessages(...) })` with delta publishing closure and Resume supervision, sinking into MqttSinkRef. Called from `MaterializeEnrichmentGraph()` when `Alerts.Enabled`.
- [x] 6.2 TestKit test in `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs`: alert consumer materializes when enabled, skips when disabled.

## 7. Discovery Integration

- [x] 7.1 Extend `MqttEgressActor.PublishDiscovery()` to publish alert device discovery when `EnrichmentOptions.Alerts.Enabled` is true.
- [x] 7.2 Test: alert discovery published alongside model and consensus discovery when enabled.

## 8. Validation

```
dotnet run --project src/Njord.Tests/Njord.Tests.csproj
```

All existing tests SHALL continue to pass. New tests:
- `src/Njord.Tests/Enrichment/AlertEvaluatorSpec.cs`
- `src/Njord.Tests/Enrichment/AlertResultSpec.cs`
- `src/Njord.Tests/Egress/TopicSchemeSpec.cs` (extended)
- `src/Njord.Tests/Egress/DiscoveryPayloadBuilderSpec.cs` (extended)
- `src/Njord.Tests/Enrichment/EnrichmentActorSpec.cs` (extended)
