## MODIFIED Requirements

### Requirement: Cycles are scheduled at the configured interval
The pipeline SHALL no longer emit `PollAll` commands on a tick. Instead, the pipeline SHALL receive individual `WeightedTarget` elements via a `Source.Queue` fed by the SchedulerActor. The tick source is removed entirely — all poll timing is owned by the SchedulerActor.

#### Scenario: Targets arrive from the SchedulerActor
- **WHEN** the SchedulerActor's timer fires for (lucerne, icon_d2)
- **THEN** a `WeightedTarget` for that pair enters the pipeline via the Source.Queue

#### Scenario: No tick source exists
- **WHEN** the pipeline is materialized
- **THEN** no `Source.Tick` or `RestartSource.WithBackoff` is attached

### Requirement: Fetch outcomes feed the MQTT egress directly
The pipeline SHALL publish each successful `FetchOutcome` as device state payloads to MQTT immediately upon completion. After publishing, the pipeline SHALL compute a data hash over the forecast values and send it to the SchedulerActor via the built-in Ask flow for schedule feedback. Egress unavailability (broker down) MUST NOT fail or stall the fetch pipeline.

#### Scenario: Outcome reaches MQTT before hash computation
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** device state publishes occur first, then the hash is computed and sent to the SchedulerActor

#### Scenario: Ask flow provides backpressure
- **WHEN** the SchedulerActor is processing a previous HashResult
- **THEN** the stream waits for Ack before processing the next fetch outcome

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the pipeline continues fetching; publishes are retried or dropped per the egress policy

## REMOVED Requirements

### Requirement: The pipeline restarts with backoff
**Reason**: The tick source wrapped in `RestartSource.WithBackoff` is removed. Fetch-level supervision remains unchanged (stream supervision decider handles transient failures). Source-level restarts are no longer needed because there is no tick source — the SchedulerActor provides targets via the queue.
**Migration**: Remove `RestartSource.WithBackoff` and `TickSource.cs`. Stream supervision for fetch failures continues to operate as before.
