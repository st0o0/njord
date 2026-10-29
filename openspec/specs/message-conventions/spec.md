## Purpose

Defines the conventions for actor messages: naming, response hierarchies with typed success and failure subtypes, handshake failure naming, and the organization of Njord.Messages into domain namespaces with one file per command or query. All inter-actor messages live in Njord.Messages, including stream handshakes.

## Requirements

### Requirement: Handshake failures follow Request*Failed naming
Failure response records SHALL be named `Request<Noun>Failed` when they respond to a `Request<Noun>` command, matching the established `Request<Noun>Response` / `Request<Noun>Completed` / `Request<Noun>Failed` convention.

#### Scenario: ForecastSink handshake failure
- **WHEN** a `RequestForecastSink` command fails
- **THEN** the failure record is `RequestForecastSinkFailed` (not `ForecastSinkFailed`)

### Requirement: Failed carries exception
Every `*Failed` response record SHALL carry the caught `Exception` as a `Cause` property.

#### Scenario: Actor encounters exception during handling
- **WHEN** an actor encounters an exception while handling a query or command
- **THEN** it responds with the domain's `*Failed` record containing the caught `Exception` as `Cause`

### Requirement: Message location rule
Every public record or class that is sent between actors (via Tell, Ask, or shard routing) or through Akka.Streams boundaries (via SinkRef, SourceRef, or hub materialization) SHALL be defined in the Njord.Messages project. Stream handshake messages carrying ISinkRef<T> or ISourceRef<T> are included.

#### Scenario: Stream handshakes are in Njord.Messages
- **WHEN** the ForecastPipeline replies to a sink request with an ISinkRef
- **THEN** the RequestForecastSinkCompleted record is defined in Njord.Messages.Stream

#### Scenario: Stream payloads are in Njord.Messages
- **WHEN** the gateway emits DomainDataReceived through the pipeline
- **THEN** DomainDataReceived is defined in Njord.Messages.Stream

### Requirement: Dead message types removed
QueryBudgetUsage hierarchy, BudgetStatusFailed, PollStatesFailed, ToWireName extensions, and FetchRejectionReason.Budget/.Expired SHALL NOT exist. QueryBudgetStatus/BudgetStatusResult is the single budget query. FetchRejectionReason contains only QueueFull and PipelineUnavailable.

#### Scenario: QueryBudgetUsage hierarchy is removed
- **WHEN** a consumer needs budget information
- **THEN** it uses QueryBudgetStatus / BudgetStatusResult (which is a superset)

#### Scenario: FetchRejectionReason has only QueueFull and PipelineUnavailable
- **WHEN** the FetchRejectionReason enum is inspected
- **THEN** it contains only QueueFull and PipelineUnavailable

