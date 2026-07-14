# pipeline-instrumentation Specification

## Purpose

OpenTelemetry spans and metrics for the njord pipeline: HTTP fetches, poll
scheduling, data-change detection, MQTT publishes, discovery, and connection
lifecycle. All instruments are defined centrally in `NjordTelemetry`.

## Requirements

### Requirement: HTTP fetch spans are traced
Each HTTP fetch to the Open-Meteo API SHALL create an Activity span named
`njord.fetch` with tags `location` and `model`. The span SHALL be stopped
when the fetch completes (success or failure). On failure, the span status
SHALL be set to Error.

#### Scenario: Successful fetch creates a span
- **WHEN** PipelineActor fetches forecast data for location "lucerne" and
  model "icon_d2" and the request succeeds
- **THEN** a completed `njord.fetch` span exists with tags
  `location=lucerne`, `model=icon_d2`, and status OK

#### Scenario: Failed fetch creates an error span
- **WHEN** PipelineActor fetches forecast data and the request fails
- **THEN** a completed `njord.fetch` span exists with status Error and the
  failure reason recorded

### Requirement: Fetch count is metered
Each completed fetch SHALL increment a counter `njord.fetch.total` with tags
`location`, `model`, and `status` (value `success` or `failure`).

#### Scenario: Successful fetch increments counter
- **WHEN** a fetch for model "icon_d2" at location "lucerne" succeeds
- **THEN** `njord.fetch.total` is incremented with tags `location=lucerne`,
  `model=icon_d2`, `status=success`

### Requirement: Fetch duration is metered
Each completed fetch SHALL record its duration in milliseconds to a histogram
`njord.fetch.duration` with tags `location` and `model`.

#### Scenario: Fetch duration is recorded
- **WHEN** a fetch completes in 450 ms
- **THEN** `njord.fetch.duration` records the value 450 with the
  corresponding location and model tags

### Requirement: Fetch failures are metered
Each fetch failure SHALL increment a counter `njord.fetch.failures` with tags
`location`, `model`, and `reason`.

#### Scenario: Timeout failure increments counter
- **WHEN** a fetch for model "gfs_seamless" times out
- **THEN** `njord.fetch.failures` is incremented with tag `reason=timeout`

### Requirement: Poll attempts are metered
Each scheduled poll attempt SHALL increment a counter `njord.polls.total`
with tags `location` and `model`.

#### Scenario: Scheduled poll increments counter
- **WHEN** SchedulerActor fires a ScheduledPoll for location "lucerne" and
  model "icon_d2"
- **THEN** `njord.polls.total` is incremented with the corresponding tags

### Requirement: Data changes are metered
Each data change detected by the scheduler (hash mismatch) SHALL increment a
counter `njord.data.changes` with tags `location` and `model`.

#### Scenario: Hash change increments counter
- **WHEN** SchedulerActor detects a hash change for model "ecmwf_ifs025"
- **THEN** `njord.data.changes` is incremented with the corresponding tags

### Requirement: MQTT publish spans are traced
Each MQTT publish operation SHALL create an Activity span named
`njord.mqtt.publish` with a tag `type` (value `state` or `discovery`).

#### Scenario: State publish creates a span
- **WHEN** MqttConnectionActor publishes a state message
- **THEN** a completed `njord.mqtt.publish` span exists with tag `type=state`

### Requirement: MQTT publishes are metered
Each MQTT publish SHALL increment a counter `njord.mqtt.publishes` with tag
`type` (value `state` or `discovery`).

#### Scenario: State publish increments counter
- **WHEN** a state message is published via MQTT
- **THEN** `njord.mqtt.publishes` is incremented with tag `type=state`

### Requirement: MQTT publish duration is metered
Each MQTT publish SHALL record its duration in milliseconds to a histogram
`njord.mqtt.publish.duration`.

#### Scenario: Publish duration is recorded
- **WHEN** an MQTT publish completes in 12 ms
- **THEN** `njord.mqtt.publish.duration` records the value 12

### Requirement: Discovery publishes are metered
Each batch of discovery config messages SHALL increment a counter
`njord.mqtt.discovery` by the number of config messages sent.

#### Scenario: Discovery publish increments counter
- **WHEN** DiscoveryActor publishes 432 discovery config messages
- **THEN** `njord.mqtt.discovery` is incremented by 432

### Requirement: MQTT connection state is metered
An UpDownCounter `njord.mqtt.connected` SHALL reflect the current MQTT
connection state: +1 on connect, -1 on disconnect.

#### Scenario: Connection established
- **WHEN** MqttConnectionActor connects successfully
- **THEN** `njord.mqtt.connected` is incremented by 1

#### Scenario: Connection lost
- **WHEN** MqttConnectionActor detects a disconnect
- **THEN** `njord.mqtt.connected` is decremented by 1

### Requirement: MQTT reconnects are metered
Each MQTT reconnect attempt SHALL increment a counter
`njord.mqtt.reconnects`.

#### Scenario: Reconnect increments counter
- **WHEN** MqttConnectionActor schedules a reconnect
- **THEN** `njord.mqtt.reconnects` is incremented by 1
