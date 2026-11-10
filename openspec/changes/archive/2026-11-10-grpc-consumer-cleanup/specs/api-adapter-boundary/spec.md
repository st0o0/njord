## MODIFIED Requirements

### Requirement: gRPC responses are unchanged by the boundary refactor
For the same system state, every RPC SHALL return the same response as before this change,
including error status codes and messages. The internal event model change (ConsumerEvent
replacing Proto.EntityEvent in the hub) SHALL NOT affect wire-format behavior.

#### Scenario: Unknown location keeps its status code
- **WHEN** a client calls `Subscribe` for an unconfigured location
- **THEN** the call fails with the same status code and message as before

#### Scenario: Subscribe snapshot identical
- **WHEN** a client subscribes and the same set of features have produced results
- **THEN** the snapshot SHALL contain the same EntityStateUpdate messages with the same
  values as before this change, plus default-valued entries for entities that previously
  had no state

### Requirement: Proto mapping at the service boundary
The `NjordGrpcService` SHALL be the exclusive location where domain types are converted to
Proto types for the Subscribe stream. The GrpcConsumer actor SHALL NOT import or construct
Proto types. The mapping SHALL be a pure function applied in a `.Select()` stream stage.

#### Scenario: Subscribe stream maps at boundary
- **WHEN** a client calls Subscribe
- **THEN** the gRPC service SHALL receive a SourceRef of domain events and map each to
  Proto.EntityEvent before writing to the response stream

### Requirement: Actor unavailability maps to a gRPC status
When the target actor cannot be resolved or does not answer within the ask timeout, the RPC
SHALL fail with `Unavailable` (resolution) or `DeadlineExceeded` (timeout), and SHALL NOT
leak an unhandled exception.

#### Scenario: Read model not ready
- **WHEN** a unary RPC is called before the consumer actor is resolvable
- **THEN** the call fails with status `Unavailable`
