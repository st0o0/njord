## MODIFIED Requirements

### Requirement: Consumers attach via SourceRef to pipeline BroadcastHub

Each consumer SHALL request a `SourceRef<PipelineEvent>` from the forecast pipeline and run its own sink. There is exactly one hop from pipeline to consumer: no intermediate relay actors, no intermediate BroadcastHubs between the pipeline and a consumer.

#### Scenario: MQTT consumer attaches
- **WHEN** the MQTT state publisher starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from the forecast pipeline and
  materializes its own sink that publishes state to MQTT

#### Scenario: gRPC consumer attaches
- **WHEN** `ForecastReadModel` starts
- **THEN** it requests a `SourceRef<PipelineEvent>` from the forecast pipeline and
  materializes its own sink that updates in-memory state and feeds its
  secondary BroadcastHub

#### Scenario: Consumer independence
- **WHEN** any consumer project is inspected
- **THEN** it does not reference other consumer projects; consumers are
  independent output adapters

### Requirement: GrpcConsumerActor serves unary and streaming RPCs

`ForecastReadModel`, an actor in `Njord.Pipeline` resolved by the `IForecastReadModel` marker, SHALL hold the latest pipeline results in memory per location for unary queries, and SHALL provide a secondary `BroadcastHub<PipelineEvent>` for streaming RPCs. The gRPC API project SHALL NOT contain this or any other actor.

#### Scenario: Unary query
- **WHEN** a gRPC client calls `GetEnrichments` for a location
- **THEN** the gRPC service asks `ForecastReadModel` and maps its typed
  analysis results; no persistence, no shard region

#### Scenario: Unary query after restart
- **WHEN** `ForecastReadModel` restarts and a client queries before the first
  poll cycle completes
- **THEN** the response indicates no data is available yet

#### Scenario: Streaming RPC
- **WHEN** a gRPC client calls `StreamForecasts` or `StreamEnrichments`
- **THEN** the gRPC service requests a `SourceRef` from `ForecastReadModel`'s
  secondary BroadcastHub and streams filtered `PipelineEvent` instances

#### Scenario: Dead gRPC streaming client
- **WHEN** a gRPC streaming client disconnects without completing
- **THEN** the secondary BroadcastHub's IdleTimeout detects and cleans up the
  dead subscription without affecting other streaming clients
