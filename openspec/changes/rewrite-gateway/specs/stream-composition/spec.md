## MODIFIED Requirements

### Requirement: Each stream-owning actor materializes its graph once
`OpenMeteoGateway` SHALL materialize its fetch-and-deliver graph once, when the pipeline's
SinkRef arrives. The graph SHALL terminate in the pipeline's SinkRef sink with an `AlsoTo`
branch for replies. No separate forecast queue or branch stream SHALL be materialized; the
SinkRef itself is the stream's sink. `ForecastPipeline` SHALL materialize its graphs once per
incarnation as before. Both SHALL use `Context.Materializer()`, so a graph lives exactly as
long as the actor incarnation that owns it.

#### Scenario: No per-cycle materialization
- **WHEN** 10 fetch requests are processed
- **THEN** no stream materialization happens beyond the gateway's initial graph and the stream refs vended on request

#### Scenario: Actor stop terminates graph
- **WHEN** the gateway stops
- **THEN** its graph terminates with the actor-scoped materializer; no explicit queue completion is needed

#### Scenario: Single graph with pipeline as sink
- **WHEN** the gateway receives the pipeline SinkRef
- **THEN** it SHALL materialize one graph whose terminal sink is the SinkRef, not a separate intermediary queue
