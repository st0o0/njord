## MODIFIED Requirements

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a `BudgetThrottleStage` that derives its rate from `IBudgetProvider.GetCurrentRate()`. The stage SHALL implement weighted token-bucket throttling based on the effective budget (default: 80% of `RequestsPerMinute`). The stage SHALL adapt to budget changes at runtime without re-materializing the graph. The fetch call SHALL use `SelectAsyncUnordered(2)` to limit concurrent connections to Open-Meteo to 2. Between the `SelectAsyncUnordered` output and the `BroadcastHub` sink, the pipeline SHALL include a `Buffer(32, OverflowStrategy.Backpressure)` stage to decouple HTTP fetch throughput from downstream processing speed.

#### Scenario: Throttle rate derived from budget
- **WHEN** the effective budget is 600 req/min (free tier)
- **THEN** the stage SHALL throttle at 480 weighted cost-units per minute (80% politeness margin)

#### Scenario: Budget override changes throttle rate at runtime
- **WHEN** the user sets a `BudgetOverride` of 60 req/min via gRPC
- **THEN** the stage SHALL adapt to 48 cost-units per minute within 5 seconds

#### Scenario: Maximum 2 concurrent HTTP calls
- **WHEN** the throttle releases 2 targets within the same second
- **THEN** `SelectAsyncUnordered(2)` processes both concurrently but does not start a third until one completes

#### Scenario: Fetch is inlined in the pipeline graph
- **WHEN** the PipelineActor materializes the pipeline
- **THEN** the fetch logic is a direct `SelectAsyncUnordered` call to `IOpenMeteoClient.FetchAsync`, not a separate `FetchStage.Create()` flow

#### Scenario: Buffer decouples fetch from downstream processing
- **WHEN** downstream consumers are slow to process fetch results
- **THEN** the Buffer(32) accepts completed results without backpressuring the SelectAsync slots, allowing HTTP requests to continue at the token-bucket rate

#### Scenario: Buffer applies backpressure when full
- **WHEN** 32 fetch results are buffered and downstream has not consumed any
- **THEN** the pipeline backpressures the fetch stage until at least one result is consumed

### Requirement: Fetch outcomes fan out via BroadcastHub to egress and feedback consumers
The pipeline SHALL broadcast each `FetchOutcome` via a `BroadcastHub` with `bufferSize: 2`. The EgressActor SHALL consume the BroadcastHub via a `SourceRef` and map outcomes to `MqttMessage`(s) for MQTT publish. The PipelineActor SHALL materialize a local feedback consumer that computes a data hash and sends it to the SchedulerActor via Ask. Egress unavailability (broker down) MUST NOT fail or stall the fetch pipeline -- the BroadcastHub decouples the two paths.

#### Scenario: Egress receives outcome via BroadcastHub SourceRef
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the `FetchOutcome.Success` is broadcast via BroadcastHub and received by the EgressActor's consumer graph

#### Scenario: Feedback consumer computes hash and asks scheduler
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** the feedback consumer computes a `HashResult` and sends it to the SchedulerActor via Ask

#### Scenario: Ask flow provides backpressure via BroadcastHub
- **WHEN** the SchedulerActor is processing a previous HashResult
- **THEN** the feedback consumer waits for Ack; BroadcastHub propagates this as backpressure to the pipeline

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the pipeline continues fetching; the egress consumer handles the failure independently
