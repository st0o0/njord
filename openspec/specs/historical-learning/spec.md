# historical-learning Specification

## Purpose

Historical learning tracks forecast accuracy over time, computes model weights from inverse MAE, detects anomalies and forecast drift, identifies seasonal model preferences, and produces a weighted consensus. Results are serialized to MQTT for Home Assistant consumption.

## Requirements

### Requirement: ForecastHistoryActor persists forecast records via Akka.Persistence
The `ForecastHistoryActor` SHALL be a `ReceivePersistentActor` with PersistenceId `"forecast-history-{location}"`. It SHALL accept `RecordSnapshot` messages containing a `ModelSnapshot` and persist `ForecastRecorded` events with: timestamp, location, and consensus values. Per-model forecast values SHALL NOT be stored in the record to reduce memory footprint. On recovery, it SHALL rebuild in-memory `ForecastHistory` state from persisted events. It SHALL take a snapshot every 100 events. Records older than the retention window SHALL be excluded from analysis during recovery.

#### Scenario: Persist and recover
- **WHEN** the actor receives a `RecordSnapshot` and restarts
- **THEN** the recovered state contains the previously persisted record with consensus values

#### Scenario: Snapshot taken after 100 events
- **WHEN** 100 `ForecastRecorded` events have been persisted
- **THEN** the actor saves a snapshot of the current `ForecastHistory`

#### Scenario: Old records excluded during recovery
- **WHEN** the actor recovers and retention is 30 days
- **THEN** records older than 30 days are not included in the analysis state

#### Scenario: Records contain only consensus values
- **WHEN** a `RecordSnapshot` is processed
- **THEN** the persisted `ForecastRecord` SHALL contain consensus values and an empty model values dictionary

### Requirement: ForecastHistoryActor responds to history queries
The `ForecastHistoryActor` SHALL accept `QueryHistory` messages and respond with a `HistoryResponse` containing the current `ForecastHistory` state (all records within the retention window).

#### Scenario: Query returns current state
- **WHEN** the actor has 100 records within retention and receives a `QueryHistory`
- **THEN** it responds with a `HistoryResponse` containing 100 records

### Requirement: Model accuracy tracking via MAE
`HistoryAnalyzer.ModelAccuracy` SHALL accept a `ForecastHistory` and a `ParameterDef`. For each model, it SHALL compute MAE by comparing the model's forecast at horizon h24 against the "observed" value (consensus at h0 for the same target hour, recorded when that hour arrived). It SHALL compute rolling 7-day and 30-day MAE. If fewer than 48 matching pairs exist, the result SHALL be `null`.

#### Scenario: Consistent 2 degrees C overshoot
- **WHEN** a model's h24 forecasts consistently exceed observed by 2 degrees C over 7 days
- **THEN** the 7-day MAE for that model is approximately 2.0

#### Scenario: Insufficient history
- **WHEN** fewer than 48 forecast-observation pairs exist for a model
- **THEN** the MAE is null

### Requirement: Weighted consensus from inverse MAE
`HistoryAnalyzer.ModelWeights` SHALL accept a dictionary of model to MAE values. It SHALL compute weights as `1 / (MAE + 0.1)`, normalized to sum to 1.0. Models with null MAE SHALL receive equal default weight. `HistoryAnalyzer.WeightedConsensus` SHALL accept model values and weights and return the weighted mean.

#### Scenario: Low-error model gets higher weight
- **WHEN** model A has MAE 0.5 and model B has MAE 2.0
- **THEN** model A's weight is higher than model B's weight

#### Scenario: All models have null MAE
- **WHEN** no model has computed MAE (cold start)
- **THEN** all models receive equal weight (1/N)

### Requirement: Forecast drift as run-to-run standard deviation
`HistoryAnalyzer.ForecastDrift` SHALL accept a `ForecastHistory`, a model, and a `ParameterDef`. It SHALL collect the last N (default 5) forecasts from that model for the same target hour and compute the standard deviation. If fewer than 2 forecasts exist for the same target hour, the result SHALL be `null`.

#### Scenario: Stable model
- **WHEN** a model's last 5 forecasts for the same target hour are [20.0, 20.1, 19.9, 20.0, 20.2]
- **THEN** the drift is approximately 0.1

#### Scenario: Unstable model
- **WHEN** a model's last 5 forecasts for the same target hour are [15.0, 20.0, 18.0, 22.0, 16.0]
- **THEN** the drift is approximately 2.7

#### Scenario: Insufficient runs
- **WHEN** fewer than 2 forecasts exist for the same target hour
- **THEN** the drift is null

### Requirement: Seasonal preference identifies best model per season
`HistoryAnalyzer.SeasonalPreference` SHALL accept a `ForecastHistory`, a `ParameterDef`, and a `DateTimeOffset` (now). It SHALL determine the current season (spring 3-5, summer 6-8, autumn 9-11, winter 12-2), filter records to that season, compute 30-day MAE per model, and return the model with the lowest MAE. If no model has sufficient data, the result SHALL be `null`.

#### Scenario: Best model in summer
- **WHEN** in July with 30 days of data and model A has lowest MAE
- **THEN** the seasonal preference is model A

#### Scenario: No seasonal data
- **WHEN** no records exist for the current season
- **THEN** the result is null

### Requirement: Anomaly detection via z-score
`HistoryAnalyzer.AnomalyDetection` SHALL accept a `ForecastHistory`, a `ParameterDef`, the current consensus value, and the current hour-of-day. It SHALL compute the historical mean and standard deviation for that parameter at that hour-of-day. If the current value deviates by more than 2 sigma, it SHALL return `(true, deviationInSigma)`. If fewer than 30 records exist for that hour, the result SHALL be `null`.

#### Scenario: Normal value
- **WHEN** the current consensus is within 1 sigma of the historical mean
- **THEN** anomaly is false

#### Scenario: Anomalous value
- **WHEN** the current consensus deviates by 3 sigma from the historical mean
- **THEN** anomaly is true with deviation 3.0

#### Scenario: Insufficient history
- **WHEN** fewer than 30 records exist for the current hour-of-day
- **THEN** the result is null

### Requirement: HistoryResult aggregates all history analysis and serializes to MQTT
`HistoryResult` SHALL be a record holding the location and all history analysis values (per-model MAE, weights, drift, seasonal preference, anomaly detection, weighted consensus values). It SHALL expose `ToMqttMessages(baseTopic)` producing a single `MqttMessage` on topic `{baseTopic}/{location}/history` with a flat JSON payload.

#### Scenario: History message content
- **WHEN** ToMqttMessages is called for location "lucerne" with baseTopic "njord"
- **THEN** one message has topic `njord/lucerne/history`

#### Scenario: Cold start produces nulls
- **WHEN** insufficient history exists
- **THEN** the JSON contains null values for MAE, drift, and anomaly fields

#### Scenario: Retained message
- **WHEN** ToMqttMessages produces a message
- **THEN** the message has Retain = true
