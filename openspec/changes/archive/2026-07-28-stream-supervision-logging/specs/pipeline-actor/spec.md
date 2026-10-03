## MODIFIED Requirements

### Requirement: The pipeline actor materializes the feedback consumer locally
The PipelineActor SHALL materialize a BroadcastHub consumer that filters for `FetchOutcome.Success`, computes a `ForecastDataHash`, and sends the `HashResult` to the SchedulerActor via the built-in `Ask<Ack>` flow.

The feedback consumer's stream supervision SHALL use the shared `StreamSupervision.LoggingDecider` with the PipelineActor's ILogger. The fetch stage's stream supervision SHALL also use the shared decider.

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a FetchOutcome.Success arrives
- **THEN** a ForecastDataHash is computed and sent to SchedulerActor via Ask

#### Scenario: Feedback consumer ignores failures
- **WHEN** a FetchOutcome.Failure arrives
- **THEN** it is filtered out before hash computation

#### Scenario: Feedback consumer lifecycle is bound to PipelineActor
- **WHEN** PipelineActor stops
- **THEN** the feedback consumer stream completes

#### Scenario: Feedback consumer logs exceptions instead of silently resuming
- **WHEN** an exception occurs in the feedback consumer stream
- **THEN** the exception is logged at Warning level via the PipelineActor's ILogger
