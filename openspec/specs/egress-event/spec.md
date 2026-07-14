# egress-event Specification

## Purpose

Protocol-neutral egress layer: `EgressEvent` is the discriminated union carrying all output data from producers (ModelStateActor, EnrichmentActor) through the EgressActor's MergeHub/BroadcastHub to protocol-specific consumers (MqttEgressActor, future SignalR).

## Requirements

### Requirement: EgressEvent is a protocol-neutral discriminated union

The system SHALL define `EgressEvent` as an abstract record in `Njord.Egress` with one sealed variant per output data category. Each variant SHALL carry the domain result and location context — no protocol-specific types (no `MqttMessage`, no topic strings, no JSON payloads). The variants are:

- `PerModelUpdate(string Location, WeatherModel Model, IReadOnlyDictionary<string, string> HorizonPayloads)`
- `ConsensusUpdate(string Location, ConsensusResult Result)`
- `AlertUpdate(string Location, AlertResult Result)`
- `DerivedUpdate(string Location, DerivedResult Result)`
- `TrendUpdate(string Location, TrendResult Result)`
- `IndexUpdate(string Location, IndexResult Result)`
- `EnergyUpdate(string Location, EnergyResult Result)`
- `HistoryUpdate(string Location, HistoryResult Result)`

#### Scenario: EgressEvent carries domain data only
- **WHEN** an `EgressEvent` variant is constructed
- **THEN** it SHALL contain only domain types from `Njord.Domain` or `Njord.Egress` — no references to `Njord.Mqtt` types

#### Scenario: All output categories are representable
- **WHEN** any data producer (ModelStateActor, EnrichmentActor sub-graphs) produces output
- **THEN** the output SHALL be expressible as exactly one `EgressEvent` variant

### Requirement: EgressActor materializes a MergeHub and BroadcastHub for EgressEvent

The `EgressActor` SHALL materialize an Akka.Streams graph with a `MergeHub.Source<EgressEvent>` (collecting from all producers) connected to a `BroadcastHub.Sink<EgressEvent>` (distributing to all consumers). The actor SHALL vend `ISinkRef<EgressEvent>` to producers via `RequestEgressSink` / `EgressSinkResponse` and `ISourceRef<EgressEvent>` to consumers via `RequestEgressSource` / `EgressSourceResponse`.

#### Scenario: Producer attaches via SinkRef
- **WHEN** an actor sends `RequestEgressSink` to `EgressActor`
- **THEN** `EgressActor` SHALL respond with `EgressSinkResponse(ISinkRef<EgressEvent>)` connected to the MergeHub

#### Scenario: Consumer attaches via SourceRef
- **WHEN** an actor sends `RequestEgressSource` to `EgressActor`
- **THEN** `EgressActor` SHALL respond with `EgressSourceResponse(ISourceRef<EgressEvent>)` connected to the BroadcastHub

#### Scenario: Multiple producers and consumers operate concurrently
- **WHEN** multiple producers send `EgressEvent` instances into the MergeHub and multiple consumers subscribe via the BroadcastHub
- **THEN** every consumer SHALL receive every `EgressEvent` from every producer

### Requirement: EgressActor pre-materializes hub before vending refs

The `EgressActor` SHALL pre-materialize the MergeHub and BroadcastHub in `PreStart` so that StreamRef requests can be served immediately without waiting for producers or consumers. The MergeHub SHALL use a per-producer buffer size of 8. The BroadcastHub SHALL use a buffer size of 64.

#### Scenario: StreamRef available immediately after actor start
- **WHEN** `EgressActor` starts and another actor sends `RequestEgressSink` before any producer has attached
- **THEN** `EgressActor` SHALL respond with a valid `ISinkRef<EgressEvent>` without blocking

### Requirement: ModelStateActor produces PerModelUpdate events

The `ModelStateActor` (renamed from `MqttPublisherActor`) SHALL reside in `Njord.Egress`, subscribe to the Pipeline BroadcastHub via `ISourceRef<FetchOutcome>`, transform `FetchOutcome.Success` into `EgressEvent.PerModelUpdate`, and send them into the EgressActor's MergeHub via `ISinkRef<EgressEvent>`. It SHALL NOT reference any types from `Njord.Mqtt`.

#### Scenario: Successful fetch produces PerModelUpdate
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Success` from the pipeline
- **THEN** it SHALL compute per-horizon payloads via `HorizonProjection.BuildPerHorizon` and emit an `EgressEvent.PerModelUpdate` with the location, model, and horizon data

#### Scenario: Unchanged data is deduplicated
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Success` whose per-horizon payloads are identical to the previously emitted values for the same (location, model, horizon)
- **THEN** it SHALL NOT emit an `EgressEvent.PerModelUpdate`

#### Scenario: Fetch failures are dropped
- **WHEN** `ModelStateActor` receives a `FetchOutcome.Failure`
- **THEN** it SHALL NOT emit any `EgressEvent`
