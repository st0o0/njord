# consumer-domain-events Specification

## Purpose
Domain-typed event model for the GrpcConsumer BroadcastHub, removing Proto types from the actor/stream layer.

## Requirements

### Requirement: Domain-typed event model for the consumer hub
The GrpcConsumer's BroadcastHub SHALL carry domain-typed events instead of Proto types.
The event hierarchy SHALL include: snapshot delivery, entity state changes, and availability
transitions. These types SHALL live in the Njord.Grpc project (consumer-internal, not in
Njord.Messages).

#### Scenario: Hub carries domain events
- **WHEN** the consumer receives a FeatureResult from the pipeline
- **THEN** it SHALL publish a domain-typed state-changed event to the hub, not a Proto message

#### Scenario: Availability transitions use domain events
- **WHEN** the consumer detects a stale entity
- **THEN** it SHALL publish a domain-typed unavailable event to the hub, not a Proto message

### Requirement: No Proto types in actor or stream internals
The GrpcConsumer actor, its state, and its BroadcastHub SHALL NOT reference any type from
the `Njord.Grpc.Proto` namespace. Proto conversion SHALL occur exclusively at the gRPC
service boundary in `NjordGrpcService`.

#### Scenario: Consumer compiles without Proto using directive
- **WHEN** the GrpcConsumer.cs file is inspected
- **THEN** it SHALL contain no `using Njord.Grpc.Proto` or `Proto.` references

### Requirement: Stream stages use only pure functions
All stream operators (Select, Where, etc.) in the consumer and gRPC service SHALL capture
only immutable local values or use pure functions. They SHALL NOT close over mutable actor
fields (`_state`, `_entityHubInput`, etc.).

#### Scenario: Snapshot built in receive handler
- **WHEN** a Subscribe request arrives
- **THEN** the consumer SHALL build the snapshot list inside the Receive handler and capture
  it in `Source.From(...)` before entering the stream pipeline

#### Scenario: Proto mapping is a pure Select stage
- **WHEN** the gRPC service maps consumer events to Proto
- **THEN** the `.Select()` stage SHALL call a pure extension method with no external state
