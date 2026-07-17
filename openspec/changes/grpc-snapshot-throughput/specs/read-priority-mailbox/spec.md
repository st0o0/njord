## ADDED Requirements

### Requirement: GrpcSnapshotConsumerActor uses Tell for snapshot updates
The `GrpcSnapshotConsumerActor` stream graph SHALL use `Tell` (fire-and-forget) instead of `Ask<Ack>` when forwarding `EgressEvent.PerModelUpdate` and `EgressEvent.EnrichmentUpdate` to the snapshot actors. The stream SHALL NOT wait for acknowledgement from the snapshot actors.

#### Scenario: Stream processes events without blocking
- **WHEN** 28 `PerModelUpdate` events arrive from the BroadcastHub in a burst
- **THEN** the stream SHALL forward all 28 via Tell without waiting for responses
- **AND** a concurrent gRPC `GetForecast` Ask SHALL reach the `ForecastSnapshotActor` mailbox immediately

#### Scenario: Stream uses synchronous Select
- **WHEN** the stream graph is materialized
- **THEN** it SHALL use `Select` (not `SelectAsync`) since no async operation is needed

### Requirement: Snapshot actors do not reply to updates
`ForecastSnapshotActor` and `EnrichmentSnapshotActor` SHALL NOT send `Ack` replies for `UpdateForecast` and `UpdateEnrichment` messages. The update handlers SHALL process the state mutation and optional `SaveSnapshot` without replying to `Sender`.

#### Scenario: UpdateForecast is fire-and-forget
- **WHEN** `ForecastSnapshotActor` receives an `UpdateForecast` message
- **THEN** it SHALL update `_state` and optionally call `SaveSnapshot`
- **AND** it SHALL NOT call `Sender.Tell`

#### Scenario: UpdateEnrichment is fire-and-forget
- **WHEN** `EnrichmentSnapshotActor` receives an `UpdateEnrichment` message
- **THEN** it SHALL update `_state` and optionally call `SaveSnapshot`
- **AND** it SHALL NOT call `Sender.Tell`

### Requirement: Read-priority mailbox for snapshot actors
`ForecastSnapshotActor` and `EnrichmentSnapshotActor` SHALL use a custom `UnboundedStablePriorityMailbox` that processes read messages before write messages. Within the same priority level, FIFO ordering SHALL be preserved.

#### Scenario: Read messages have higher priority
- **WHEN** the mailbox contains `UpdateForecast` messages AND a `GetForecast` message arrives
- **THEN** `GetForecast` SHALL be dequeued before any pending `UpdateForecast` messages

#### Scenario: Priority assignment
- **WHEN** the mailbox receives messages
- **THEN** `GetForecast`, `GetAllForecasts`, `GetEnrichment`, `GetAllEnrichments` SHALL have priority 0
- **AND** `UpdateForecast`, `UpdateEnrichment` SHALL have priority 1
- **AND** all other messages (including `SaveSnapshotSuccess`, `SaveSnapshotFailure`, etc.) SHALL have priority 2

#### Scenario: FIFO within same priority
- **WHEN** two `GetForecast` messages are in the mailbox
- **THEN** they SHALL be dequeued in arrival order

### Requirement: Ack message removed
The `Ack` record SHALL be removed from the gRPC messages. No actor SHALL reference or produce `Ack` messages.

#### Scenario: No Ack references in codebase
- **WHEN** the change is complete
- **THEN** grep for `Ack` in `src/Njord/Grpc/` SHALL return no results (excluding test files)
