## ADDED Requirements

### Requirement: Ref requesters handle typed request failures with backoff
Every actor that requests a SinkRef or SourceRef (`RequestEgressSink`, `RequestEgressSource`, `RequestMqttSink`, `RequestPipelineSink`, `RequestPipelineSource`) SHALL handle the matching project-owned failure message (`EgressSinkFailed`, `EgressSourceFailed`, `MqttSinkFailed`, `PipelineSinkFailed`, `PipelineSourceFailed`, each carrying the `Exception` cause). On a failure message the requester SHALL log at Warning level and re-request the ref after a capped exponential backoff, reusing the existing re-resolve/backoff mechanism of its base class or owner. A failure message SHALL NOT be left unhandled.

Request/failure messages SHALL be project-owned records; the system SHALL NOT use Akka's `Status.Failure` as a reply or `Status.Success`/`Status.Failure` as stream-termination messages.

#### Scenario: Stream consumer retries after a failed ref request
- **WHEN** a StreamConsumerActor subclass receives a failure message for a ref it requested
- **THEN** it logs a warning and re-requests the ref after the next backoff delay

#### Scenario: Scheduler retries after a failed pipeline ref request
- **WHEN** the SchedulerActor receives PipelineSinkFailed or PipelineSourceFailed while waiting for refs
- **THEN** it logs a warning and re-resolves the pipeline and re-requests both refs after the next backoff delay

#### Scenario: gRPC stream call fails with Unavailable
- **WHEN** WeatherGrpcService requests an EgressSource for StreamForecasts or StreamEnrichments and receives EgressSourceFailed
- **THEN** the call ends with `RpcException` status `Unavailable`

#### Scenario: Failure-consumer termination uses project-owned messages
- **WHEN** the SchedulerActor's failure-consumer `Sink.ActorRef` completes or fails
- **THEN** the SchedulerActor receives `FailureConsumerCompleted` or `FailureConsumerFailed(Exception Cause)` and handles it (log; a failure triggers the same re-resolve path as loss of the pipeline source)
