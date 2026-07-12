## MODIFIED Requirements

### Requirement: The pipeline is a single materialized graph
The pipeline SHALL be materialized exactly once by the `PipelineActor` using `Context.Materializer()` (actor-bound lifecycle). No stage within the graph SHALL materialize a sub-stream via `RunWith` or equivalent. The graph SHALL remain materialized for the lifetime of the actor incarnation. No `IHostedService` or manual `KillSwitch` SHALL be used.

#### Scenario: No per-cycle materialization
- **WHEN** 10 poll commands are processed
- **THEN** zero additional stream materializations occur beyond the initial actor-bound materialization

#### Scenario: Actor stop terminates graph
- **WHEN** the PipelineActor stops
- **THEN** the pipeline graph completes without requiring explicit KillSwitch shutdown

### Requirement: Fetch outcomes publish directly without aggregation
Each successful `FetchOutcome` SHALL be mapped to an `MqttMessage` (state topic, payload, retain=true) and delivered to the egress actor's MergeHub via StreamRef. The pipeline SHALL NOT call `IMqttPublisher.PublishAsync` directly. Failed outcomes SHALL NOT produce a message; they are logged and filtered.

#### Scenario: Successful fetch produces MqttMessage via StreamRef
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** one `MqttMessage` for device `njord_lucerne_icon_d2` enters the egress MergeHub via the StreamRef

#### Scenario: Failed fetch does not produce MqttMessage
- **WHEN** a fetch fails
- **THEN** no MqttMessage enters the StreamRef for that device

### Requirement: Clean shutdown via actor lifecycle
The pipeline SHALL terminate cleanly when the PipelineActor stops. In-flight fetches SHALL complete (or timeout) before the graph finalizes. No explicit `KillSwitch` management is required — `Context.Materializer()` binds the graph to the actor.

#### Scenario: Graceful shutdown completes in-flight work
- **WHEN** the actor is stopped while 3 fetches are in progress
- **THEN** those fetches complete (or timeout) and the graph terminates

## REMOVED Requirements

### Requirement: Clean shutdown via KillSwitch
**Reason:** Replaced by actor-bound materialization. The actor's lifecycle manages stream termination — no explicit `KillSwitch.Shutdown()` or `IHostApplicationLifetime` registration is needed.
**Migration:** Remove `UniqueKillSwitch` from `PollPipeline.Create` return value. Remove `IHostApplicationLifetime` stop registration. The `PipelineActor.PostStop` naturally terminates the graph via the actor-scoped materializer.
