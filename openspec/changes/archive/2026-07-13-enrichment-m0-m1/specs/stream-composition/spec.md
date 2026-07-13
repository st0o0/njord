# stream-composition Specification (Delta)

## Purpose

Extends the BroadcastHub consumer architecture to include the EnrichmentActor as an additional consumer of the pipeline's SourceRef.

## ADDED Requirements

### Requirement: The EnrichmentActor is a BroadcastHub consumer via SourceRef
The `EnrichmentActor` SHALL request a `SourceRef<FetchOutcome>` from the `PipelineActor` using the existing `RequestPipelineSource` / `PipelineSourceResponse` protocol. This makes the EnrichmentActor a third consumer of the pipeline's BroadcastHub, alongside the EgressActor's consumer graph and the PipelineActor's local feedback consumer. The PipelineActor SHALL NOT require any changes to support this — the existing SourceRef vending mechanism supports multiple consumers.

#### Scenario: Three consumers receive the same fetch outcome
- **WHEN** a `FetchOutcome.Success` enters the BroadcastHub
- **THEN** the egress consumer, the feedback consumer, and the EnrichmentActor's consumer all receive it

#### Scenario: EnrichmentActor connects independently
- **WHEN** the EnrichmentActor starts and requests a SourceRef
- **THEN** the PipelineActor vends a SourceRef without any protocol changes

#### Scenario: EnrichmentActor failure does not affect other consumers
- **WHEN** the EnrichmentActor's consumer stream fails
- **THEN** the egress consumer and feedback consumer continue operating; the EnrichmentActor restarts and re-requests a SourceRef
