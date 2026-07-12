## ADDED Requirements

### Requirement: The pipeline is a single materialized graph
The pipeline SHALL be materialized exactly once at startup as a single connected graph from source to sink. No stage within the graph SHALL materialize a sub-stream via `RunWith` or equivalent. The graph SHALL remain materialized for the lifetime of the host process.

#### Scenario: No per-cycle materialization
- **WHEN** 10 poll commands are processed
- **THEN** zero additional stream materializations occur beyond the initial startup materialization

### Requirement: MergeHub accepts commands from multiple sources
The pipeline entry point SHALL be a `MergeHub<PipelineCommand>` that accepts dynamically attached sources. The materialized sink handle SHALL be available for source attachment after startup.

#### Scenario: Tick source attaches to the hub
- **WHEN** the pipeline is materialized
- **THEN** a `Source.Tick`-based producer attaches to the MergeHub and emits `PollAll` commands at the configured interval

#### Scenario: Multiple sources can attach concurrently
- **WHEN** a second source (e.g. test source) attaches to the MergeHub
- **THEN** commands from both sources are processed through the same pipeline graph

### Requirement: Weighted throttle enforces the global API budget
The pipeline SHALL throttle fetch requests using a cost-weighted throttle set to 80% of the Open-Meteo per-minute rate limit. Each element's cost SHALL be its pre-calculated API weight. The throttle SHALL use shaping mode (delay, not drop).

#### Scenario: Weight-1 requests within budget pass immediately
- **WHEN** 8 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** all 8 pass through without delay

#### Scenario: Burst beyond budget is shaped
- **WHEN** 500 weight-1 targets enter the throttle with budget ceiling 480/min
- **THEN** the first 480 pass within the first minute; the remaining 20 are delayed into the next minute window

### Requirement: Fetch outcomes publish directly without aggregation
Each successful `FetchOutcome` SHALL be mapped to a device state payload and published to MQTT independently. The pipeline SHALL NOT collect or batch outcomes before publishing. Failed outcomes SHALL NOT produce a publish; the device retains its last state on the broker.

#### Scenario: Successful fetch publishes immediately
- **WHEN** a fetch for (lucerne, icon_d2) succeeds
- **THEN** one MQTT state publish for device `njord_lucerne_icon_d2` occurs without waiting for other fetches

#### Scenario: Failed fetch does not publish
- **WHEN** a fetch for (lucerne, ecmwf_ifs025) fails with ModelUnavailable
- **THEN** no MQTT state publish occurs for that device; the broker retains its previous state

### Requirement: Pipeline stages are independently testable flows
Each processing stage (expand, throttle+fetch, publish) SHALL be implemented as a standalone `Flow<TIn, TOut>` or `Sink<TIn>` factory method. Tests SHALL be able to wire a `TestSource` to any stage without materializing the full pipeline.

#### Scenario: Expand stage is testable in isolation
- **WHEN** a test wires `TestSource<PipelineCommand> → ExpandStage → TestSink`
- **THEN** the expand logic can be verified without an HTTP client or MQTT connection

#### Scenario: Fetch stage is testable with a mock client
- **WHEN** a test wires `TestSource<WeightedTarget> → FetchStage(mockClient) → TestSink`
- **THEN** fetch behavior (parallelism, error handling) can be verified in isolation

### Requirement: Stream supervision resumes on transient fetch failures
The fetch stage SHALL use a supervision decider that resumes processing on transient failures (HTTP timeout, rate-limit 429, transport errors). A single failed fetch SHALL NOT restart or terminate the graph. Persistent failures (repeated errors for the same target) SHALL be logged but SHALL NOT stall the pipeline.

#### Scenario: HTTP timeout does not kill the stream
- **WHEN** a single fetch times out
- **THEN** the stream continues processing subsequent targets; the timed-out target emits a `FetchOutcome.Failure`

#### Scenario: Repeated failures for one model do not stall others
- **WHEN** model "icon_d2" fails 5 times consecutively
- **THEN** other models continue to be fetched and published on schedule

### Requirement: Clean shutdown via KillSwitch
The pipeline SHALL expose a `KillSwitch` (or equivalent cancellation mechanism) that the host can trigger during graceful shutdown. When triggered, the pipeline SHALL complete in-flight fetches and then terminate. No new commands SHALL be processed after shutdown is signalled.

#### Scenario: Graceful shutdown completes in-flight work
- **WHEN** the host signals shutdown while 3 fetches are in progress
- **THEN** those 3 fetches complete (or timeout) and no further targets enter the fetch stage

### Requirement: Observability via structured logging per outcome
The pipeline SHALL emit one structured log entry per fetch outcome (success or failure) containing location, model, duration, and result status. A periodic summary (targets fetched since last summary, success rate) SHALL be emittable via a side-channel (`WireTap` or equivalent) without blocking the main flow.

#### Scenario: Each fetch logs its outcome
- **WHEN** a fetch for (lucerne, icon_d2) completes in 1200ms
- **THEN** a structured log entry is emitted with location="lucerne", model="icon_d2", duration=1200ms, status="success"
