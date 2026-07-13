## Why

After every poll njord holds raw data from up to 8 independent weather models in RAM — a multi-model ensemble forwarded 1:1 to MQTT. All multi-model intelligence (consensus, uncertainty, spread) remains untapped; downstream consumers (HA templates, external services) would each have to duplicate the statistical aggregation. This change adds the enrichment actor infrastructure (M0) and the first consumer — consensus (M1) — so njord publishes a statistically aggregated "consensus" pseudo-model alongside the raw per-model data.

## What Changes

- **EnrichmentActor** — new actor consuming the pipeline's `BroadcastHub<FetchOutcome>` via `SourceRef`, maintaining a running `ModelSnapshot` via `Scan`, and distributing to consumer streams through a second `BroadcastHub<ModelSnapshot>`.
- **ModelSnapshot** — new domain type: immutable record holding the latest `ModelForecast` per (location, model) with change detection.
- **MqttSinkRef protocol** — `MqttEgressActor` gains a `RequestMqttSink` / `MqttSinkResponse` handler so the EnrichmentActor can push computed `MqttMessage`s into the existing MergeHub transport.
- **EnrichmentOptions** — per-consumer `Enabled` flag in `NjordOptions.Enrichment`. Only enabled consumers are materialized. Consensus enabled by default.
- **ConsensusComputer** — pure static class computing median, trimmed mean, spread, IQR, agreement score, outlier identification, confidence interval, and model availability matrix.
- **ConsensusResult** — record per (parameter, horizon) serialized to `MqttMessage` with delta publishing (lastPublished cache).
- **Topic scheme extension** — `TopicScheme.ConsensusTopic(baseTopic, location, horizon)` for the consensus pseudo-model topics.
- **Discovery payload** — consensus device `njord_{location}_consensus` with the same horizons and parameters as model devices, plus meta attributes (spread, agreement, models_used).

## Non-goals

- **Later enrichment consumers (M2–M7)** — alerts, derived values, trends, indices, energy, history are out of scope. This change delivers only the infrastructure + consensus.
- **Akka.Cluster** — data volume doesn't justify a cluster; all consumers run in-process.
- **Custom Akka.Streams stages** — exclusively built-in operators.
- **Separate MQTT connection per consumer** — all consumers use the existing MergeHub/transport.
- **Consensus in HA** — no Jinja2 templates or helpers; njord computes everything.
- **API budget impact** — zero. Enrichment operates exclusively on already-fetched data; no additional API calls.

## Capabilities

### New Capabilities
- `model-snapshot`: Running state of latest model data per location with update and change detection.
- `enrichment-actor`: EnrichmentActor lifecycle, Scan-based snapshot accumulation, second BroadcastHub, SinkRef binding to MqttEgressActor, consumer materialization gated by configuration.
- `consensus-computation`: Median consensus, trimmed mean, spread, IQR, agreement score, outlier identification, confidence interval, model availability matrix across all available models per time point.

### Modified Capabilities
- `mqtt-egress`: MqttEgressActor must provide a `SinkRef<MqttMessage>` on request via `RequestMqttSink` / `MqttSinkResponse` so external actors can push into the existing MergeHub.
- `stream-composition`: BroadcastHub consumer architecture extended with the EnrichmentActor as an additional consumer of the pipeline's `SourceRef<FetchOutcome>`.

## Impact

- **New files:** `src/Njord/Enrichment/` directory — `ModelSnapshot.cs`, `EnrichmentActor.cs`, `ConsensusComputer.cs`, `ConsensusResult.cs`, `EnrichmentOptions.cs`.
- **Modified files:** `MqttEgressActor.cs` (SinkRef protocol), `TopicScheme.cs` (consensus topics), `DiscoveryPayloadBuilder.cs` (consensus device), `NjordOptions.cs` (enrichment config), `Program.cs` (DI registration).
- **New tests:** `ModelSnapshotSpec.cs`, `EnrichmentActorSpec.cs`, `ConsensusComputerSpec.cs` in `src/Njord.Tests/Enrichment/`.
- **No new dependencies** — all computation is pure C#; Akka.Streams operators are already available.
