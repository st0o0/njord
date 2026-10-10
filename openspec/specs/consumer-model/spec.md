# consumer-model Specification

## Purpose
TBD - created by archiving change restructure-enrichment-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Consumers attach via SourceRef to pipeline BroadcastHub

Each consumer SHALL request a `SourceRef<PipelineEvent>` from `PipelineActor`
and run its own sink. There is exactly one hop from pipeline to consumer — no
intermediate relay actors, no intermediate BroadcastHubs.

#### Scenario: MQTT consumer attaches
- **WHEN** `MqttConsumerActor` starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from `PipelineActor` and
  materializes its own sink that publishes state to MQTT

#### Scenario: gRPC consumer attaches
- **WHEN** `GrpcConsumerActor` starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from `PipelineActor` and
  materializes its own sink that updates in-memory state and feeds its
  secondary BroadcastHub

#### Scenario: Consumer independence
- **WHEN** any consumer project is inspected
- **THEN** it does not reference other consumer projects — consumers are
  independent output adapters

### Requirement: Consumers are output adapters for typed results

Each consumer SHALL receive typed `PipelineEvent` instances and translate them
into its protocol-specific output format. Consumers SHALL NOT define
presentation abstractions (no `IEnrichmentPresenter`, no `TypeName` matching).

#### Scenario: MQTT consumer receives alert results
- **WHEN** the pipeline produces a `PipelineEvent.AnalysisResult` with alert data
- **THEN** the MQTT consumer translates it to a state JSON payload and publishes
  it to the appropriate MQTT topic — no presenter indirection

#### Scenario: gRPC consumer receives analysis results
- **WHEN** a `PipelineEvent.AnalysisResult` arrives at `GrpcConsumerActor`
- **THEN** it updates its in-memory state for the corresponding location

### Requirement: MQTT consumer publishes state only

The MQTT consumer SHALL publish state messages to MQTT topics. It SHALL NOT
manage MQTT Discovery lifecycle (no retained config payloads, no HA birth-message
handling, no tombstoning, no `DiscoveryPayloadBuilder`).

#### Scenario: State publishing
- **WHEN** a poll cycle completes with analysis results
- **THEN** the MQTT consumer publishes state JSON to per-device state topics

#### Scenario: No discovery payloads
- **WHEN** the MQTT consumer starts or receives a HA birth message
- **THEN** it does NOT publish any retained config payloads to
  `homeassistant/device/*/config` topics

### Requirement: GrpcConsumerActor serves unary and streaming RPCs

`GrpcConsumerActor` SHALL hold the latest pipeline results in-memory
(`Dictionary<Location, LatestResults>`) for unary query responses, and SHALL
provide a secondary `BroadcastHub<PipelineEvent>` for gRPC streaming RPCs.

#### Scenario: Unary query
- **WHEN** a gRPC client calls `GetEnrichments` for a location
- **THEN** `GrpcConsumerActor` responds from its in-memory state with typed
  analysis results — no persistence, no shard region

#### Scenario: Unary query after restart
- **WHEN** `GrpcConsumerActor` restarts and a client queries before the first
  poll cycle completes
- **THEN** the response indicates no data is available yet

#### Scenario: Streaming RPC
- **WHEN** a gRPC client calls `StreamForecasts` or `StreamEnrichments`
- **THEN** the gRPC service requests a `SourceRef` from `GrpcConsumerActor`'s
  secondary BroadcastHub and streams filtered `PipelineEvent` instances

#### Scenario: Dead gRPC streaming client
- **WHEN** a gRPC streaming client disconnects without completing
- **THEN** the secondary BroadcastHub's IdleTimeout detects and cleans up the
  dead subscription without affecting other streaming clients

### Requirement: EnrichmentSnapshotActor shard region is removed

The `EnrichmentSnapshotActor` shard region, its persistence DTOs
(`EnrichmentSnapshotDto`, `EnrichmentEntryDto`), and all untyped enrichment
messages (`UpdateEnrichment`, `QueryEnrichment`, `QueryEnrichmentCompleted`,
`QueryAllEnrichmentsCompleted`, `IWithEnrichmentKey`) SHALL be removed.

#### Scenario: No enrichment shard region
- **WHEN** the actor system starts
- **THEN** no `EnrichmentSnapshotActor` shard region is registered

#### Scenario: No untyped messages
- **WHEN** the codebase is searched for `IWithEnrichmentKey` or `UpdateEnrichment`
- **THEN** no results are found

### Requirement: Consumer registration via DI

Each consumer SHALL expose an `IServiceCollection` extension method
(e.g. `AddMqttConsumer()`, `AddGrpcConsumer()`) for registration. The Host
calls these; each consumer owns its internal wiring.

#### Scenario: Enabling MQTT consumer
- **WHEN** the Host calls `services.AddMqttConsumer()`
- **THEN** the MQTT consumer actor and supporting services are registered

