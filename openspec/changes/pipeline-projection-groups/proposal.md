## Why

The current pipeline processes single-model forecasts through ConsensusBuilder → Analyzers. The new pipeline processes multi-domain `DomainDataReceived` events, maintains a `LocationForecast` read-model per location, and routes data through ProjectionGroups to typed enrichments. This is the core architectural shift from "compute consensus" to "enrich multi-domain data."

## What Changes

- **BREAKING**: Pipeline input changes from `Forecast` to `DomainDataReceived(Location, DomainKind, IDomainForecast)`.
- `MergeHub<DomainDataReceived>` replaces `MergeHub<Forecast>`.
- `StatefulSelectMany` state changes from `ModelForecasts` to `LocationForecast` per location (previous + current).
- ProjectionGroup-based evaluation: each group has a Projector + N Enrichments.
- Reactive evaluation: only groups affected by the changed domain run.
- Weather Alerts migrated as the first ProjectionGroup (14 rules on `DomainForecast<WeatherPoint>`).
- Alert confidence derived from Ensemble StdDev instead of model agreement.
- `BroadcastHub<PipelineEvent>` with new event types (`WeatherDataReceived`, `AlertRaised`, etc.).

## Non-goals

- Adding enrichments for non-Weather domains — that's Phase 4 per-domain changes.
- Changing consumer attachment (SourceRef pattern stays).

## Capabilities

### New Capabilities

- `projection-group-pipeline`: LocationForecast state, ProjectionGroup evaluation, reactive domain routing.
- `weather-alert-projection`: Weather alerts as a ProjectionGroup on DomainForecast<WeatherPoint>.

### Modified Capabilities

- `pipeline-composition`: Entire pipeline input/state/output model changes.
- `analysis-rules`: Alert rules work on DomainForecast instead of LocationConsensus.
- `threshold-alerts`: Confidence from Ensemble StdDev instead of model agreement.
- `consumer-model`: Consumers receive new PipelineEvent types.

## Impact

- **Njord.Pipeline**: ForecastPipeline core redesign.
- **Njord.Alerts**: All 14 rules refactored to DomainForecast<WeatherPoint> input.
- **Njord.Messages**: New PipelineEvent variants.
- **Njord.Grpc**: GrpcConsumer adapts to new events.
- **Njord.Mqtt**: MqttStatePublisher adapts to new events.
