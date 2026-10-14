## Purpose

Debug-level element tracing across all Akka.Streams data-processing graphs using the built-in `.Log()` stage, activatable per-subsystem via Serilog configuration overrides.
## Requirements
### Requirement: Log stages use actor logger
Each `.Log()` stage in an actor-owned graph SHALL receive the actor's `ILoggingAdapter` as the explicit `log` parameter. Graphs run by gRPC services (non-actor) MAY omit the logger parameter.

#### Scenario: Actor log stage uses actor logger
- **WHEN** `MqttStatePublisher` adds a `.Log()` stage to its graph
- **THEN** the stage is called with `_log` as the third parameter: `.Log("name", extractor, _log)`

### Requirement: Log stages emit at Debug level

All `.Log()` stages SHALL emit element events at Debug level (the Akka.Streams default). No custom `LogLevels` attributes SHALL be applied to change the element log level.

#### Scenario: Log stage output invisible at Information level
- **WHEN** the minimum log level is Information
- **THEN** no `.Log()` stage element messages appear in console output

#### Scenario: Log stage output visible at Debug level
- **WHEN** the minimum log level for the owning actor's category (or a namespace above it) is Debug
- **THEN** `.Log()` stage element messages appear in console output under that actor's category, each message naming its stage

### Requirement: The MQTT state graph has log stages
The `MqttStatePublisher` graph SHALL include a `.Log()` stage on the incoming `PipelineEvent`s and one on the mapped `MqttMessage`s before `SendAsync`. The gateway fetch graph, the `ForecastPipeline` analysis graph and the gRPC streaming graphs currently have no `.Log()` stage.

#### Scenario: MQTT state graph has log stages
- **WHEN** `MqttStatePublisher` materializes its graph
- **THEN** it contains `.Log("mqtt-state-in", ...)` after the kill switch and `.Log("mqtt-state-out", ...)` before the `SendAsync` stage

### Requirement: Log stage names follow subsystem-stage kebab-case
Each `.Log()` stage name SHALL follow the pattern `{subsystem}-{stage}` in kebab-case, where the stage part describes the data at that point.

#### Scenario: MQTT log stage names
- **WHEN** inspecting the `MqttStatePublisher` graph
- **THEN** its log stage names are `mqtt-state-in` and `mqtt-state-out`

### Requirement: Log stage extractors are compact
Each `.Log()` stage SHALL provide an extractor function that returns a compact one-line summary of the element. Extractors SHALL NOT serialize full payloads, call LINQ aggregations, or perform expensive operations.

#### Scenario: Pipeline event extractor
- **WHEN** a `ForecastReceived` for location "vreden" and model "icon_d2" passes `mqtt-state-in`
- **THEN** the extracted string is `model vreden/icon_d2`

#### Scenario: MQTT message extractor
- **WHEN** an `MqttMessage` with topic "njord/vreden/icon_d2/h3" and a 512-byte payload passes `mqtt-state-out`
- **THEN** the extracted string contains the topic and the payload size (`[512B]`)

### Requirement: Stream tracing is activated through configuration
Stream log tracing SHALL be activatable by lowering the Serilog minimum level for the owning actor's log category (its full type name, e.g. `Njord.Mqtt.MqttStatePublisher`) to Debug in `appsettings*.json`, without code changes or recompilation.

#### Scenario: Enable MQTT state tracing
- **WHEN** the configuration sets the override `"Njord.Mqtt.MqttStatePublisher": "Debug"`
- **THEN** elements passing through `mqtt-state-in` and `mqtt-state-out` appear in console output

#### Scenario: Disable all tracing in production
- **WHEN** the default minimum level is Information and no Debug overrides are configured
- **THEN** zero `.Log()` stage messages appear in console output

