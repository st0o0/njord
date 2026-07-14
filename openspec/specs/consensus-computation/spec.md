# consensus-computation Specification

## Purpose

Pure statistical computations over multi-model forecast data: median, trimmed mean, spread, IQR, agreement score, outlier identification, confidence interval, and model availability matrix. All functions are static and side-effect-free — input in, result out.

## Requirements

### Requirement: Median consensus over non-null model values
`ConsensusComputer.ComputeMedian` SHALL accept an `IReadOnlyList<double?>` and return the median of the non-null values. For an even count, the median SHALL be the average of the two middle values. If fewer than 1 non-null value exists, the result SHALL be `null`.

#### Scenario: Odd number of values
- **WHEN** values are [20.0, 22.0, 21.0, null, 23.0, 19.0]
- **THEN** the median is 21.5 (sorted non-null: 19, 20, 21, 22, 23 → middle is 21.0)

#### Scenario: Even number of values
- **WHEN** values are [20.0, 22.0, 21.0, 23.0]
- **THEN** the median is 21.5 (average of 21 and 22)

#### Scenario: All null
- **WHEN** all values are null
- **THEN** the result is null

#### Scenario: Single value
- **WHEN** values are [42.0]
- **THEN** the median is 42.0

### Requirement: Trimmed mean over non-null model values
`ConsensusComputer.ComputeTrimmedMean` SHALL accept an `IReadOnlyList<double?>` and a `trimPercent` (0.0–0.5). It SHALL sort non-null values, remove the lowest and highest `trimPercent` fraction (rounding down the count to trim), and return the arithmetic mean of the remaining values. If fewer than 3 non-null values exist, it SHALL fall back to the simple mean.

#### Scenario: 10% trim on 8 values
- **WHEN** 8 non-null values are provided with `trimPercent` = 0.1
- **THEN** 0 values are trimmed from each end (floor(8 * 0.1) = 0) and the mean of all 8 is returned

#### Scenario: 20% trim on 10 values
- **WHEN** 10 non-null values are provided with `trimPercent` = 0.2
- **THEN** 2 values are trimmed from each end (floor(10 * 0.2) = 2) and the mean of the middle 6 is returned

#### Scenario: Fewer than 3 values falls back to simple mean
- **WHEN** values are [20.0, 22.0]
- **THEN** the result is 21.0 regardless of trimPercent

### Requirement: Spread across model values
`ConsensusComputer.ComputeSpread` SHALL return the difference between the maximum and minimum non-null values. If fewer than 2 non-null values exist, the result SHALL be `null`.

#### Scenario: Normal spread
- **WHEN** values are [18.0, 22.0, 20.0, null, 25.0]
- **THEN** the spread is 7.0 (25 − 18)

#### Scenario: Single value
- **WHEN** values are [20.0]
- **THEN** the spread is null

### Requirement: Interquartile range
`ConsensusComputer.ComputeIqr` SHALL return P75 − P25 of the non-null values using linear interpolation. If fewer than 4 non-null values exist, the result SHALL be `null`.

#### Scenario: 8 values
- **WHEN** 8 sorted non-null values are [18, 19, 20, 21, 22, 23, 24, 25]
- **THEN** Q1 = 19.25, Q3 = 23.75, IQR = 4.5

#### Scenario: Fewer than 4 values
- **WHEN** values are [18.0, 20.0, 22.0]
- **THEN** the IQR is null

### Requirement: Agreement score
`ConsensusComputer.ComputeAgreement` SHALL accept values, a reference value (typically the median), and a tolerance. It SHALL return the fraction (0.0–1.0) of non-null values within `tolerance` of the reference. If no non-null values exist, the result SHALL be `null`.

#### Scenario: High agreement
- **WHEN** values are [20.0, 20.5, 19.5, 20.2], median is 20.1, tolerance is 1.0
- **THEN** agreement is 1.0 (all within ±1.0)

#### Scenario: Partial agreement
- **WHEN** values are [20.0, 25.0, 19.0, 30.0], median is 22.5, tolerance is 3.0
- **THEN** agreement is 0.5 (20.0 and 25.0 are within ±3.0; 19.0 and 30.0 are not)

### Requirement: Outlier identification
`ConsensusComputer.IdentifyOutlier` SHALL accept a list of (model, value) pairs and a reference value. It SHALL return the model with the largest absolute deviation from the reference, along with the deviation value. If no values exist, the result SHALL be `null`.

#### Scenario: Clear outlier
- **WHEN** models are [(icon_d2, 20.0), (ecmwf, 21.0), (gfs, 35.0)] and reference is 21.0
- **THEN** the outlier is gfs with deviation 14.0

#### Scenario: All equal
- **WHEN** all model values are 20.0 and reference is 20.0
- **THEN** the outlier has deviation 0.0

### Requirement: Confidence interval via percentiles
`ConsensusComputer.ComputeConfidenceInterval` SHALL accept values and two percentile bounds (e.g. 10, 90). It SHALL return the values at those percentiles using linear interpolation. If fewer than 2 non-null values exist, the result SHALL be `null`.

#### Scenario: P10/P90 on 8 values
- **WHEN** 8 sorted non-null values span [18..25] and percentiles are (10, 90)
- **THEN** the lower and upper bounds are computed via linear interpolation

#### Scenario: Single value
- **WHEN** values are [20.0]
- **THEN** the confidence interval is null

### Requirement: Model availability matrix
`ConsensusComputer.BuildAvailabilityMatrix` SHALL accept a `ModelSnapshot` and a target `DateTimeOffset`. For each model in the snapshot, it SHALL report whether the model's forecast series contains a non-null value at or near that time point. The result SHALL be a dictionary of `WeatherModel → bool`.

#### Scenario: Model with data at horizon
- **WHEN** icon_d2's forecast has a point at +3h with non-null temperature
- **THEN** icon_d2 is `true` in the availability matrix for +3h

#### Scenario: Model beyond horizon
- **WHEN** icon_d2's forecast has no point at +72h (beyond its horizon)
- **THEN** icon_d2 is `false` in the availability matrix for +72h

### Requirement: ConsensusResult is a pure data record without MQTT serialization
`ConsensusResult` SHALL be a record in `Njord.Domain.Analysis` holding location, per-horizon consensus metrics (median, trimmed mean, spread, IQR, agreement, outliers, confidence interval, model availability). It SHALL NOT contain `ToMqttMessages()` or reference `MqttMessage`, `TopicScheme`, or any type from `Njord.Mqtt`. MQTT serialization of `ConsensusResult` SHALL be the responsibility of `StatePayloadBuilder` in `Njord.Mqtt`.

#### Scenario: ConsensusResult has no MQTT dependency
- **WHEN** `ConsensusResult` is instantiated
- **THEN** it contains only domain data — no MQTT types are referenced

#### Scenario: MQTT serialization lives in StatePayloadBuilder
- **WHEN** a `ConsensusResult` needs to be published via MQTT
- **THEN** `StatePayloadBuilder.FromConsensus(result, baseTopic, location)` produces the `MqttMessage` instances

### Requirement: Consensus topic scheme
The consensus pseudo-model SHALL use topic pattern `{baseTopic}/{location}/consensus/{horizon}`. The device id SHALL be `njord_{location}_consensus`. Discovery SHALL produce a device payload with the same horizons and parameters as model devices, plus additional diagnostic attributes (spread, agreement, models_used) as sensor attributes on each component.

#### Scenario: Consensus topic
- **WHEN** baseTopic is "njord", location is "lucerne", horizon is "h3"
- **THEN** the topic is "njord/lucerne/consensus/h3"

#### Scenario: Consensus device id
- **WHEN** location is "lucerne"
- **THEN** the device id is "njord_lucerne_consensus"

#### Scenario: Discovery payload includes diagnostic attributes
- **WHEN** the consensus discovery payload is built
- **THEN** each sensor component includes `spread`, `agreement`, and `models_used` as JSON attributes in the value template
