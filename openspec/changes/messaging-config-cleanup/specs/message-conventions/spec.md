## MODIFIED Requirements

### Requirement: Message location rule
Every public record or class that is sent between actors (via Tell, Ask, or shard routing) or through Akka.Streams boundaries (via SinkRef, SourceRef, or hub materialization) SHALL be defined in the Njord.Messages project. Stream handshake messages carrying ISinkRef<T> or ISourceRef<T> are included.

#### Scenario: Stream handshakes are in Njord.Messages
- **WHEN** the ForecastPipeline replies to a sink request with an ISinkRef
- **THEN** the RequestForecastSinkCompleted record is defined in Njord.Messages.Stream

#### Scenario: Stream payloads are in Njord.Messages
- **WHEN** the gateway emits DomainDataReceived through the pipeline
- **THEN** DomainDataReceived is defined in Njord.Messages.Stream

## REMOVED Requirements

### Requirement: Dead message types
**Reason**: Never constructed or handled in production code.
**Migration**: Use QueryBudgetStatus instead of QueryBudgetUsage. Remove catch clauses for the deleted Failed variants.

#### Scenario: QueryBudgetUsage hierarchy is removed
- **WHEN** a consumer needs budget information
- **THEN** it uses QueryBudgetStatus / BudgetStatusResult (which is a superset)

#### Scenario: Unused Failed variants are removed
- **WHEN** BudgetStatusFailed, BudgetUsageFailed, or PollStatesFailed is referenced
- **THEN** compilation fails (types deleted)

### Requirement: Unused wire-name extensions
**Reason**: Production uses ToProto() mapping, not ToWireName().
**Migration**: Delete test assertions that used ToWireName. Use ToProto() for wire serialization.

#### Scenario: ToWireName is removed
- **WHEN** AlertSeverityExtensions or ConfidenceLevelExtensions is inspected
- **THEN** ToWireName methods do not exist; ToConfidence remains

### Requirement: Unused FetchRejectionReason members
**Reason**: Budget gate delays instead of rejecting; Expired is never emitted.
**Migration**: Remove match arms for Budget and Expired in PollWorker.

#### Scenario: FetchRejectionReason has only QueueFull and PipelineUnavailable
- **WHEN** the FetchRejectionReason enum is inspected
- **THEN** it contains only QueueFull and PipelineUnavailable
