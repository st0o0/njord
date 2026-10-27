## MODIFIED Requirements

### Requirement: Pipeline offer as internal stream branch
The gateway SHALL offer successful fetch results to the pipeline through a `WireTap` side-sink (not `AlsoTo`). The reply sink SHALL NOT propagate backpressure to the main pipeline data path. If the reply sink is temporarily slow, copies SHALL be dropped rather than blocking the pipeline delivery.

#### Scenario: Successful fetch offers to pipeline
- **WHEN** a client returns `FetchSuccess`
- **THEN** the gateway SHALL offer the forecast data to the pipeline and reply `FetchDomainCompleted` to the requester

#### Scenario: Pipeline unavailability does not fail the fetch
- **WHEN** the pipeline sink is not yet connected
- **THEN** the gateway SHALL still reply to the requester; the pipeline offer SHALL be dropped with a logged warning

#### Scenario: Reply sink backpressure does not slow the pipeline
- **WHEN** the reply sink is temporarily slower than the main stream
- **THEN** reply copies SHALL be dropped and the pipeline data path SHALL continue unimpeded
