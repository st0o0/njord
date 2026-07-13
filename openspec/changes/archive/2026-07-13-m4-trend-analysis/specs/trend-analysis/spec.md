## ADDED Requirements

### Requirement: Trend direction compares consensus median between snapshots
`TrendAnalyzer.TrendDirection` SHALL accept a `double?` previous median, a `double?` current median, and a `double` dead-band threshold. It SHALL return a `(string Direction, double Delta)?`. Direction SHALL be "rising" when `current − previous > threshold`, "falling" when `previous − current > threshold`, and "stable" otherwise. Delta SHALL be `current − previous`. If either input is `null`, the result SHALL be `null`.

#### Scenario: Rising trend
- **WHEN** previous median is 18.0, current is 22.0, threshold is 0.5
- **THEN** direction is "rising" and delta is 4.0

#### Scenario: Falling trend
- **WHEN** previous median is 22.0, current is 18.0, threshold is 0.5
- **THEN** direction is "falling" and delta is -4.0

#### Scenario: Stable within dead-band
- **WHEN** previous median is 20.0, current is 20.3, threshold is 0.5
- **THEN** direction is "stable" and delta is 0.3

#### Scenario: Null previous
- **WHEN** previous median is null
- **THEN** the result is null

#### Scenario: Null current
- **WHEN** current median is null
- **THEN** the result is null

### Requirement: Weather-change detection compares WMO code categories
`TrendAnalyzer.WeatherChange` SHALL accept a `int?` previous WMO code and a `int?` current WMO code. It SHALL classify each code into a category: clear (0–3), fog (45–48), drizzle (51–57), rain (61–67), snow (71–77), showers (80–86), thunderstorm (95–99). If the category changed, it SHALL return a `WeatherChangeResult` with `FromCategory`, `ToCategory`, and `Description` (e.g. "clear → rain"). If the category did not change or either code is `null`, the result SHALL be `null`.

#### Scenario: Clear to rain
- **WHEN** previous WMO code is 1 (mainly clear) and current is 63 (moderate rain)
- **THEN** the result has FromCategory "clear", ToCategory "rain", Description "clear → rain"

#### Scenario: Same category
- **WHEN** previous WMO code is 61 (slight rain) and current is 65 (heavy rain)
- **THEN** the result is null (both are "rain")

#### Scenario: Null codes
- **WHEN** either previous or current WMO code is null
- **THEN** the result is null

### Requirement: Precipitation timing finds start and end of precipitation
`TrendAnalyzer.PrecipitationTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for precipitation, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours for points where precipitation > 0. It SHALL return `(int? StartsInHours, int? EndsInHours)` — the hours-from-now to the first and last non-zero precipitation point. If no precipitation is found, both SHALL be `null`.

#### Scenario: Rain starting in 3 hours ending in 8 hours
- **WHEN** the series has precipitation > 0 from T0+3h through T0+8h
- **THEN** StartsInHours is 3 and EndsInHours is 8

#### Scenario: No precipitation
- **WHEN** all precipitation values are 0 or null in the next 24h
- **THEN** both StartsInHours and EndsInHours are null

#### Scenario: Continuous precipitation from now
- **WHEN** precipitation is > 0 from T0+0h through T0+12h
- **THEN** StartsInHours is 0 and EndsInHours is 12

### Requirement: Extrema timing finds hour of max and min temperature
`TrendAnalyzer.ExtremaTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for temperature, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours and return `(int? MaxInHours, int? MinInHours)` — the hours-from-now to the maximum and minimum temperature. If fewer than 2 non-null temperature points exist, the result SHALL be `(null, null)`.

#### Scenario: Peak at midday, low at dawn
- **WHEN** the series has max temperature at T0+6h and min at T0+18h
- **THEN** MaxInHours is 6 and MinInHours is 18

#### Scenario: Insufficient data
- **WHEN** the series has fewer than 2 non-null temperature values in the next 24h
- **THEN** both are null

### Requirement: Consensus stability compares IQR between snapshots
`TrendAnalyzer.ConsensusStability` SHALL accept `double?` previous IQR and `double?` current IQR. It SHALL return `(string Label, double Ratio)?`. The ratio is `current / previous`. Label SHALL be "converging" when ratio < 0.8, "diverging" when ratio > 1.2, and "stable" otherwise. If either IQR is `null` or previous is 0, the result SHALL be `null`.

#### Scenario: Converging models
- **WHEN** previous IQR is 5.0 and current IQR is 3.0
- **THEN** label is "converging" and ratio is 0.6

#### Scenario: Diverging models
- **WHEN** previous IQR is 3.0 and current IQR is 5.0
- **THEN** label is "diverging" and ratio is approximately 1.67

#### Scenario: Stable
- **WHEN** previous IQR is 4.0 and current IQR is 4.2
- **THEN** label is "stable" and ratio is 1.05

#### Scenario: Null IQR
- **WHEN** either IQR is null
- **THEN** the result is null

#### Scenario: Zero previous IQR
- **WHEN** previous IQR is 0.0
- **THEN** the result is null (division by zero avoided)

### Requirement: Predictability decay measures spread growth across horizons
`TrendAnalyzer.PredictabilityDecay` SHALL accept an `IReadOnlyList<(int HorizonHours, double? Spread)>` of consensus spreads at each horizon. It SHALL compute a linear regression slope of spread vs horizon hours, ignoring null spreads. It SHALL return `(double DecayRate, int? ReliableHours)?`. `DecayRate` is the slope (spread increase per hour). `ReliableHours` is the first horizon where spread exceeds a threshold (default 3.0 °C), or `null` if it never does. If fewer than 2 non-null data points exist, the result SHALL be `null`.

#### Scenario: Gradual decay
- **WHEN** spreads are [(3, 1.0), (6, 1.5), (12, 2.5), (24, 4.0), (48, 7.0), (72, 10.0)]
- **THEN** DecayRate is positive and ReliableHours is 24 (first horizon where spread > 3.0)

#### Scenario: Flat spread
- **WHEN** spreads are [(3, 2.0), (6, 2.1), (12, 2.0), (24, 2.2)]
- **THEN** DecayRate is near 0 and ReliableHours is null (spread never exceeds 3.0)

#### Scenario: Insufficient data
- **WHEN** only one horizon has a non-null spread
- **THEN** the result is null

### Requirement: TrendResult aggregates all trend analysis and serializes to MQTT
`TrendResult` SHALL be a record holding the location and all trend analysis results. It SHALL expose a static `Compute` method taking current and previous `ModelSnapshot`, location, horizons, parameter set, and `TimeProvider`. It SHALL expose `ToMqttMessages(baseTopic)` producing a single `MqttMessage` on topic `{baseTopic}/{location}/trends` with a flat JSON payload containing all trend data.

#### Scenario: Trend message content
- **WHEN** ToMqttMessages is called for location "lucerne" with baseTopic "njord"
- **THEN** one message has topic `njord/lucerne/trends` with JSON keys for trend directions, weather change, precipitation timing, extrema timing, stability, and decay

#### Scenario: No previous snapshot
- **WHEN** Compute is called with previous snapshot null
- **THEN** the result has all fields null and ToMqttMessages produces a JSON with null values

#### Scenario: Retained message
- **WHEN** ToMqttMessages produces a message
- **THEN** the message has Retain = true
