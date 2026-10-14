## ADDED Requirements

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
Stream log tracing SHALL be activatable by lowering the Serilog minimum level for the owning actor's log source to Debug in `appsettings*.json`, without code changes or recompilation.

#### Scenario: Enable MQTT state tracing
- **WHEN** the configuration lowers the minimum level for the `MqttStatePublisher` log source to Debug
- **THEN** elements passing through `mqtt-state-in` and `mqtt-state-out` appear in console output

#### Scenario: Disable all tracing in production
- **WHEN** the default minimum level is Information and no Debug overrides are configured
- **THEN** zero `.Log()` stage messages appear in console output

## MODIFIED Requirements

### Requirement: Log stages use actor logger
Each `.Log()` stage in an actor-owned graph SHALL receive the actor's `ILoggingAdapter` as the explicit `log` parameter. Graphs run by gRPC services (non-actor) MAY omit the logger parameter.

#### Scenario: Actor log stage uses actor logger
- **WHEN** `MqttStatePublisher` adds a `.Log()` stage to its graph
- **THEN** the stage is called with `_log` as the third parameter: `.Log("name", extractor, _log)`

## REMOVED Requirements

### Requirement: Log stages on all data-processing stream graphs
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "The MQTT state graph has log stages" in this capability.

### Requirement: Log stage naming convention
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Log stage names follow subsystem-stage kebab-case" in this capability.

### Requirement: Compact extractor functions
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Log stage extractors are compact" in this capability.

### Requirement: Activation via Serilog namespace override
**Reason**: The requirement named types, files or behavior that no longer exist; it is restated against the current code.
**Migration**: Superseded by "Stream tracing is activated through configuration" in this capability.
