# stream-composition Specification

## Purpose

Structural requirements for the Akka.Streams graphs: each stream-owning actor (`OpenMeteoGateway`, `ForecastPipeline`, consumers) materializes its graph once and binds its lifecycle to the actor, the gateway throttles to the global API budget and sends each forecast directly into the pipeline input, streams use a logging supervision decider, metrics are recorded per fetch outcome, and ref requesters handle typed handshake failures.
## Requirements
### Requirement: Each stream-owning actor materializes its graph once
`OpenMeteoGateway` SHALL materialize its private fetch graph once, in `PreStart`, without waiting for any other actor, and a small forecast branch each time it attaches to the pipeline input. `ForecastPipeline` SHALL materialize its input hub, analysis graph and output hub once per actor incarnation, in `PreStart`. Both SHALL use `Context.Materializer()`, so a graph lives exactly as long as the actor incarnation that owns it. No stage SHALL materialize a sub-stream per element; the only additional materializations SHALL be the SinkRefs and SourceRefs vended to producers and consumers and the gateway's forecast branch. No `IHostedService` SHALL own a stream.

#### Scenario: No per-cycle materialization
- **WHEN** 10 fetch requests are processed
- **THEN** no stream materialization happens beyond the actors' initial graphs, the gateway's forecast branch and the stream refs vended on request

#### Scenario: Actor stop terminates graph
- **WHEN** the gateway or `ForecastPipeline` stops
- **THEN** its graphs terminate with the actor-scoped materializer

#### Scenario: Gateway materializes without waiting for consumers
- **WHEN** `OpenMeteoGateway` starts
- **THEN** its fetch graph is materialized immediately, before the pipeline answers its SinkRef request

### Requirement: Observability via metrics per fetch outcome
`OpenMeteoClient` SHALL record every fetch in the `njord_openmeteo_fetch_total` counter tagged with `location`, `model` and `outcome` (`success` or the lower-case failure reason), and SHALL record the duration of successful fetches in the `njord_openmeteo_fetch_duration_seconds` histogram tagged with `location` and `model`.

#### Scenario: Each fetch counts its outcome
- **WHEN** a fetch for (lucerne, icon_d2) succeeds in 1.2 s
- **THEN** `njord_openmeteo_fetch_total{location="lucerne",model="icon_d2",outcome="success"}` increases by 1 and 1.2 s is recorded in the duration histogram

#### Scenario: Failures are counted by reason
- **WHEN** a fetch is answered with HTTP 429
- **THEN** the counter increases with `outcome="ratelimited"` and no duration is recorded

