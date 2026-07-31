## Purpose

Debug-level element tracing across all Akka.Streams data-processing graphs using the built-in `.Log()` stage, activatable per-subsystem via Serilog configuration overrides.

## Requirements

### Requirement: Log stages on all data-processing stream graphs

Every data-processing Akka.Streams graph in njord SHALL include at least one `.Log()` stage at a semantic boundary. Infrastructure graphs (StreamRef vending) are excluded.

#### Scenario: Pipeline fetch graph has log stages
- **WHEN** `PipelineActor` materializes the fetch pipeline
- **THEN** the graph contains `.Log()` stages before and after the fetch operation

#### Scenario: Enrichment graph has log stages
- **WHEN** `EnrichmentActor` materializes the enrichment graph
- **THEN** the graph contains `.Log()` stages after the scan source and after the enrichment flow

#### Scenario: MQTT send graph has log stage
- **WHEN** `MqttConnectionActor` materializes the MQTT publish pipeline
- **THEN** the graph contains a `.Log()` stage before the `SendAsync` operation

#### Scenario: gRPC streaming graphs have log stages
- **WHEN** `WeatherGrpcService` materializes a `StreamForecasts` or `StreamEnrichments` graph
- **THEN** each graph contains a `.Log()` stage after the location filter

### Requirement: Log stage naming convention

Each `.Log()` stage name SHALL follow the pattern `{subsystem}-{stage}` in kebab-case. Subsystem values SHALL be one of: `pipeline`, `egress`, `enrichment`, `mqtt`, `discovery`, `grpc`. Stage names SHALL describe the data at that point (e.g., `fetch-in`, `fetch-out`, `send`, `snapshot`).

#### Scenario: Pipeline log stage names
- **WHEN** inspecting the `PipelineActor` stream graph
- **THEN** log stage names are `pipeline-fetch-in`, `pipeline-fetch-out`, and `pipeline-hash`

#### Scenario: MQTT log stage names
- **WHEN** inspecting MQTT actor stream graphs
- **THEN** log stage names include `mqtt-egress-in`, `mqtt-egress-out`, and `mqtt-send`

### Requirement: Compact extractor functions

Each `.Log()` stage SHALL provide an extractor function that returns a compact one-line summary of the element. Extractors SHALL NOT serialize full payloads, call LINQ aggregations, or perform expensive operations.

#### Scenario: Fetch outcome extractor
- **WHEN** a `FetchOutcome.Success` for location "vreden" model "icon_d2" passes through a `.Log()` stage
- **THEN** the extracted string contains `"vreden/icon_d2"`

#### Scenario: MQTT message extractor
- **WHEN** an `MqttMessage` with topic "njord/vreden/icon_d2/3h" and 512-byte payload passes through a `.Log()` stage
- **THEN** the extracted string contains the topic and payload size

### Requirement: Log stages use actor logger

Each `.Log()` stage in an actor-owned graph SHALL receive the actor's `ILoggingAdapter` as the explicit `log` parameter. `WeatherGrpcService` graphs (non-actor) MAY omit the logger parameter.

#### Scenario: Actor log stage uses actor logger
- **WHEN** `PipelineActor` adds a `.Log()` stage to its graph
- **THEN** the stage is called with `_log` as the third parameter: `.Log("name", extractor, _log)`

### Requirement: Log stages emit at Debug level

All `.Log()` stages SHALL emit element events at Debug level (the Akka.Streams default). No custom `LogLevels` attributes SHALL be applied to change the element log level.

#### Scenario: Log stage output invisible at Information level
- **WHEN** the minimum log level is Information
- **THEN** no `.Log()` stage element messages appear in console output

#### Scenario: Log stage output visible at Debug level
- **WHEN** the minimum log level for the relevant namespace is Debug
- **THEN** `.Log()` stage element messages appear in console output with the stage name as context

### Requirement: Activation via Serilog namespace override

Stream log tracing for a specific subsystem SHALL be activatable by adding a Serilog `MinimumLevel.Override` entry for the subsystem namespace or stage name in `appsettings*.json`, without code changes or recompilation.

#### Scenario: Enable pipeline tracing
- **WHEN** `appsettings.json` contains `"MinimumLevel": { "Override": { "pipeline-fetch-in": "Debug" } }`
- **THEN** elements passing through the `pipeline-fetch-in` stage appear in console output

#### Scenario: Disable all tracing in production
- **WHEN** the default minimum level is Information and no Debug overrides are configured
- **THEN** zero `.Log()` stage messages appear in console output
