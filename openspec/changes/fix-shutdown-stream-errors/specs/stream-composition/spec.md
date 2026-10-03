## MODIFIED Requirements

### Requirement: Clean shutdown via actor lifecycle
The pipeline and egress hub graphs SHALL terminate cleanly on service shutdown. A CoordinatedShutdown task in phase `before-service-unbind` SHALL ask the PipelineActor and then the EgressActor to stop their streams (`StopStreams`), each bounded by a timeout; a timeout or failure SHALL be logged as a warning and SHALL NOT block shutdown. In-flight fetches SHALL complete (or time out) before the graph finalizes. The actor-owned kill switch SHALL be used only for this shutdown path; the graphs SHALL still be bound to the actor via `Context.Materializer()`.

#### Scenario: Graceful shutdown completes in-flight work
- **WHEN** the actor is stopped while 3 fetches are in progress
- **THEN** those fetches complete (or timeout) and the graph terminates

#### Scenario: SIGTERM produces no stream errors
- **WHEN** the service receives SIGTERM during normal operation
- **THEN** no `AbruptTerminationException` is logged for `egress-hub`, `pipeline-fetch-in` or `pipeline-fetch-out`

#### Scenario: Unresponsive actor does not block shutdown
- **WHEN** an actor does not answer `StopStreams` within the timeout
- **THEN** a warning is logged and CoordinatedShutdown proceeds
