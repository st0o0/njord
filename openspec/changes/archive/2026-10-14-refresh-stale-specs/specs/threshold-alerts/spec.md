## MODIFIED Requirements

### Requirement: Storm warning evaluates wind gusts against threshold
`StormRule.Evaluate(consensus, settings)` SHALL scan the consensus median of `wind_gusts_10m` over the next 24 h and classify the maximum median gust against the storm `SeverityThresholds` (default [17, 25, 33] m/s). Confidence SHALL be the average consensus agreement of the hours whose median gust reaches the advisory threshold; when no hour reaches it, severity SHALL be None. The typed details SHALL carry `ExpectedMaxGust` (the maximum median gust) and the models agreeing.

#### Scenario: Storm expected
- **WHEN** the maximum consensus median gust in the next 24 h is 18 m/s and the qualifying hours have an average agreement of 0.75
- **THEN** severity is Yellow and confidence is 0.75

#### Scenario: No storm
- **WHEN** no consensus median gust in the next 24 h reaches 17 m/s
- **THEN** severity is None and confidence is 0.0

### Requirement: Fog risk evaluates combined conditions
`FogRule.Evaluate(consensus, settings)` SHALL check each hour of the next 24 h on the consensus medians: fog conditions are met when `temperature_2m` − `dew_point_2m` < 2 °C AND `wind_speed_10m` < 3 m/s AND `relative_humidity_2m` > 90 %. The number of fog hours SHALL be the trigger value (`FogHours` in the typed details); confidence SHALL be the average agreement of the four parameters over the fog hours. Severity SHALL be Yellow for at least one fog hour and Orange from `FogPersistentHours` on.

#### Scenario: Fog likely
- **WHEN** the consensus medians meet all three conditions in 2 hours
- **THEN** severity is Yellow and the fog hours are 2

#### Scenario: No fog risk
- **WHEN** the consensus medians meet all three conditions in no hour
- **THEN** severity is None and confidence is 0.0

### Requirement: Snow warning evaluates snowfall accumulation
`SnowRule.Evaluate(consensus, settings)` SHALL sum the consensus median `snowfall` over the next 24 h and take the larger of that sum and the daily consensus maximum of `snowfall_sum` when it is available. Severity SHALL be Yellow for any snow, Orange above 5 cm and Red above 20 cm; confidence SHALL be the average agreement of the hours with snowfall, and severity SHALL be None when no hour has snowfall. The typed details SHALL carry `ExpectedAccumulation` (the sum) and `FreezingLevel` (median of the hourly `freezing_level_height` medians, when available).

#### Scenario: Light snow
- **WHEN** the hourly consensus snowfall over the next 24 h sums to 2 cm
- **THEN** severity is Yellow and the expected accumulation is 2.0

#### Scenario: Heavy snow
- **WHEN** the snowfall sum is 25 cm
- **THEN** severity is Red

#### Scenario: Daily snowfall sum increases severity
- **WHEN** the hourly sum is low but the daily consensus `snowfall_sum` exceeds 5 cm
- **THEN** the daily value is used, producing a higher severity

#### Scenario: Daily snowfall not available
- **WHEN** `snowfall_sum` is not in the daily consensus
- **THEN** the snow alert uses only the hourly sum

### Requirement: Pressure drop evaluates rapid pressure change
`PressureDropRule.Evaluate(consensus, settings)` SHALL compute, over the consensus median `pressure_msl` of the next 24 h, the largest drop between any two hours at least 3 hours apart. Severity SHALL be Yellow from `PressureDropThreshold` (default 5 hPa) and Orange from `PressureDropSevereThreshold` (default 10 hPa). Confidence SHALL be the average agreement over the window when the drop reaches the threshold, else 0. The typed details SHALL carry `MaxDrop`.

#### Scenario: Weather front approaching
- **WHEN** the consensus median pressure falls by 6 hPa within 3 hours
- **THEN** severity is Yellow and the max drop is 6.0

#### Scenario: Stable pressure
- **WHEN** no 3-hour drop of the consensus median reaches 5 hPa
- **THEN** severity is None and confidence is 0.0

### Requirement: Thunderstorm warning evaluates combined instability indicators
`ThunderstormRule.Evaluate(consensus, settings)` SHALL check each hour of the next 24 h on the consensus medians: thunderstorm conditions exist when `cape` > `CapeThreshold` (default 1000 J/kg) AND `precipitation` > `ThunderstormPrecipThreshold` (default 5 mm) AND `wind_gusts_10m` > `ThunderstormGustThreshold` (default 15 m/s). Confidence SHALL be the fraction of evaluated hours meeting all three conditions. Severity: None (confidence 0), Yellow (below 0.5), Orange (0.5 to 0.75), Red (above 0.75).

#### Scenario: Thunderstorm likely
- **WHEN** 20 of the 25 evaluated hours meet all three conditions
- **THEN** severity is Red and confidence is 0.8

#### Scenario: No thunderstorm risk
- **WHEN** no hour meets all three conditions
- **THEN** severity is None and confidence is 0.0

### Requirement: AlertResult aggregates all alerts for a location
`AlertAnalyzer.Compute(consensus, settings)` SHALL evaluate every registered `IAlertRule` against the `ConsensusSnapshot` and return an `AlertResult` whose location is `ConsensusSnapshot.Location` and which contains exactly 14 alerts (one per `AlertType`), or `null` when alerts are disabled.

#### Scenario: Serialization to MQTT messages
- **WHEN** alerts are serialized
- **THEN** each alert produces one MQTT message on its sub-topic

#### Scenario: None severity still publishes
- **WHEN** no threshold is exceeded for an alert type
- **THEN** a "none" severity alert is published

#### Scenario: All 14 alert types present
- **WHEN** `AlertAnalyzer.Compute` runs with alerts enabled
- **THEN** the result contains exactly 14 alerts, one for each AlertType value

### Requirement: Alert thresholds are configurable
All alert thresholds SHALL be configurable via `AlertOptions` bound from the `Njord:Enrichment:Alerts` section and converted to `AlertSettings` (with `SeverityThresholds`) for the rules. Defaults: frost [0, -5, -15] C, heat [30, 35, 40] C, storm [17, 25, 33] m/s, heavy rain hourly 10 mm / daily 25 mm, pressure drop 5 / 10 hPa, ice 2.0 C, wind chill [-10, -20, -30] C, visibility [1000, 200, 50] m, tropical night [20, 23, 25] C, humidity [16, 21, 24] C, fog persistent hours 4. An `Enabled` flag (default `true`) SHALL gate the entire alert feature.

#### Scenario: Custom frost thresholds
- **WHEN** `AlertOptions.FrostThresholds` is set to [2, 0, -10]
- **THEN** the frost rule uses those thresholds for tiered severity

#### Scenario: Alerts disabled
- **WHEN** `AlertOptions.Enabled` is `false`
- **THEN** `AlertAnalyzer.Compute` returns `null` and the pipeline emits no `AlertComputed` event

## REMOVED Requirements

### Requirement: Discovery payload for the alerts device
**Reason**: MQTT Discovery was removed and `MqttOptions` has no `DiscoveryEnabled`; no alerts discovery payload or binary_sensor components are published.
**Migration**: None; alert state is published on `{baseTopic}/{location}/alerts/{alert_type}` (requirement "Alert topics use the alerts segment").
