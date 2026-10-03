## MODIFIED Requirements

### Requirement: The EnrichmentActor maintains a ModelSnapshot via Scan

The EnrichmentActor SHALL accumulate `FetchOutcome.Success` into a `ModelSnapshot` via Scan. After accumulation, the snapshot SHALL be broadcast to two branches: (1) History (raw `ModelSnapshot`), (2) Consensus transformation followed by enrichments.

#### Scenario: Success updates the snapshot
- **WHEN** a `FetchOutcome.Success` arrives
- **THEN** the `ModelSnapshot` is updated and broadcast to both branches

#### Scenario: Failure does not change the snapshot
- **WHEN** a `FetchOutcome.Failure` arrives
- **THEN** the `ModelSnapshot` remains unchanged

#### Scenario: Unchanged data is filtered
- **WHEN** a `FetchOutcome.Success` arrives with identical data
- **THEN** downstream receives no update

#### Scenario: No Ingest namespace import in Enrichment
- **WHEN** the EnrichmentActor source is inspected
- **THEN** it SHALL NOT import any namespace from `Njord.Ingest`

### Requirement: EnrichmentActor fans out enrichment results to EgressActor

The enrichment inline flow SHALL consume `ConsensusSnapshot` (not `ModelSnapshot`) and produce `EgressEvent` messages sent to the `EgressActor`.

#### Scenario: Enrichment produces EnrichmentUpdate
- **WHEN** a `ConsensusSnapshot` flows through the enrichment inline flow
- **THEN** each enabled enrichment produces `EgressEvent.EnrichmentUpdate`

#### Scenario: No type-specific Materialize methods
- **WHEN** the `EnrichmentActor` source is inspected
- **THEN** there SHALL be no per-enrichment-type materialization methods

#### Scenario: No MQTT dependency
- **WHEN** the `EnrichmentActor` project references are inspected
- **THEN** there SHALL be no reference to MQTTnet

### Requirement: The EnrichmentActor pipeline broadcasts ModelSnapshot before consensus

The stream graph SHALL broadcast the `ModelSnapshot` to two outputs: one for History (raw) and one for the consensus `Select` stage. The consensus stage output SHALL feed the enrichment inline flow.

#### Scenario: History receives raw ModelSnapshot
- **WHEN** a `ModelSnapshot` is broadcast
- **THEN** the History branch receives the unmodified `ModelSnapshot`

#### Scenario: Enrichments receive ConsensusSnapshot
- **WHEN** a `ModelSnapshot` is broadcast
- **THEN** the enrichment branch receives `ConsensusSnapshot` instances produced by the consensus `Select` stage

#### Scenario: Consensus stage is a Select transformation
- **WHEN** the stream graph is inspected
- **THEN** the consensus stage SHALL be a `Select` (map), not an actor or separate materialization

### Requirement: Consumer streams are materialized only when enabled

Enrichment consumer streams SHALL be materialized only for enabled features. The consensus `Select` stage SHALL always be materialized (it is not toggleable). History materialization is unchanged.

#### Scenario: Disabled feature is not materialized
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is false
- **THEN** no alert consumer stream is materialized

#### Scenario: Enabled stateless feature is materialized via loop
- **WHEN** `EnrichmentOptions.Alerts.Enabled` is true
- **THEN** the alert consumer stream is materialized consuming `ConsensusSnapshot`

#### Scenario: Enabled stateful feature uses Scan pairing
- **WHEN** `EnrichmentOptions.Trends.Enabled` is true
- **THEN** the trend consumer uses Scan to pair current and previous `ConsensusSnapshot`

#### Scenario: Enabled actor feature delegates materialisation
- **WHEN** `EnrichmentOptions.History.Enabled` is true
- **THEN** the history consumer is materialized on the raw `ModelSnapshot` branch

### Requirement: Consensus egress events originate from the consensus stage

The consensus stage SHALL emit `EgressEvent.ConsensusUpdate` directly to the `EgressActor`, separate from the enrichment inline flow.

#### Scenario: ConsensusUpdate reaches EgressActor
- **WHEN** the consensus `Select` stage produces `ConsensusSnapshot` instances
- **THEN** corresponding `EgressEvent.ConsensusUpdate` events SHALL be sent to the `EgressActor`

#### Scenario: Consensus egress does not flow through enrichment inline flow
- **WHEN** the enrichment inline flow processes enrichments
- **THEN** it SHALL NOT produce consensus-related `EgressEvent` messages

### Requirement: Stream supervision resumes on consumer errors

The stream supervision strategy SHALL resume on consumer exceptions without killing the pipeline.

#### Scenario: Consumer exception does not kill the pipeline
- **WHEN** an enrichment consumer throws an exception
- **THEN** the stream resumes and other consumers are unaffected
