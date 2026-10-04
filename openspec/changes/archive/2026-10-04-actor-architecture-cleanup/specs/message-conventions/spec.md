## ADDED Requirements

### Requirement: Messages targeting ShardRegion entities implement routing marker interfaces
Messages sent to ShardRegion entities SHALL implement one of the routing marker interfaces defined in `Njord.Messages`: `IWithLocation` (carries `string Location`), `IWithModelKey` (extends `IWithLocation`, carries `string ModelId`), or `IWithEnrichmentKey` (extends `IWithLocation`, carries `string TypeName`). The `NjordMessageExtractor` SHALL use these interfaces to extract entity ids for shard routing.

#### Scenario: UpdateForecast implements IWithModelKey
- **WHEN** `UpdateForecast` is sent to the ForecastSnapshot ShardRegion
- **THEN** the message implements `IWithModelKey` and `NjordMessageExtractor.EntityId` returns `"{Location}|{ModelId}"`

#### Scenario: UpdateEnrichment implements IWithEnrichmentKey
- **WHEN** `UpdateEnrichment` is sent to the EnrichmentSnapshot ShardRegion
- **THEN** the message implements `IWithEnrichmentKey` and `NjordMessageExtractor.EntityId` returns `"{Location}|{TypeName}"`

#### Scenario: QueryForecast implements IWithModelKey
- **WHEN** `QueryForecast` is sent to the ForecastSnapshot ShardRegion
- **THEN** the message implements `IWithModelKey` for correct routing

#### Scenario: Unknown message type throws ArgumentException
- **WHEN** a message without a routing marker interface is sent to `NjordMessageExtractor.EntityId`
- **THEN** an `ArgumentException` is thrown

### Requirement: Njord.Messages organizes messages by domain namespace
The `Njord.Messages` project SHALL organize messages into domain-specific namespaces: `Pipeline/` (scheduling, budget, poll state), `Egress/` (per-model updates, enrichment updates, capabilities), `Mqtt/` (connection events, inbound messages, subscribe), `Enrichment/` (history recording and queries), `Sensors/` (readings, snapshots), `Snapshots/` (forecast and enrichment queries), and `Common/` (shared types like Ack). Stream wiring messages (`RequestPipelineSink`, `RequestEgressSink`, `RequestMqttSink` and their responses) SHALL NOT appear in `Njord.Messages` — they are actor-internal records in their respective feature libraries.

#### Scenario: Mqtt namespace contains connection events
- **WHEN** searching `Njord.Messages` for MQTT-related messages
- **THEN** `MqttConnected`, `MqttDisconnected`, `SubscribeInbound`, and `MqttInboundMessage` are in `Njord.Messages.Mqtt`

#### Scenario: Enrichment namespace contains history messages
- **WHEN** searching `Njord.Messages` for enrichment history messages
- **THEN** `RecordSnapshot`, `QueryHistory`, and `QueryHistoryResponse` are in `Njord.Messages.Enrichment`

#### Scenario: No Akka.Streams types in Njord.Messages
- **WHEN** the `Njord.Messages` project is compiled
- **THEN** it has no dependency on `Akka.Streams` and contains no `ISinkRef<T>` or `ISourceRef<T>` types

#### Scenario: Stream wiring messages are actor-internal
- **WHEN** searching `Njord.Messages` for `RequestPipelineSink`, `RequestEgressSink`, or `RequestMqttSink`
- **THEN** no matches are found — these records live in the actor's own feature library

## MODIFIED Requirements

### Requirement: CLAUDE.md message pattern documentation
CLAUDE.md SHALL document the message organisation accurately. Messages that cross domain boundaries live in the shared `Njord.Messages` project. Stream wiring messages (`Request*Sink/Source` and responses) are actor-internal in each feature library.

#### Scenario: Accurate documentation
- **WHEN** reading the CLAUDE.md message convention section
- **THEN** it states that cross-domain messages are in `Njord.Messages` and stream wiring messages are actor-internal
