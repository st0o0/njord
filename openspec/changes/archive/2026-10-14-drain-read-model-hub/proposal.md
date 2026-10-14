## Why

`ForecastReadModel` feeds every pipeline event into a secondary `BroadcastHub<PipelineEvent>` (buffer size 2) that only gRPC streaming clients consume. An Akka.NET BroadcastHub does not drop elements when no subscriber is attached: it backpressures its upstream. Without a connected `StreamForecasts`/`StreamEnrichments` client, the read model therefore stops pulling from the pipeline after a few events. The pipeline's own output hub moves at the pace of its slowest consumer, so the whole analysis stream stalls, MQTT included. A slow or stuck streaming client causes the same stall while it is attached. MQTT-only setups and the time between ha-njord reconnects are exactly the cases where no streaming client is connected.

## What Changes

- The read model's secondary hub gets a permanent drain consumer, so it always has a subscriber and never backpressures the read model's sink.
- Each streaming client's SourceRef gets a bounded buffer that drops its oldest events when the client falls behind, so a slow client loses events for itself instead of holding up the read model, the pipeline or MQTT.
- New specs pin the behavior: with no streaming client attached, the read model state and a second pipeline consumer keep receiving events well beyond the hub buffer size, and a non-consuming streaming client does not stop them either.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `consumer-model`: the "ForecastReadModel serves unary and streaming RPCs" requirement adds that the secondary hub never backpressures the read model (permanent drain) and that each streaming client gets a bounded drop-oldest buffer.

## Non-goals

- No change to the pipeline output hub in `ForecastPipeline` (`pipeline-composition` keeps it free of Buffer/KeepAlive stages); its consumers are the always-running MQTT publisher and the read model, which this change keeps draining.
- No fix for the other read-model and MQTT deviations found in the spec audit: `GetEnrichments` returning only the latest analysis event, `StreamEnrichments` ignoring the location filter, and MQTT publish errors stopping the MQTT stream. These are separate changes.
- No change to the gRPC wire format or to the events a keeping-up client receives.

## Impact

- `src/Njord.Pipeline/ForecastReadModel.cs`: the drain in `MaterializeGraph`, the per-client buffer in `HandleStreamRequest`.
- `src/Njord.Pipeline.Tests/ForecastReadModelSpec.cs`: new backpressure specs.
- No API, message, options or persistence change. No polling change, so no API-budget impact (requests/month unchanged against the 300k free-tier limit).
