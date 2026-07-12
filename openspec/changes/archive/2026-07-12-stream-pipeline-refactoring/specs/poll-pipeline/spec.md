## MODIFIED Requirements

### Requirement: Cycles are scheduled at the configured interval
The pipeline SHALL emit a `PollAll` command at the configured interval (default 60 minutes) via a `Source.Tick` attached to the MergeHub. The tick source SHALL use the injected `TimeProvider` for testability.

#### Scenario: Tick emits PollAll at interval
- **WHEN** the configured poll interval is 60 minutes
- **THEN** a `PollAll` command enters the MergeHub every 60 minutes

### Requirement: Each cycle fans out over locations × models
For every `PollAll` command the expand stage SHALL emit exactly one `WeightedTarget` per configured (location, model) pair.

#### Scenario: Fan-out count
- **WHEN** 2 locations and 4 models are configured and a `PollAll` is processed
- **THEN** exactly 8 `WeightedTarget` elements are emitted

### Requirement: Outbound requests respect the per-minute budget
The pipeline SHALL throttle outbound fetch requests using a weighted throttle set to 80% of the resolved per-minute budget. Each request's cost is its pre-calculated API weight.

#### Scenario: Burst is shaped by weight
- **WHEN** a `PollAll` fans out 24 weight-1 targets and the budget ceiling is 480/min
- **THEN** all 24 pass through the throttle without delay (well within budget)

### Requirement: The pipeline restarts with backoff
The tick source SHALL be wrapped in `RestartSource.WithBackoff` so that source-level failures (not individual fetch failures) restart the tick with exponential backoff and jitter without terminating the service. Individual fetch failures are handled by the stream supervision decider and do not trigger a source restart.

#### Scenario: Source failure restarts the tick
- **WHEN** the tick source throws an unhandled exception
- **THEN** the tick restarts after the backoff delay and the pipeline continues processing

#### Scenario: Fetch failure does not restart the source
- **WHEN** a single fetch throws an unhandled exception
- **THEN** the supervision decider handles it (resume/log) without restarting the tick source

### Requirement: Fetch outcomes feed the MQTT egress directly
The pipeline SHALL publish each successful `FetchOutcome` as a device state payload to MQTT immediately upon completion. There is no batching, no cycle-result aggregation, and no intermediate actor forwarding. Egress unavailability (broker down) MUST NOT fail or stall the fetch pipeline.

#### Scenario: Outcome reaches MQTT without batching
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** a device state publish for `njord_lucerne_icon_d2` occurs immediately

#### Scenario: Broker outage does not stall fetching
- **WHEN** the MQTT broker is unreachable
- **THEN** the pipeline continues fetching; publishes are retried or dropped per the egress policy

## REMOVED Requirements

### Requirement: Cycles aggregate with a timeout and never block on missing models
**Reason:** With consensus deferred, there is no need to collect all model outcomes before publishing. Each fetch outcome publishes independently to its device state topic. The "cycle result" abstraction (received/failed/unanswered lists) is replaced by per-outcome logging and direct publish.
**Migration:** Remove `CycleResult`, `CycleId`, and the aggregation window. Per-outcome structured logging provides equivalent observability. The MQTT egress receives individual `FetchOutcome.Success` payloads instead of batched `CycleResult`.

### Requirement: Cycle results feed the MQTT egress
**Reason:** Replaced by direct per-outcome publishing. The pipeline no longer produces `CycleResult`; each successful outcome is published individually. The "one summary log per cycle" is replaced by per-outcome structured logs and periodic metric summaries.
**Migration:** The `PublishTelemetry(IReadOnlyList<ModelForecast>)` message is replaced by individual publishes from the stream sink. The `PipelineGuardianActor`'s forwarding role is eliminated.
