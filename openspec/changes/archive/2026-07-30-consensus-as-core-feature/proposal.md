## Why

Consensus is njord's core value proposition — synthesizing multi-model forecasts into a consolidated view — yet it is implemented as just another `IStatelessEnrichment`, architecturally equal to activity indices or BBQ scores. This creates three problems: (1) enrichments receive raw `ModelSnapshot` and each independently navigates multi-model data instead of consuming the consolidated consensus, (2) daily consensus exists in three redundant forms (direct daily parameters, hourly→daily rollup via `DailyConsensusSummary`, and ha-njord's own client-side aggregation), and (3) consensus has no structural priority in the pipeline despite all other enrichments logically depending on it.

## What Changes

- **BREAKING**: Introduce `ConsensusSnapshot` as the new core domain type replacing `ConsensusResult`, with clearly separated `HourlyConsensus` and `DailyConsensus` facets.
- **BREAKING**: Consensus becomes an Akka.Streams `Select` transformation in the pipeline graph (between `ModelSnapshot` accumulation and enrichment computation), not an `IEnrichmentFeature`.
- **BREAKING**: `IStatelessEnrichment` and `IStatefulEnrichment` interfaces change from `Compute(ModelSnapshot, ...)` to `Compute(ConsensusSnapshot, string location)` — enrichments consume consensus output.
- **BREAKING**: Remove `DailyConsensusSummary` and its hourly→daily rollup. Daily consensus comes exclusively from model daily parameters (`temperature_2m_max`, `precipitation_sum`, etc.).
- Remove `ConsensusEnrichment` from the enrichment feature registry.
- Pipeline graph splits after `ModelSnapshot`: History branch keeps raw snapshot access; all other enrichments receive `ConsensusSnapshot` downstream.
- Consensus egress (MQTT state topics, gRPC mapping, HA discovery) moves out of the enrichment pathway into direct egress from the consensus stage.

## Non-goals

- Changing the consensus statistics (median, trimmed mean, spread, IQR, agreement, outlier, CI) — `ConsensusComputer` is untouched.
- Changing MQTT topic structure or HA entity naming — `h0..hN` and `d0..dN` topics stay as-is.
- Changes to ha-njord — it will benefit from cleaner daily data but is out of scope here.
- Changing the poll pipeline, Open-Meteo client, or fetch logic.
- No API-budget impact — this change does not alter polling behavior.

## Capabilities

### New Capabilities

- `consensus-snapshot`: The `ConsensusSnapshot` domain type, its `Compute` factory (pure transformation from `ModelSnapshot`), and the stream stage that produces it. Covers type structure (`HourlyConsensus`, `DailyConsensus`), cutoff computation, model-count filtering, and the Akka.Streams integration point.

### Modified Capabilities

- `enrichment-actor`: Pipeline graph topology changes — `ModelSnapshot` broadcast splits into History (raw) and Consensus→Enrichment branches.
- `enrichment-feature-registry`: `ConsensusEnrichment` removed from the `IEnrichmentFeature` set; remaining enrichments get new `Compute` signature.
- `hourly-consensus`: Now part of `ConsensusSnapshot.Hourly`; computation logic migrates from `ConsensusResult`.
- `daily-consensus`: Now part of `ConsensusSnapshot.Daily`; `DailyConsensusSummary` removed; daily comes exclusively from model daily parameters.
- `egress-stream-graph`: Consensus egress originates from the consensus stage, not the enrichment pathway.
- `grpc-enrichment-api`: `ConsensusUpdate` proto mapping adapts to `ConsensusSnapshot`.
- `threshold-alerts`: Consumes `ConsensusSnapshot` instead of `ModelSnapshot`.
- `derived-values`: Consumes `ConsensusSnapshot` instead of `ModelSnapshot`.
- `trend-analysis`: Consumes `ConsensusSnapshot` instead of `ModelSnapshot`.
- `activity-indices`: Consumes `ConsensusSnapshot` instead of `ModelSnapshot`.
- `energy-management`: Consumes `ConsensusSnapshot` instead of `ModelSnapshot`.

## Impact

- **Domain**: `ConsensusResult` renamed to `ConsensusSnapshot`; `DailyConsensusSummary` deleted; new wrapper records `HourlyConsensus`, `DailyConsensus`.
- **Enrichment interfaces**: `IStatelessEnrichment`, `IStatefulEnrichment` signatures change; all five non-History enrichments refactored.
- **Pipeline actor**: `EnrichmentActor` graph topology changes (broadcast + consensus stage + merge).
- **Egress**: `StatePayloadBuilder.FromConsensus`, `DiscoveryPayloadBuilder`, `EnrichmentProtoMapper` adapt to new type.
- **Tests**: Consensus tests, enrichment tests, payload builder tests, enrichment actor tests all require updates.
- **Persistence**: `ConsensusResult` appears in persistence DTOs — migration path needed if persisted snapshots reference it.
