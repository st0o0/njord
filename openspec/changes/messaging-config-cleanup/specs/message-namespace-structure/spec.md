## ADDED Requirements

### Requirement: Messages project namespace layout
Njord.Messages SHALL organize its types into the following top-level namespaces: Alerts, Budget, Config, Fetch, Polling, Stream, and Results. The Results namespace SHALL contain sub-namespaces per OutputKind: Alerts, Scores, Conditions, Trends, and Forecasts.

#### Scenario: Polling messages live in the Polling namespace
- **WHEN** a consumer references polling commands (StartPolling, StopPolling, TriggerImmediatePoll, PollWorkerReport, QueryPollStates, QueryPollHealth)
- **THEN** they are found in the `Njord.Messages.Polling` namespace

#### Scenario: Stream payload and handshake types live in the Stream namespace
- **WHEN** a consumer references DomainDataReceived, PipelineEvent subtypes, or stream request/response messages (RequestForecastSink, RequestPipelineOutput, RequestGrpcStream families)
- **THEN** they are found in the `Njord.Messages.Stream` namespace

#### Scenario: Feature results are organized by OutputKind
- **WHEN** a consumer references a feature result record
- **THEN** alert results are in `Njord.Messages.Results.Alerts`, score results in `Njord.Messages.Results.Scores`, condition results in `Njord.Messages.Results.Conditions`, trend results in `Njord.Messages.Results.Trends`, and forecast results in `Njord.Messages.Results.Forecasts`

#### Scenario: Feature enums are co-located with their results
- **WHEN** a feature result uses a domain enum (e.g., UvLevel, CommuteLevel, PollenType)
- **THEN** that enum is defined in the same namespace and file as the result that uses it

#### Scenario: Fetch messages use the Fetch namespace
- **WHEN** a consumer references FetchDomain, FetchDomainCompleted, FetchDomainFailed, FetchDomainRejected, FetchResult, FetchSuccess, FetchEmpty, FetchError, or QueryDomainData
- **THEN** they are found in the `Njord.Messages.Fetch` namespace

### Requirement: PipelineEvent subtypes are standalone records
The 8 PipelineEvent subtypes (ForecastDataReceived, AirQualityDataReceived, FloodDataReceived, MarineDataReceived, SeasonalDataReceived, SatelliteDataReceived, EnsembleDataReceived, Heartbeat) SHALL be top-level sealed records inheriting from PipelineEvent, not nested inside it.

#### Scenario: PipelineEvent subtypes are not nested
- **WHEN** a consumer pattern-matches on a PipelineEvent
- **THEN** each subtype is a top-level type in the Stream namespace (e.g., `ForecastDataReceived`, not `PipelineEvent.ForecastDataReceived`)

### Requirement: No inter-actor messages outside Njord.Messages
Every public record that crosses an actor or stream boundary SHALL be defined in Njord.Messages. No exceptions for Akka.Streams types.

#### Scenario: Core/Actors contains no message types
- **WHEN** Njord.Core.Actors is inspected
- **THEN** it contains only actor marker interfaces, IActorRegistration, and StreamSupervision -- no message records

#### Scenario: Njord.Grpc contains no message types
- **WHEN** Njord.Grpc is inspected for record types
- **THEN** GrpcStreamMessages.cs does not exist; those types live in Njord.Messages.Stream
