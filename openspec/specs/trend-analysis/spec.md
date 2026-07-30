# trend-analysis Specification

## Purpose

Trend analysis compares consecutive poll snapshots to detect directional changes in weather parameters, weather-code category transitions, precipitation and extrema timing, model consensus stability, and predictability decay across horizons. Results are aggregated into a TrendResult and published as a single retained MQTT message per location.

## Requirements

### Requirement: Trend direction compares consensus median between snapshots

Trend analysis SHALL accept `ConsensusSnapshot` and `ConsensusSnapshot?` previous instead of `ModelSnapshot`. Trend direction compares consensus medians at the same horizon between two consecutive snapshots.

#### Scenario: Rising trend
- **WHEN** current consensus median temperature at h3 is 22 degrees C and previous was 19 degrees C
- **THEN** the trend direction is "rising" with delta 3.0

#### Scenario: Falling trend
- **WHEN** current consensus median temperature at h3 is 15 degrees C and previous was 19 degrees C
- **THEN** the trend direction is "falling" with delta -4.0

#### Scenario: Stable within dead-band
- **WHEN** the delta between current and previous consensus median is within the dead-band
- **THEN** the trend direction is "stable"

#### Scenario: Null previous
- **WHEN** `previous` is null
- **THEN** no trend events are produced

#### Scenario: Null current
- **WHEN** the current consensus median is null at a horizon
- **THEN** that horizon's trend is null

### Requirement: Weather-change detection compares WMO code categories
`TrendAnalyzer.WeatherChange` SHALL accept a `int?` previous WMO code and a `int?` current WMO code. It SHALL classify each code into a category: clear (0-3), fog (45-48), drizzle (51-57), rain (61-67), snow (71-77), showers (80-86), thunderstorm (95-99). If the category changed, it SHALL return a `WeatherChangeResult` with `FromCategory`, `ToCategory`, and `Description` (e.g. "clear -> rain"). If the category did not change or either code is `null`, the result SHALL be `null`.

#### Scenario: Clear to rain
- **WHEN** previous WMO code is 1 (mainly clear) and current is 63 (moderate rain)
- **THEN** the result has FromCategory "clear", ToCategory "rain", Description "clear -> rain"

#### Scenario: Same category
- **WHEN** previous WMO code is 61 (slight rain) and current is 65 (heavy rain)
- **THEN** the result is null (both are "rain")

#### Scenario: Null codes
- **WHEN** either previous or current WMO code is null
- **THEN** the result is null

### Requirement: Precipitation timing finds start and end of precipitation
`TrendAnalyzer.PrecipitationTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for precipitation, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours for points where precipitation > 0. It SHALL return `(int? StartsInHours, int? EndsInHours)` -- the hours-from-now to the first and last non-zero precipitation point. If no precipitation is found, both SHALL be `null`.

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
`TrendAnalyzer.ExtremaTiming` SHALL accept a `ForecastSeries`, a `ParameterDef` for temperature, and a `DateTimeOffset` (now). It SHALL scan the next 24 hours and return `(int? MaxInHours, int? MinInHours)` -- the hours-from-now to the maximum and minimum temperature. If fewer than 2 non-null temperature points exist, the result SHALL be `(null, null)`.

#### Scenario: Peak at midday, low at dawn
- **WHEN** the series has max temperature at T0+6h and min at T0+18h
- **THEN** MaxInHours is 6 and MinInHours is 18

#### Scenario: Insufficient data
- **WHEN** the series has fewer than 2 non-null temperature values in the next 24h
- **THEN** both are null

### Requirement: Consensus stability compares IQR between snapshots

Stability SHALL compare IQR values from `ConsensusSnapshot.Hourly` between current and previous.

#### Scenario: Converging models
- **WHEN** current IQR is smaller than previous IQR
- **THEN** stability label is "converging"

#### Scenario: Diverging models
- **WHEN** current IQR is larger than previous IQR
- **THEN** stability label is "diverging"

#### Scenario: Stable
- **WHEN** IQR ratio is within tolerance
- **THEN** stability label is "stable"

### Requirement: Predictability decay measures spread growth across horizons

Decay SHALL use spread values from `ConsensusSnapshot.Hourly.Parameters`.

#### Scenario: Gradual decay
- **WHEN** spread increases from h0 to h24
- **THEN** a positive decay rate is computed

#### Scenario: Flat spread
- **WHEN** spread is constant across horizons
- **THEN** decay rate is near zero

#### Scenario: Insufficient data
- **WHEN** fewer than 2 horizons have spread values
- **THEN** decay is null

### Requirement: TrendResult aggregates all trend analysis and serializes to MQTT

`TrendResult` SHALL be computed from `ConsensusSnapshot` pairs. Location comes from `ConsensusSnapshot.Location`.

#### Scenario: Trend message content
- **WHEN** trends are serialized to MQTT
- **THEN** one retained message is published with direction, timing, stability, and decay

#### Scenario: No previous snapshot
- **WHEN** previous `ConsensusSnapshot` is null
- **THEN** no trend message is emitted

#### Scenario: Retained message
- **WHEN** a trend message is published
- **THEN** it is retained

### Requirement: TrendResult serialization with pinned wire names
`TrendResult`, `ParameterTrend`, and `WeatherChangeResult` records SHALL have `[property: JsonProperty("...")]` on all positional parameters. Value tuple properties (`PrecipTiming`, `ExtremaTiming`, `Stability`, `Decay`) SHALL be replaced with named records (`PrecipTimingInfo`, `ExtremaTimingInfo`, `StabilityInfo`, `DecayInfo`) carrying `[JsonProperty]` attributes.

#### Scenario: TrendResult with all fields round-trips through JSON
- **WHEN** a `TrendResult` with parameter trends, weather change, and all timing/stability/decay fields is serialized and deserialized
- **THEN** all properties round-trip correctly with camelCase wire names, including the replaced tuple fields using named record types
