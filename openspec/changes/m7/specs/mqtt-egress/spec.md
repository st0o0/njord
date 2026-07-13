## ADDED Requirements

### Requirement: TopicScheme provides history topic helpers
`TopicScheme` SHALL expose `HistoryDeviceId(string location)` returning `njord_{slug(location)}_history` and `HistoryTopic(string baseTopic, string location)` returning `{baseTopic}/{slug(location)}/history`.

#### Scenario: History device id
- **WHEN** location is "lucerne"
- **THEN** `HistoryDeviceId` returns "njord_lucerne_history"

#### Scenario: History topic
- **WHEN** baseTopic is "njord", location is "lucerne"
- **THEN** `HistoryTopic` returns "njord/lucerne/history"

### Requirement: DiscoveryPayloadBuilder builds a history device
`DiscoveryPayloadBuilder.BuildHistory` SHALL produce a device-based discovery payload for location with device id `njord_{location}_history`, model `history`. Sensors SHALL include: per-model MAE sensors (numeric), per-model weight sensors (numeric), per-model drift sensors (numeric), seasonal best-model sensor (text), anomaly sensor (binary_sensor), anomaly deviation sensor (numeric), and weighted consensus sensors (numeric per parameter).

#### Scenario: History device payload structure
- **WHEN** `BuildHistory` is called for location "lucerne" with models ["icon_d2", "ecmwf_ifs025"]
- **THEN** the payload contains device id "njord_lucerne_history", model "history"

#### Scenario: Per-model sensors
- **WHEN** models are ["icon_d2", "ecmwf_ifs025"]
- **THEN** sensors exist for mae_7d_icon_d2, mae_30d_icon_d2, weight_icon_d2, drift_icon_d2, and similarly for ecmwf_ifs025

#### Scenario: Anomaly is binary sensor
- **WHEN** the history device is built
- **THEN** an anomaly sensor exists with platform "binary_sensor"
